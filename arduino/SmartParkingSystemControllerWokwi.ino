#include <EEPROM.h>
#include <MFRC522.h>
#include <Servo.h>
#include <SPI.h>
#include <ctype.h>
#include <stdlib.h>
#include <string.h>

#include <LiquidCrystal_I2C.h>

// -----------------------------------------------------------------------------
// Smart Parking System Controller — Wokwi build
// -----------------------------------------------------------------------------
// Це варіант основної прошивки, адаптований для запуску в Wokwi.
// Уся логіка паркінгу, RFID, LCD, servo, HC-SR04 та EEPROM збережена 1:1.
//
// Єдина відмінність — канал зв'язку із застосунком:
// команди йдуть через апаратний Serial (Serial Monitor), без HC-05/HC-06.
//
// Протокол команд / відповідей |||...||| залишений без змін.
// -----------------------------------------------------------------------------

#define COMM Serial

// -----------------------------------------------------------------------------
// RFID
// -----------------------------------------------------------------------------
constexpr uint8_t SS_PIN = 10;
constexpr uint8_t RST_PIN = 9;
MFRC522 rfid(SS_PIN, RST_PIN);

// -----------------------------------------------------------------------------
// LCD 16x2 по I2C
// -----------------------------------------------------------------------------
LiquidCrystal_I2C lcd(0x27, 16, 2);

// -----------------------------------------------------------------------------
// Servo воріт
// -----------------------------------------------------------------------------
Servo gateServo;

// -----------------------------------------------------------------------------
// Базові системні константи
// -----------------------------------------------------------------------------
constexpr uint8_t SERVO_PIN = 5;
constexpr uint8_t SLOT_COUNT = 6;
constexpr uint8_t MAX_CARD_COUNT = 3;
constexpr uint8_t UID_LENGTH = 4;
constexpr uint8_t DISPLAY_TEXT_LENGTH = 16;
constexpr uint8_t NO_PIN = 255;
constexpr long COMM_BAUD_RATE = 9600;
constexpr uint16_t CONFIG_SIGNATURE = 0x5350;
constexpr uint8_t CONFIG_VERSION = 5;
constexpr unsigned long LCD_MESSAGE_DURATION_MS = 3000UL;
constexpr uint16_t EEPROM_ADDRESS = 0;
constexpr size_t RX_BUFFER_SIZE = 40;
constexpr uint8_t ALL_SLOTS_ENABLED_MASK = (1 << SLOT_COUNT) - 1;
constexpr char PROTOCOL_FRAME_MARKER[] = "|||";
constexpr uint8_t PROTOCOL_FRAME_MARKER_LENGTH = 3;
constexpr unsigned long GATE_PASSAGE_AUTO_EXIT_COOLDOWN_MS = 3000UL;
constexpr uint8_t GATE_PASSAGE_STABILITY_READS = 2;

// -----------------------------------------------------------------------------
// Піни паркомісць
// -----------------------------------------------------------------------------
const uint8_t trigPins[SLOT_COUNT] = {7, 4, A0, NO_PIN, NO_PIN, NO_PIN};
const uint8_t echoPins[SLOT_COUNT] = {8, 6, A1, NO_PIN, NO_PIN, NO_PIN};
constexpr uint8_t GATE_PASSAGE_TRIG_PIN = A2;
constexpr uint8_t GATE_PASSAGE_ECHO_PIN = A3;

// -----------------------------------------------------------------------------
// Стан воріт
// -----------------------------------------------------------------------------
enum GateMode
{
    GATE_CLOSED,
    GATE_TEMPORARY_OPEN,
    GATE_FORCED_OPEN,
    GATE_LOCKED
};

// -----------------------------------------------------------------------------
// Конфігурація, яка зберігається в EEPROM
// -----------------------------------------------------------------------------
struct PersistedConfig
{
    uint16_t signature;
    uint8_t version;

    uint8_t servoOpenAngle;
    uint8_t servoClosedAngle;
    uint16_t servoOpenDurationMs;

    uint16_t occupiedThresholdCm;

    uint16_t telemetryIntervalMs;

    uint8_t forceGateOpen;
    uint8_t forceGateLock;
    uint8_t autoExitOpenEnabled;
    uint8_t autoCloseAfterPassEnabled;
    uint16_t gatePassageThresholdCm;

    uint8_t slotEnabledMask;

    uint8_t displayForceEnabled;
    char displayForcedText[DISPLAY_TEXT_LENGTH + 1];
    char displayDefaultText[DISPLAY_TEXT_LENGTH + 1];
    char displayAllowedText[DISPLAY_TEXT_LENGTH + 1];
    char displayBlockedText[DISPLAY_TEXT_LENGTH + 1];
    char displayInvalidText[DISPLAY_TEXT_LENGTH + 1];
    char displayLockedText[DISPLAY_TEXT_LENGTH + 1];

    uint8_t allowedCount;
    byte allowedCards[MAX_CARD_COUNT][UID_LENGTH];

    uint8_t blockedCount;
    byte blockedCards[MAX_CARD_COUNT][UID_LENGTH];
};

PersistedConfig config;

// -----------------------------------------------------------------------------
// Глобальний runtime-стан
// -----------------------------------------------------------------------------
GateMode gateMode = GATE_CLOSED;
bool temporaryGateOpen = false;
unsigned long temporaryGateExpiresAt = 0;
unsigned long lastTelemetryAt = 0;
unsigned long messageVisibleUntil = 0;
unsigned long transientDisplayUntil = 0;
char currentDisplayText[DISPLAY_TEXT_LENGTH + 1] = "";
char transientDisplayText[DISPLAY_TEXT_LENGTH + 1] = "";
bool currentDisplayForced = false;
bool gatePassageOccupied = false;
bool previousGatePassageOccupied = false;
bool pendingGatePassageOccupied = false;
bool gatePassageAutoCloseArmed = false;
bool gatePassageVehicleSeen = false;
uint8_t gatePassageStableReads = 0;
int16_t gatePassageDistanceCm = -1;
unsigned long lastAutoExitOpenAt = 0;

unsigned long slotOccupiedSince[SLOT_COUNT] = {0};
bool slotOccupied[SLOT_COUNT] = {false};
int16_t slotDistanceCm[SLOT_COUNT] = {0};

char rxBuffer[RX_BUFFER_SIZE];
size_t rxIndex = 0;

