#include <SPI.h>
#include <MFRC522.h>
#include <LiquidCrystal_I2C.h>
#include <Servo.h>
#include <SoftwareSerial.h>

// --- Bluetooth (SoftwareSerial) ---
// HC-05 TXD -> Arduino RX (pin 2)
// HC-05 RXD -> Arduino TX (pin 3)
// IMPORTANT: USE A VOLTAGE DIVIDER FOR HC-05 RX!
SoftwareSerial btSerial(2, 3); // RX (Arduino), TX (Arduino)

// --- RFID ---
#define SS_PIN 10
#define RST_PIN 9
MFRC522 rfid(SS_PIN, RST_PIN);

// --- LCD 16x2 ---
LiquidCrystal_I2C lcd(0x27, 16, 2);

// --- Servo ---
Servo myServo;
const int SERVO_PIN = 5;
const int SERVO_OPEN_ANGLE = 90;
const int SERVO_CLOSED_ANGLE = 0;
const unsigned long SERVO_OPEN_DURATION = 3000;

const int TRIG_PIN_1 = 7;
const int ECHO_PIN_1 = 8;
const int TRIG_PIN_2 = 4;
const int ECHO_PIN_2 = 6;
const int TRIG_PIN_3 = A0;
const int ECHO_PIN_3 = A1;

const int OCCUPIED_THRESHOLD_CM = 20;

bool servoActive = false;
unsigned long activationTime = 0;
unsigned long blockedCardTime = 0;
bool blockedCardDisplayed = false;

unsigned long lastParkingStatusUpdateTime = 0;
const unsigned long PARKING_STATUS_UPDATE_INTERVAL = 500;

const byte allowedCards[3][4] = {
    {0xB0, 0x41, 0xCE, 0x32},
    {0x83, 0x68, 0x4C, 0xAB},
    {0x33, 0x04, 0x84, 0xAB}
};

const byte blockedCards[2][4] = {
    {0x43, 0x36, 0x54, 0xAB},
    {0xE3, 0x6A, 0x3B, 0xAB}
};

bool compareUID(byte *uid, const byte list[][4], int size)
{
    for (int i = 0; i < size; i++)
    {
        if (memcmp(uid, list[i], 4) == 0)
        {
            return true;
        }
    }

    return false;
}

long getDistanceCm(int trigPin, int echoPin)
{
    digitalWrite(trigPin, LOW);
    delayMicroseconds(2);
    digitalWrite(trigPin, HIGH);
    delayMicroseconds(10);
    digitalWrite(trigPin, LOW);

    long duration = pulseIn(echoPin, HIGH, 25000);
    if (duration == 0)
    {
        return -1;
    }

    long distanceCm = duration * 0.0343 / 2;
    if (distanceCm < 0 || distanceCm > 400)
    {
        return -1;
    }

    return distanceCm;
}

void setup()
{
    Serial.begin(9600);
    Serial.println("Arduino Parking System Started with HC-SR04 Sensors.");
    Serial.println("Initializing HC-05 connection...");

    btSerial.begin(9600);
    Serial.println("HC-05 SoftwareSerial initialized at 9600 baud.");

    SPI.begin();
    rfid.PCD_Init();

    lcd.init();
    lcd.backlight();
    lcd.setCursor(0, 0);
    lcd.print("Scan Card      ");

    myServo.attach(SERVO_PIN);
    myServo.write(SERVO_CLOSED_ANGLE);

    pinMode(TRIG_PIN_1, OUTPUT);
    pinMode(ECHO_PIN_1, INPUT);
    pinMode(TRIG_PIN_2, OUTPUT);
    pinMode(ECHO_PIN_2, INPUT);
    pinMode(TRIG_PIN_3, OUTPUT);
    pinMode(ECHO_PIN_3, INPUT);
}

void loop()
{
    if (rfid.PICC_IsNewCardPresent() && rfid.PICC_ReadCardSerial())
    {
        lcd.setCursor(0, 0);

        if (compareUID(rfid.uid.uidByte, allowedCards, 3))
        {
            lcd.print("Access Granted  ");

            if (!servoActive)
            {
                myServo.write(SERVO_OPEN_ANGLE);
                servoActive = true;
                activationTime = millis();
            }
        }
        else if (compareUID(rfid.uid.uidByte, blockedCards, 2))
        {
            lcd.print("Blocked Card    ");
            blockedCardTime = millis();
            blockedCardDisplayed = true;
        }
        else
        {
            lcd.print("Invalid Card    ");
            blockedCardTime = millis();
            blockedCardDisplayed = true;
        }

        rfid.PICC_HaltA();
        rfid.PCD_StopCrypto1();
    }

    if (blockedCardDisplayed && millis() - blockedCardTime >= 3000)
    {
        lcd.setCursor(0, 0);
        lcd.print("Scan Card      ");
        blockedCardDisplayed = false;
    }

    if (servoActive && millis() - activationTime >= SERVO_OPEN_DURATION)
    {
        myServo.write(SERVO_CLOSED_ANGLE);
        servoActive = false;

        if (!blockedCardDisplayed)
        {
            lcd.setCursor(0, 0);
            lcd.print("Scan Card      ");
        }
    }

    if (millis() - lastParkingStatusUpdateTime >= PARKING_STATUS_UPDATE_INTERVAL)
    {
        lastParkingStatusUpdateTime = millis();

        int distance1 = getDistanceCm(TRIG_PIN_1, ECHO_PIN_1);
        int distance2 = getDistanceCm(TRIG_PIN_2, ECHO_PIN_2);
        int distance3 = getDistanceCm(TRIG_PIN_3, ECHO_PIN_3);

        char status1 = (distance1 != -1 && distance1 < OCCUPIED_THRESHOLD_CM) ? 'O' : 'F';
        char status2 = (distance2 != -1 && distance2 < OCCUPIED_THRESHOLD_CM) ? 'O' : 'F';
        char status3 = (distance3 != -1 && distance3 < OCCUPIED_THRESHOLD_CM) ? 'O' : 'F';

        btSerial.print("P1 ");
        btSerial.println(status1);

        btSerial.print("P2 ");
        btSerial.println(status2);

        btSerial.print("P3 ");
        btSerial.println(status3);

        Serial.print("P1 Dist: ");
        if (distance1 == -1)
        {
            Serial.print("N/A");
        }
        else
        {
            Serial.print(distance1);
        }
        Serial.print("cm -> ");
        Serial.println(status1);

        Serial.print("P2 Dist: ");
        if (distance2 == -1)
        {
            Serial.print("N/A");
        }
        else
        {
            Serial.print(distance2);
        }
        Serial.print("cm -> ");
        Serial.println(status2);

        Serial.print("P3 Dist: ");
        if (distance3 == -1)
        {
            Serial.print("N/A");
        }
        else
        {
            Serial.print(distance3);
        }
        Serial.print("cm -> ");
        Serial.println(status3);

        Serial.println("---");
    }

    delay(50);
}