char lcdLine1[17] = "Scan Card";
char lcdLine2[17] = "";

// -----------------------------------------------------------------------------
// Оголошення функцій
// -----------------------------------------------------------------------------
void setDefaultConfig();
void loadConfig();
void saveConfig();
void applyGateOutput();
void updateGateMode();
void updateGatePassageState();
void updateGatePassageAutomation();
void updateParkingStates();
void updateLcd();
void showMessage(const char *line1, const char *line2);
void updateDisplayState();
void setDisplayText(char target[DISPLAY_TEXT_LENGTH + 1], const char *value);
void setTransientDisplayText(const char *value);
void sendOk(const __FlashStringHelper *scope, const __FlashStringHelper *message);
void sendError(const __FlashStringHelper *scope, const __FlashStringHelper *message);
void sendOk(const char *scope, const char *message);
void sendError(const char *scope, const char *message);
void sendHello();
void sendProfile();
void sendConfig();
void sendTelemetry();
void sendCardsLine(const __FlashStringHelper *prefix, byte count, const byte list[][UID_LENGTH]);
void sendDisplayTextLine(const __FlashStringHelper *key, const char *value);
void beginProtocolFrame();
void endProtocolFrame();
bool unwrapProtocolFrame(char *line);
void handleSerialInput();
void dispatchCommand(char *line);
void handleHelloCommand(char *context);
void handleGetCommand(char *context);
void handleGateCommand(char *context);
void handleConfigCommand(char *context);
void handleParkingCommand(char *context);
void handleCardsCommand(char *context);
void handleDisplayCommand(char *context);
void handleRfid();
bool compareUid(const byte *uid, const byte list[][UID_LENGTH], byte count);
bool parseUidHex(const char *value, byte outUid[UID_LENGTH]);
int findUid(const byte target[UID_LENGTH], const byte list[][UID_LENGTH], byte count);
bool addUid(byte list[][UID_LENGTH], byte &count, const byte uid[UID_LENGTH]);
bool removeUid(byte list[][UID_LENGTH], byte &count, const byte uid[UID_LENGTH]);
void printUidHex(Stream &stream, const byte uid[UID_LENGTH]);
const __FlashStringHelper *getGateModeText();
const __FlashStringHelper *getSlotStateText(uint8_t slotIndex);
void writeSlotLine(uint8_t slotIndex);
long readDistanceCm(uint8_t trigPin, uint8_t echoPin);
void startTemporaryGateOpen(bool armAutoClose);
void resetGatePassageAutomation();
void trimLine(char *line);
bool isSlotEnabled(uint8_t slotIndex);
void setSlotEnabled(uint8_t slotIndex, bool isEnabled);
void printWokwiHelp();

// -----------------------------------------------------------------------------
// setup
// -----------------------------------------------------------------------------
void setup()
{
    // У Wokwi-режимі весь обмін іде через апаратний Serial.
    Serial.begin(COMM_BAUD_RATE);

    SPI.begin();
    rfid.PCD_Init();

    lcd.init();
    lcd.backlight();

    gateServo.attach(SERVO_PIN);

    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        if (trigPins[i] != NO_PIN && echoPins[i] != NO_PIN)
        {
            pinMode(trigPins[i], OUTPUT);
            pinMode(echoPins[i], INPUT);
        }
    }

    pinMode(GATE_PASSAGE_TRIG_PIN, OUTPUT);
    pinMode(GATE_PASSAGE_ECHO_PIN, INPUT);

    loadConfig();

    updateGatePassageState();
    updateGateMode();
    updateParkingStates();
    updateDisplayState();

    showMessage("Smart Parking", "Controller Ready");

    // Невелика підказка з прикладами команд для Serial Monitor.
    printWokwiHelp();

    sendHello();
}

// -----------------------------------------------------------------------------
// loop
// -----------------------------------------------------------------------------
void loop()
{
    handleSerialInput();
    handleRfid();

    updateGatePassageState();
    updateGatePassageAutomation();

    updateGateMode();

    updateParkingStates();
    updateDisplayState();

    updateLcd();

    delay(20);
}

// -----------------------------------------------------------------------------
// Wokwi help
// -----------------------------------------------------------------------------
void printWokwiHelp()
{
    Serial.println();
    Serial.println(F("=== Smart Parking Controller (Wokwi mode) ==="));
    Serial.println(F("Type a command and press Enter."));
    Serial.println(F("Examples:"));
    Serial.println(F("  HELLO SPS"));
    Serial.println(F("  GET PROFILE"));
    Serial.println(F("  GET CONFIG"));
    Serial.println(F("  GET SNAPSHOT"));
    Serial.println(F("  GATE OPEN_TEMP"));
    Serial.println(F("  GATE FORCE_OPEN ON"));
    Serial.println(F("  GATE FORCE_OPEN OFF"));
    Serial.println(F("  GATE LOCK ON"));
    Serial.println(F("  GATE LOCK OFF"));
    Serial.println(F("  GATE CLOSE"));
    Serial.println(F("  CONFIG SAVE"));
    Serial.println(F("  CONFIG RESET"));
    Serial.println(F("  PARKING ENABLE 1   /  PARKING DISABLE 1"));
    Serial.println(F("  CARDS ALLOWED ADD B041CE32"));
    Serial.println(F("  CARDS BLOCKED ADD 433654AB"));
    Serial.println(F("  DISPLAY FORCE ON  /  DISPLAY FORCE OFF"));
    Serial.println(F("  DISPLAY TEXT DEFAULT SMART PARKING"));
    Serial.println(F("============================================="));
    Serial.println();
}

// -----------------------------------------------------------------------------
// setDefaultConfig
// -----------------------------------------------------------------------------
void setDefaultConfig()
{
    memset(&config, 0, sizeof(config));

    config.signature = CONFIG_SIGNATURE;
    config.version = CONFIG_VERSION;

    config.servoOpenAngle = 90;
    config.servoClosedAngle = 0;
    config.servoOpenDurationMs = 3000;
    config.occupiedThresholdCm = 20;
    config.telemetryIntervalMs = 500;
    config.forceGateOpen = 0;
    config.forceGateLock = 0;
    config.autoExitOpenEnabled = 0;
    config.autoCloseAfterPassEnabled = 1;
    config.gatePassageThresholdCm = 20;

    config.slotEnabledMask = ALL_SLOTS_ENABLED_MASK;
    config.displayForceEnabled = 0;
    setDisplayText(config.displayForcedText, "FORCED TEXT");
    setDisplayText(config.displayDefaultText, "SMART PARKING");
    setDisplayText(config.displayAllowedText, "ACCESS GRANTED");
    setDisplayText(config.displayBlockedText, "BLOCKED CARD");
    setDisplayText(config.displayInvalidText, "INVALID CARD");
    setDisplayText(config.displayLockedText, "ACCESS LOCKED");

    config.allowedCount = 3;
    byte defaultAllowed[3][UID_LENGTH] = {
        {0xB0, 0x41, 0xCE, 0x32},
        {0x83, 0x68, 0x4C, 0xAB},
        {0x33, 0x04, 0x84, 0xAB}};
    memcpy(config.allowedCards, defaultAllowed, sizeof(defaultAllowed));

    config.blockedCount = 2;
    byte defaultBlocked[2][UID_LENGTH] = {
        {0x43, 0x36, 0x54, 0xAB},
        {0xE3, 0x6A, 0x3B, 0xAB}};
    memcpy(config.blockedCards, defaultBlocked, sizeof(defaultBlocked));
}

void loadConfig()
{
    EEPROM.get(EEPROM_ADDRESS, config);

    if (config.signature != CONFIG_SIGNATURE || config.version != CONFIG_VERSION)
    {
        setDefaultConfig();
        saveConfig();
    }
}

void saveConfig()
{
    config.signature = CONFIG_SIGNATURE;
    config.version = CONFIG_VERSION;
    EEPROM.put(EEPROM_ADDRESS, config);
}

void setDisplayText(char target[DISPLAY_TEXT_LENGTH + 1], const char *value)
{
    size_t writeIndex = 0;

    if (value == nullptr)
    {
        target[0] = '\0';
        return;
    }

    while (*value != '\0' && writeIndex < DISPLAY_TEXT_LENGTH)
    {
        char current = *value++;
        if (current < 32 || current > 126 || current == '|')
        {
            continue;
        }

        target[writeIndex++] = current;
    }

    target[writeIndex] = '\0';
}

void setTransientDisplayText(const char *value)
{
    setDisplayText(transientDisplayText, value);
    transientDisplayUntil = millis() + LCD_MESSAGE_DURATION_MS;
    updateDisplayState();
}

void updateDisplayState()
{
    if (config.displayForceEnabled)
    {
        setDisplayText(currentDisplayText, config.displayForcedText);
        currentDisplayForced = true;
        return;
    }

    currentDisplayForced = false;

    if (transientDisplayUntil > millis() && transientDisplayText[0] != '\0')
    {
        setDisplayText(currentDisplayText, transientDisplayText);
        return;
    }

    setDisplayText(currentDisplayText, config.displayDefaultText);
}

void updateGateMode()
{
    GateMode previousMode = gateMode;

    if (config.forceGateLock)
    {
        gateMode = GATE_LOCKED;
        temporaryGateOpen = false;
        resetGatePassageAutomation();
    }
    else if (config.forceGateOpen)
    {
        gateMode = GATE_FORCED_OPEN;
        temporaryGateOpen = false;
        resetGatePassageAutomation();
    }
    else if (temporaryGateOpen && millis() < temporaryGateExpiresAt)
    {
        gateMode = GATE_TEMPORARY_OPEN;
    }
    else
    {
        temporaryGateOpen = false;
        resetGatePassageAutomation();
        gateMode = GATE_CLOSED;
    }

    if (previousMode != gateMode)
    {
        applyGateOutput();
    }
}

void applyGateOutput()
{
    if (gateMode == GATE_TEMPORARY_OPEN || gateMode == GATE_FORCED_OPEN)
    {
        gateServo.write(config.servoOpenAngle);
    }
    else
    {
        gateServo.write(config.servoClosedAngle);
    }
}

void updateGatePassageState()
{
    previousGatePassageOccupied = gatePassageOccupied;

    long distance = readDistanceCm(GATE_PASSAGE_TRIG_PIN, GATE_PASSAGE_ECHO_PIN);
    gatePassageDistanceCm = distance;
    bool measuredOccupied = distance != -1 && distance < config.gatePassageThresholdCm;

    if (measuredOccupied == pendingGatePassageOccupied)
    {
        if (gatePassageStableReads < GATE_PASSAGE_STABILITY_READS)
        {
            gatePassageStableReads++;
        }
    }
    else
    {
        pendingGatePassageOccupied = measuredOccupied;
        gatePassageStableReads = 1;
    }

    if (gatePassageStableReads >= GATE_PASSAGE_STABILITY_READS)
    {
        gatePassageOccupied = pendingGatePassageOccupied;
    }
}

void updateGatePassageAutomation()
{
    bool passageBecameOccupied = gatePassageOccupied && !previousGatePassageOccupied;
    bool passageBecameFree = !gatePassageOccupied && previousGatePassageOccupied;

    if (config.forceGateLock || config.forceGateOpen)
    {
        resetGatePassageAutomation();
        return;
    }

    if (config.autoExitOpenEnabled
        && gateMode == GATE_CLOSED
        && passageBecameOccupied
        && millis() - lastAutoExitOpenAt >= GATE_PASSAGE_AUTO_EXIT_COOLDOWN_MS)
    {
        startTemporaryGateOpen(true);
        lastAutoExitOpenAt = millis();
        showMessage("Auto Exit", "Gate Open");
        setTransientDisplayText(config.displayAllowedText);
    }

    if (!config.autoCloseAfterPassEnabled || gateMode != GATE_TEMPORARY_OPEN || !gatePassageAutoCloseArmed)
    {
        return;
    }

    if (passageBecameOccupied)
    {
        gatePassageVehicleSeen = true;
    }

    if (gatePassageVehicleSeen && passageBecameFree)
    {
        temporaryGateOpen = false;
        temporaryGateExpiresAt = 0;
        resetGatePassageAutomation();
    }
}

void startTemporaryGateOpen(bool armAutoClose)
{
    config.forceGateOpen = 0;
    temporaryGateOpen = true;
    temporaryGateExpiresAt = millis() + config.servoOpenDurationMs;
    gatePassageAutoCloseArmed = armAutoClose && config.autoCloseAfterPassEnabled;
    gatePassageVehicleSeen = gatePassageAutoCloseArmed && gatePassageOccupied;
}

void resetGatePassageAutomation()
{
    gatePassageAutoCloseArmed = false;
    gatePassageVehicleSeen = false;
}

void updateParkingStates()
{
    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        if (!isSlotEnabled(i))
        {
            slotDistanceCm[i] = -1;
            slotOccupied[i] = false;
            slotOccupiedSince[i] = 0;
            continue;
        }

        if (trigPins[i] == NO_PIN || echoPins[i] == NO_PIN)
        {
            slotDistanceCm[i] = -1;
            slotOccupied[i] = false;
            slotOccupiedSince[i] = 0;
            continue;
        }

        long distance = readDistanceCm(trigPins[i], echoPins[i]);
        slotDistanceCm[i] = distance;

        bool isOccupiedNow = distance != -1 && distance < config.occupiedThresholdCm;

        if (isOccupiedNow && !slotOccupied[i])
        {
            slotOccupiedSince[i] = millis();
        }

        if (!isOccupiedNow)
        {
            slotOccupiedSince[i] = 0;
        }

        slotOccupied[i] = isOccupiedNow;
    }
}

void updateLcd()
{
    if (millis() > messageVisibleUntil)
    {
        uint8_t freeCount = 0;
        for (uint8_t i = 0; i < SLOT_COUNT; i++)
        {
            if (isSlotEnabled(i) && !slotOccupied[i])
            {
                freeCount++;
            }
        }

        snprintf(lcdLine1, sizeof(lcdLine1), "Scan Card");
        snprintf(lcdLine2, sizeof(lcdLine2), "Free:%u/%u", freeCount, SLOT_COUNT);
    }

    lcd.setCursor(0, 0);
    lcd.print("                ");
    lcd.setCursor(0, 0);
    lcd.print(lcdLine1);

    lcd.setCursor(0, 1);
    lcd.print("                ");
    lcd.setCursor(0, 1);
    lcd.print(lcdLine2);
}

void showMessage(const char *line1, const char *line2)
{
    strncpy(lcdLine1, line1, sizeof(lcdLine1) - 1);
    strncpy(lcdLine2, line2, sizeof(lcdLine2) - 1);
    lcdLine1[sizeof(lcdLine1) - 1] = '\0';
    lcdLine2[sizeof(lcdLine2) - 1] = '\0';
    messageVisibleUntil = millis() + LCD_MESSAGE_DURATION_MS;
}

void sendOk(const __FlashStringHelper *scope, const __FlashStringHelper *message)
{
    beginProtocolFrame();
    COMM.print(F("OK|"));
    COMM.print(scope);
    COMM.print(F("|"));
    COMM.print(message);
    endProtocolFrame();
}

void sendError(const __FlashStringHelper *scope, const __FlashStringHelper *message)
{
    beginProtocolFrame();
    COMM.print(F("ERR|"));
    COMM.print(scope);
    COMM.print(F("|"));
    COMM.print(message);
    endProtocolFrame();
}

void sendOk(const char *scope, const char *message)
{
    beginProtocolFrame();
    COMM.print(F("OK|"));
    COMM.print(scope);
    COMM.print(F("|"));
    COMM.print(message);
    endProtocolFrame();
}

void sendError(const char *scope, const char *message)
{
    beginProtocolFrame();
    COMM.print(F("ERR|"));
    COMM.print(scope);
    COMM.print(F("|"));
    COMM.print(message);
    endProtocolFrame();
}

void sendHello()
{
    beginProtocolFrame();
    COMM.print(F("HELLO_OK|device=SMART_PARKING|fw=2|slots=6|transport=SERIAL"));
    endProtocolFrame();
}

void sendProfile()
{
    beginProtocolFrame();
    COMM.print(F("PROFILE|board=ATmega328P|rfid=MFRC522|lcd=I2C_16X2|gate=SERVO|transport=SERIAL|slots=6"));
    endProtocolFrame();
}

void sendConfig()
{
    beginProtocolFrame();
    COMM.print(F("CONFIG|open_angle="));
    COMM.print(config.servoOpenAngle);
    COMM.print(F("|closed_angle="));
    COMM.print(config.servoClosedAngle);
    COMM.print(F("|open_duration_ms="));
    COMM.print(config.servoOpenDurationMs);
    COMM.print(F("|threshold_cm="));
    COMM.print(config.occupiedThresholdCm);
    COMM.print(F("|telemetry_ms="));
    COMM.print(config.telemetryIntervalMs);
    COMM.print(F("|force_open="));
    COMM.print(config.forceGateOpen);
    COMM.print(F("|force_lock="));
    COMM.print(config.forceGateLock);
    COMM.print(F("|auto_exit_open="));
    COMM.print(config.autoExitOpenEnabled);
    COMM.print(F("|auto_close_after_pass="));
    COMM.print(config.autoCloseAfterPassEnabled);
    COMM.print(F("|passage_threshold_cm="));
    COMM.print(config.gatePassageThresholdCm);
    endProtocolFrame();

    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        beginProtocolFrame();
        COMM.print(F("SLOTCFG|"));
        COMM.print(i + 1);
        COMM.print(F("|enabled="));
        COMM.print(isSlotEnabled(i) ? 1 : 0);
        endProtocolFrame();
    }

    beginProtocolFrame();
    COMM.print(F("DISPLAYCFG|force="));
    COMM.print(config.displayForceEnabled ? 1 : 0);
    COMM.print(F("|forced_text="));
    COMM.print(config.displayForcedText);
    endProtocolFrame();

    sendDisplayTextLine(F("DEFAULT"), config.displayDefaultText);
    sendDisplayTextLine(F("ALLOWED"), config.displayAllowedText);
    sendDisplayTextLine(F("BLOCKED"), config.displayBlockedText);
    sendDisplayTextLine(F("INVALID"), config.displayInvalidText);
    sendDisplayTextLine(F("LOCKED"), config.displayLockedText);

    sendCardsLine(F("CARDS_ALLOWED"), config.allowedCount, config.allowedCards);
    sendCardsLine(F("CARDS_BLOCKED"), config.blockedCount, config.blockedCards);
}

void sendTelemetry()
{
    unsigned long remainingMs = 0;
    if (gateMode == GATE_TEMPORARY_OPEN && temporaryGateExpiresAt > millis())
    {
        remainingMs = temporaryGateExpiresAt - millis();
    }

    beginProtocolFrame();
    COMM.print(F("SNAPSHOT|mode="));
    COMM.print(getGateModeText());
    COMM.print(F("|remaining_ms="));
    COMM.print(remainingMs);
    COMM.print(F("|locked="));
    COMM.print(config.forceGateLock);
    COMM.print(F("|force_open="));
    COMM.print(config.forceGateOpen);
    COMM.print(F("|open_angle="));
    COMM.print(config.servoOpenAngle);
    COMM.print(F("|closed_angle="));
    COMM.print(config.servoClosedAngle);
    COMM.print(F("|open_duration_ms="));
    COMM.print(config.servoOpenDurationMs);
    COMM.print(F("|threshold_cm="));
    COMM.print(config.occupiedThresholdCm);
    COMM.print(F("|telemetry_ms="));
    COMM.print(config.telemetryIntervalMs);
    COMM.print(F("|passage_occupied="));
    COMM.print(gatePassageOccupied ? 1 : 0);
    COMM.print(F("|passage_distance_cm="));
    COMM.print(gatePassageDistanceCm);
    endProtocolFrame();

    beginProtocolFrame();
    COMM.print(F("DISPLAY|text="));
    COMM.print(currentDisplayText);
    COMM.print(F("|forced="));
    COMM.print(currentDisplayForced ? 1 : 0);
    endProtocolFrame();

    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        writeSlotLine(i);
    }

    beginProtocolFrame();
    COMM.print(F("COUNTS|allowed="));
    COMM.print(config.allowedCount);
    COMM.print(F("|blocked="));
    COMM.print(config.blockedCount);
    endProtocolFrame();
}

void sendCardsLine(const __FlashStringHelper *prefix, byte count, const byte list[][UID_LENGTH])
{
    beginProtocolFrame();
    COMM.print(prefix);
    COMM.print(F("|count="));
    COMM.print(count);

    for (byte i = 0; i < count; i++)
    {
        COMM.print(F("|"));
        printUidHex(COMM, list[i]);
    }

    endProtocolFrame();
}

void sendDisplayTextLine(const __FlashStringHelper *key, const char *value)
{
    beginProtocolFrame();
    COMM.print(F("DISPLAYTEXT|key="));
    COMM.print(key);
    COMM.print(F("|value="));
    COMM.print(value);
    endProtocolFrame();
}

void writeSlotLine(uint8_t slotIndex)
{
    beginProtocolFrame();
    COMM.print(F("SLOT|"));
    COMM.print(slotIndex + 1);
    COMM.print(F("|state="));
    COMM.print(getSlotStateText(slotIndex));
    COMM.print(F("|enabled="));
    COMM.print(isSlotEnabled(slotIndex) ? 1 : 0);
    COMM.print(F("|distance_cm="));
    COMM.print(slotDistanceCm[slotIndex]);
    COMM.print(F("|occupied_ms="));

    if (slotOccupied[slotIndex] && slotOccupiedSince[slotIndex] != 0)
    {
        COMM.print(millis() - slotOccupiedSince[slotIndex]);
    }
    else
    {
        COMM.print(0);
    }
    endProtocolFrame();
}

void beginProtocolFrame()
{
    COMM.print(PROTOCOL_FRAME_MARKER);
}

void endProtocolFrame()
{
    COMM.print(PROTOCOL_FRAME_MARKER);
    COMM.print('\n');
}

bool unwrapProtocolFrame(char *line)
{
    trimLine(line);
    size_t length = strlen(line);
    if (length == 0)
    {
        return false;
    }

    uint8_t leadingMarkerLength = 0;
    while (leadingMarkerLength < PROTOCOL_FRAME_MARKER_LENGTH && line[leadingMarkerLength] == '|')
    {
        leadingMarkerLength++;
    }

    if (leadingMarkerLength == 0)
    {
        // Команда без обгортки |||...||| — у Wokwi Serial Monitor це норма.
        return true;
    }

    if (length < leadingMarkerLength + PROTOCOL_FRAME_MARKER_LENGTH)
    {
        return false;
    }

    if (strcmp(line + length - PROTOCOL_FRAME_MARKER_LENGTH, PROTOCOL_FRAME_MARKER) != 0)
    {
        return false;
    }

    line[length - PROTOCOL_FRAME_MARKER_LENGTH] = '\0';
    memmove(line, line + leadingMarkerLength, strlen(line + leadingMarkerLength) + 1);
    trimLine(line);
    return strlen(line) > 0;
}

void handleSerialInput()
{
    while (COMM.available() > 0)
    {
        char incoming = static_cast<char>(COMM.read());
        if (incoming == '\r')
        {
            continue;
        }

        if (incoming == '\n')
        {
            rxBuffer[rxIndex] = '\0';
            if (rxIndex > 0)
            {
                if (unwrapProtocolFrame(rxBuffer))
                {
                    dispatchCommand(rxBuffer);
                }
            }

            rxIndex = 0;
            continue;
        }

        if (rxIndex < RX_BUFFER_SIZE - 1)
        {
            rxBuffer[rxIndex++] = incoming;
        }
        else
        {
            rxIndex = 0;
            sendError(F("RX"), F("BUFFER_OVERFLOW"));
        }
    }
}

void dispatchCommand(char *line)
{
    char originalLine[RX_BUFFER_SIZE];
    strncpy(originalLine, line, sizeof(originalLine) - 1);
    originalLine[sizeof(originalLine) - 1] = '\0';

    char *context = nullptr;
    char *command = strtok_r(line, " ", &context);
    if (command == nullptr)
    {
        return;
    }

    if (strcmp(command, "HELLO") == 0)
    {
        handleHelloCommand(context);
    }
    else if (strcmp(command, "GET") == 0)
    {
        handleGetCommand(context);
    }
    else if (strcmp(command, "GATE") == 0)
    {
        handleGateCommand(context);
    }
    else if (strcmp(command, "CONFIG") == 0)
    {
        handleConfigCommand(context);
    }
    else if (strcmp(command, "PARKING") == 0)
    {
        handleParkingCommand(context);
    }
    else if (strcmp(command, "CARDS") == 0)
    {
        handleCardsCommand(context);
    }
    else if (strcmp(command, "DISPLAY") == 0)
    {
        handleDisplayCommand(context);
    }
    else
    {
        beginProtocolFrame();
        COMM.print(F("RECEIVED|"));
        COMM.print(originalLine);
        endProtocolFrame();
        sendError(F("CMD"), F("UNKNOWN_COMMAND"));
    }
}

void handleHelloCommand(char *context)
{
    char *token = strtok_r(nullptr, " ", &context);
    if (token != nullptr && strcmp(token, "SPS") == 0)
    {
        sendHello();
        return;
    }

    sendError("HELLO", "INVALID_TOKEN");
}

void handleGetCommand(char *context)
{
    char *token = strtok_r(nullptr, " ", &context);
    if (token == nullptr)
    {
        sendError("GET", "MISSING_TARGET");
        return;
    }

    if (strcmp(token, "SNAPSHOT") == 0)
    {
        sendTelemetry();
    }
    else if (strcmp(token, "CONFIG") == 0)
    {
        sendConfig();
    }
    else if (strcmp(token, "PROFILE") == 0)
    {
        sendProfile();
    }
    else
    {
        sendError("GET", "UNKNOWN_TARGET");
    }
}

void handleGateCommand(char *context)
{
    char *action = strtok_r(nullptr, " ", &context);
    if (action == nullptr)
    {
        sendError("GATE", "MISSING_ACTION");
        return;
    }

    if (strcmp(action, "FORCE_OPEN") == 0)
    {
        char *value = strtok_r(nullptr, " ", &context);
        if (value == nullptr)
        {
            sendError("GATE", "MISSING_FORCE_OPEN_VALUE");
            return;
        }

        config.forceGateOpen = strcmp(value, "ON") == 0 ? 1 : 0;

        if (config.forceGateOpen)
        {
            config.forceGateLock = 0;
        }

        resetGatePassageAutomation();
        updateGateMode();
        sendOk("GATE", "FORCE_OPEN_UPDATED");
    }
    else if (strcmp(action, "OPEN_TEMP") == 0)
    {
        if (config.forceGateLock)
        {
            sendError("GATE", "LOCKED");
            return;
        }

        startTemporaryGateOpen(true);
        updateGateMode();
        sendOk("GATE", "TEMP_OPEN_STARTED");
    }
    else if (strcmp(action, "CLOSE") == 0)
    {
        config.forceGateOpen = 0;
        temporaryGateOpen = false;
        temporaryGateExpiresAt = 0;
        resetGatePassageAutomation();
        updateGateMode();
        sendOk("GATE", "CLOSED");
    }
    else if (strcmp(action, "LOCK") == 0)
    {
        char *value = strtok_r(nullptr, " ", &context);
        if (value == nullptr)
        {
            sendError("GATE", "MISSING_LOCK_VALUE");
            return;
        }

        if (strcmp(value, "ON") == 0)
        {
            config.forceGateLock = 1;
            config.forceGateOpen = 0;
            resetGatePassageAutomation();
        }
        else if (strcmp(value, "OFF") == 0)
        {
            config.forceGateLock = 0;
        }
        else
        {
            sendError("GATE", "INVALID_LOCK_VALUE");
            return;
        }

        updateGateMode();
        sendOk("GATE", "LOCK_UPDATED");
    }
    else
    {
        sendError("GATE", "UNKNOWN_ACTION");
    }
}

void handleConfigCommand(char *context)
{
    char *action = strtok_r(nullptr, " ", &context);
    if (action == nullptr)
    {
        sendError("CONFIG", "MISSING_ACTION");
        return;
    }

    if (strcmp(action, "SAVE") == 0)
    {
        saveConfig();
        sendOk("CONFIG", "SAVED");
        return;
    }

    if (strcmp(action, "RESET") == 0)
    {
        setDefaultConfig();
        saveConfig();
        updateGateMode();
        updateParkingStates();
        updateDisplayState();
        sendOk("CONFIG", "RESET_TO_DEFAULTS");
        return;
    }

    char *value = strtok_r(nullptr, " ", &context);
    if (value == nullptr)
    {
        sendError("CONFIG", "MISSING_VALUE");
        return;
    }

    long parsedValue = atol(value);

    if (strcmp(action, "OPEN_ANGLE") == 0)
    {
        config.servoOpenAngle = constrain(parsedValue, 0, 180);
        updateGateMode();
        sendOk("CONFIG", "OPEN_ANGLE_UPDATED");
    }
    else if (strcmp(action, "CLOSED_ANGLE") == 0)
    {
        config.servoClosedAngle = constrain(parsedValue, 0, 180);
        updateGateMode();
        sendOk("CONFIG", "CLOSED_ANGLE_UPDATED");
    }
    else if (strcmp(action, "OPEN_DURATION_MS") == 0)
    {
        config.servoOpenDurationMs = max(250L, parsedValue);
        updateGateMode();
        sendOk("CONFIG", "OPEN_DURATION_UPDATED");
    }
    else if (strcmp(action, "THRESHOLD_CM") == 0)
    {
        config.occupiedThresholdCm = max(1L, parsedValue);
        updateGateMode();
        sendOk("CONFIG", "THRESHOLD_UPDATED");
    }
    else if (strcmp(action, "TELEMETRY_MS") == 0)
    {
        config.telemetryIntervalMs = max(250L, parsedValue);
        updateGateMode();
        sendOk("CONFIG", "TELEMETRY_UPDATED");
    }
    else if (strcmp(action, "AUTO_EXIT_OPEN") == 0)
    {
        if (strcmp(value, "ON") == 0)
        {
            config.autoExitOpenEnabled = 1;
        }
        else if (strcmp(value, "OFF") == 0)
        {
            config.autoExitOpenEnabled = 0;
        }
        else
        {
            sendError("CONFIG", "INVALID_AUTO_EXIT_OPEN_VALUE");
            return;
        }

        sendOk("CONFIG", "AUTO_EXIT_OPEN_UPDATED");
    }
    else if (strcmp(action, "AUTO_CLOSE_AFTER_PASS") == 0)
    {
        if (strcmp(value, "ON") == 0)
        {
            config.autoCloseAfterPassEnabled = 1;
        }
        else if (strcmp(value, "OFF") == 0)
        {
            config.autoCloseAfterPassEnabled = 0;
            resetGatePassageAutomation();
        }
        else
        {
            sendError("CONFIG", "INVALID_AUTO_CLOSE_AFTER_PASS_VALUE");
            return;
        }

        sendOk("CONFIG", "AUTO_CLOSE_AFTER_PASS_UPDATED");
    }
    else if (strcmp(action, "PASSAGE_THRESHOLD_CM") == 0)
    {
        config.gatePassageThresholdCm = max(1L, parsedValue);
        updateGatePassageState();
        sendOk("CONFIG", "PASSAGE_THRESHOLD_UPDATED");
    }
    else
    {
        sendError("CONFIG", "UNKNOWN_FIELD");
        return;
    }
}

void handleParkingCommand(char *context)
{
    char *action = strtok_r(nullptr, " ", &context);
    char *slotToken = strtok_r(nullptr, " ", &context);
    if (action == nullptr || slotToken == nullptr)
    {
        sendError("PARKING", "INVALID_COMMAND");
        return;
    }

    int slotNumber = atoi(slotToken);
    if (slotNumber < 1 || slotNumber > SLOT_COUNT)
    {
        sendError("PARKING", "INVALID_SLOT");
        return;
    }

    uint8_t index = static_cast<uint8_t>(slotNumber - 1);
    if (strcmp(action, "ENABLE") == 0)
    {
        setSlotEnabled(index, true);
        sendOk("PARKING", "SLOT_ENABLED");
    }
    else if (strcmp(action, "DISABLE") == 0)
    {
        setSlotEnabled(index, false);
        sendOk("PARKING", "SLOT_DISABLED");
    }
    else
    {
        sendError("PARKING", "UNKNOWN_ACTION");
        return;
    }

    updateParkingStates();
}

void handleCardsCommand(char *context)
{
    char *listType = strtok_r(nullptr, " ", &context);
    char *action = strtok_r(nullptr, " ", &context);
    if (listType == nullptr || action == nullptr)
    {
        sendError("CARDS", "INVALID_COMMAND");
        return;
    }

    byte (*targetList)[UID_LENGTH] = nullptr;
    byte *targetCount = nullptr;

    if (strcmp(listType, "ALLOWED") == 0)
    {
        targetList = config.allowedCards;
        targetCount = &config.allowedCount;
    }
    else if (strcmp(listType, "BLOCKED") == 0)
    {
        targetList = config.blockedCards;
        targetCount = &config.blockedCount;
    }
    else
    {
        sendError("CARDS", "UNKNOWN_LIST");
        return;
    }

    if (strcmp(action, "CLEAR") == 0)
    {
        *targetCount = 0;
        sendOk("CARDS", strcmp(listType, "ALLOWED") == 0 ? "ALLOWED_CLEARED" : "BLOCKED_CLEARED");
        return;
    }

    char *uidToken = strtok_r(nullptr, " ", &context);
    if (uidToken == nullptr)
    {
        sendError("CARDS", "MISSING_UID");
        return;
    }

    byte uid[UID_LENGTH];
    if (!parseUidHex(uidToken, uid))
    {
        sendError("CARDS", "INVALID_UID");
        return;
    }

    if (strcmp(action, "ADD") == 0)
    {
        if (addUid(targetList, *targetCount, uid))
        {
            sendOk("CARDS", strcmp(listType, "ALLOWED") == 0 ? "ALLOWED_ADDED" : "BLOCKED_ADDED");
        }
        else
        {
            sendError("CARDS", strcmp(listType, "ALLOWED") == 0 ? "ALLOWED_ADD_FAILED" : "BLOCKED_ADD_FAILED");
        }
    }
    else if (strcmp(action, "REMOVE") == 0)
    {
        if (removeUid(targetList, *targetCount, uid))
        {
            sendOk("CARDS", strcmp(listType, "ALLOWED") == 0 ? "ALLOWED_REMOVED" : "BLOCKED_REMOVED");
        }
        else
        {
            sendError("CARDS", strcmp(listType, "ALLOWED") == 0 ? "ALLOWED_REMOVE_FAILED" : "BLOCKED_REMOVE_FAILED");
        }
    }
    else
    {
        sendError("CARDS", "UNKNOWN_ACTION");
    }
}

void handleDisplayCommand(char *context)
{
    char *action = strtok_r(nullptr, " ", &context);
    if (action == nullptr)
    {
        sendError("DISPLAY", "MISSING_ACTION");
        return;
    }

    if (strcmp(action, "FORCE") == 0)
    {
        char *value = strtok_r(nullptr, " ", &context);
        if (value == nullptr)
        {
            sendError("DISPLAY", "MISSING_FORCE_VALUE");
            return;
        }

        if (strcmp(value, "ON") == 0)
        {
            config.displayForceEnabled = 1;
        }
        else if (strcmp(value, "OFF") == 0)
        {
            config.displayForceEnabled = 0;
        }
        else
        {
            sendError("DISPLAY", "INVALID_FORCE_VALUE");
            return;
        }

        updateDisplayState();
        sendOk("DISPLAY", "FORCE_UPDATED");
        return;
    }

    if (strcmp(action, "TEXT") == 0)
    {
        char *key = strtok_r(nullptr, " ", &context);
        if (key == nullptr)
        {
            sendError("DISPLAY", "MISSING_TEXT_KEY");
            return;
        }

        if (context == nullptr)
        {
            context = const_cast<char *>("");
        }

        trimLine(context);

        if (strcmp(key, "FORCED") == 0)
        {
            setDisplayText(config.displayForcedText, context);
        }
        else if (strcmp(key, "DEFAULT") == 0)
        {
            setDisplayText(config.displayDefaultText, context);
        }
        else if (strcmp(key, "ALLOWED") == 0)
        {
            setDisplayText(config.displayAllowedText, context);
        }
        else if (strcmp(key, "BLOCKED") == 0)
        {
            setDisplayText(config.displayBlockedText, context);
        }
        else if (strcmp(key, "INVALID") == 0)
        {
            setDisplayText(config.displayInvalidText, context);
        }
        else if (strcmp(key, "LOCKED") == 0)
        {
            setDisplayText(config.displayLockedText, context);
        }
        else
        {
            sendError("DISPLAY", "UNKNOWN_TEXT_KEY");
            return;
        }

        updateDisplayState();
        if (strcmp(key, "FORCED") == 0)
        {
            sendOk("DISPLAY", "TEXT_FORCED_UPDATED");
        }
        else if (strcmp(key, "DEFAULT") == 0)
        {
            sendOk("DISPLAY", "TEXT_DEFAULT_UPDATED");
        }
        else if (strcmp(key, "ALLOWED") == 0)
        {
            sendOk("DISPLAY", "TEXT_ALLOWED_UPDATED");
        }
        else if (strcmp(key, "BLOCKED") == 0)
        {
            sendOk("DISPLAY", "TEXT_BLOCKED_UPDATED");
        }
        else if (strcmp(key, "INVALID") == 0)
        {
            sendOk("DISPLAY", "TEXT_INVALID_UPDATED");
        }
        else
        {
            sendOk("DISPLAY", "TEXT_LOCKED_UPDATED");
        }
        return;
    }

    sendError("DISPLAY", "UNKNOWN_ACTION");
}

void handleRfid()
{
    if (!rfid.PICC_IsNewCardPresent() || !rfid.PICC_ReadCardSerial())
    {
        return;
    }

    byte uid[UID_LENGTH];
    memcpy(uid, rfid.uid.uidByte, UID_LENGTH);

    beginProtocolFrame();
    COMM.print(F("EVENT|card="));
    if (compareUid(uid, config.allowedCards, config.allowedCount))
    {
        if (!config.forceGateLock)
        {
            if (!config.forceGateOpen)
            {
                startTemporaryGateOpen(true);
            }

            updateGateMode();
            showMessage("Access Granted", "Gate Open");
            setTransientDisplayText(config.displayAllowedText);
            COMM.print(F("ALLOWED|uid="));
        }
        else
        {
            showMessage("Access Locked", "Gate Blocked");
            setTransientDisplayText(config.displayLockedText);
            COMM.print(F("LOCKED|uid="));
        }
    }
    else if (compareUid(uid, config.blockedCards, config.blockedCount))
    {
        showMessage("Blocked Card", "Access Denied");
        setTransientDisplayText(config.displayBlockedText);
        COMM.print(F("BLOCKED|uid="));
    }
    else
    {
        showMessage("Invalid Card", "Access Denied");
        setTransientDisplayText(config.displayInvalidText);
        COMM.print(F("INVALID|uid="));
    }

    printUidHex(COMM, uid);
    endProtocolFrame();

    rfid.PICC_HaltA();
    rfid.PCD_StopCrypto1();
}

bool compareUid(const byte *uid, const byte list[][UID_LENGTH], byte count)
{
    for (byte i = 0; i < count; i++)
    {
        if (memcmp(uid, list[i], UID_LENGTH) == 0)
        {
            return true;
        }
    }

    return false;
}

bool parseUidHex(const char *value, byte outUid[UID_LENGTH])
{
    char compact[UID_LENGTH * 2 + 1];
    uint8_t compactIndex = 0;

    for (size_t i = 0; value[i] != '\0'; i++)
    {
        if (isxdigit(value[i]))
        {
            if (compactIndex >= UID_LENGTH * 2)
            {
                return false;
            }

            compact[compactIndex++] = static_cast<char>(toupper(value[i]));
        }
    }

    if (compactIndex != UID_LENGTH * 2)
    {
        return false;
    }

    compact[compactIndex] = '\0';

    for (uint8_t i = 0; i < UID_LENGTH; i++)
    {
        char chunk[3] = {compact[i * 2], compact[i * 2 + 1], '\0'};
        outUid[i] = static_cast<byte>(strtoul(chunk, nullptr, 16));
    }

    return true;
}

int findUid(const byte target[UID_LENGTH], const byte list[][UID_LENGTH], byte count)
{
    for (byte i = 0; i < count; i++)
    {
        if (memcmp(target, list[i], UID_LENGTH) == 0)
        {
            return i;
        }
    }

    return -1;
}

bool addUid(byte list[][UID_LENGTH], byte &count, const byte uid[UID_LENGTH])
{
    if (count >= MAX_CARD_COUNT || findUid(uid, list, count) != -1)
    {
        return false;
    }

    memcpy(list[count], uid, UID_LENGTH);
    count++;
    return true;
}

bool removeUid(byte list[][UID_LENGTH], byte &count, const byte uid[UID_LENGTH])
{
    int index = findUid(uid, list, count);
    if (index < 0)
    {
        return false;
    }

    for (byte i = index; i < count - 1; i++)
    {
        memcpy(list[i], list[i + 1], UID_LENGTH);
    }

    memset(list[count - 1], 0, UID_LENGTH);
    count--;
    return true;
}

void printUidHex(Stream &stream, const byte uid[UID_LENGTH])
{
    for (uint8_t i = 0; i < UID_LENGTH; i++)
    {
        if (uid[i] < 0x10)
        {
            stream.print('0');
        }

        stream.print(uid[i], HEX);
    }
}

const __FlashStringHelper *getGateModeText()
{
    switch (gateMode)
    {
        case GATE_TEMPORARY_OPEN:
            return F("TEMP_OPEN");
        case GATE_FORCED_OPEN:
            return F("FORCED_OPEN");
        case GATE_LOCKED:
            return F("LOCKED");
        default:
            return F("CLOSED");
    }
}

const __FlashStringHelper *getSlotStateText(uint8_t slotIndex)
{
    if (!isSlotEnabled(slotIndex))
    {
        return F("DISABLED");
    }

    return slotOccupied[slotIndex] ? F("OCCUPIED") : F("FREE");
}

long readDistanceCm(uint8_t trigPin, uint8_t echoPin)
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

    long distance = duration * 0.0343 / 2;
    if (distance < 0 || distance > 400)
    {
        return -1;
    }

    return distance;
}

void trimLine(char *line)
{
    size_t length = strlen(line);
    while (length > 0 && isspace(line[length - 1]))
    {
        line[--length] = '\0';
    }

    char *start = line;
    while (*start != '\0' && isspace(*start))
    {
        start++;
    }

    if (start != line)
    {
        memmove(line, start, strlen(start) + 1);
    }
}

bool isSlotEnabled(uint8_t slotIndex)
{
    return slotIndex < SLOT_COUNT
        && (config.slotEnabledMask & (1 << slotIndex)) != 0;
}

void setSlotEnabled(uint8_t slotIndex, bool isEnabled)
{
    if (slotIndex >= SLOT_COUNT)
    {
        return;
    }

    if (isEnabled)
    {
        config.slotEnabledMask |= (1 << slotIndex);
    }
    else
    {
        config.slotEnabledMask &= static_cast<uint8_t>(~(1 << slotIndex));
    }
}
