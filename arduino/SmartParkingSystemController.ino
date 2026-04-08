#include <EEPROM.h>
#include <MFRC522.h>
#include <Servo.h>
#include <SoftwareSerial.h>
#include <SPI.h>
#include <ctype.h>
#include <stdlib.h>
#include <string.h>

#define ENABLE_LCD 0

#if ENABLE_LCD
#include <LiquidCrystal_I2C.h>
#endif

// -----------------------------------------------------------------------------
// Smart Parking System Controller
// -----------------------------------------------------------------------------
// Це основна прошивка контролера для системи смарт-паркінгу.
//
// Що вона робить:
// 1. Працює з RFID-картками через MFRC522.
// 2. Керує воротами через сервопривід.
// 3. Читає стан паркомісць через ультразвукові датчики.
// 4. Показує короткий стан на LCD 16x2.
// 5. Спілкується із застосунком через Bluetooth-модуль HC-05.
// 6. Зберігає редаговані параметри в EEPROM, щоб вони не губилися після перезапуску.
//
// Важлива ідея:
// Цей файл побудований не як "просто Arduino-скетч", а як контролер зі своїм
// простим текстовим протоколом. Тобто застосунок може не лише читати стан,
// а й надсилати команди:
// - відкрити / закрити / заблокувати ворота
// - змінити конфігурацію
// - вмикати / вимикати парковочні місця
// - редагувати списки RFID-карток
//
// Зауваження по фізичному залізу:
// - Логічних слотів у системі 6.
// - Фізично датчики зараз підключені тільки для перших 3 слотів.
// - Для слотів 4-6 прошивка зараз тримає "логічне" місце без датчика.
// -----------------------------------------------------------------------------

// -----------------------------------------------------------------------------
// Bluetooth
// -----------------------------------------------------------------------------
// Використовуємо програмний Serial для HC-05.
// RX Arduino = pin 2
// TX Arduino = pin 3
// Якщо підключається HC-05, для його RX бажано використовувати дільник напруги.
SoftwareSerial btSerial(2, 3);

// -----------------------------------------------------------------------------
// RFID
// -----------------------------------------------------------------------------
// SS і RST для модуля MFRC522.
constexpr uint8_t SS_PIN = 10;
constexpr uint8_t RST_PIN = 9;
MFRC522 rfid(SS_PIN, RST_PIN);

// -----------------------------------------------------------------------------
// LCD 16x2 по I2C
// -----------------------------------------------------------------------------
#if ENABLE_LCD
LiquidCrystal_I2C lcd(0x27, 16, 2);
#endif

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
constexpr long BT_BAUD_RATE = 9600;
constexpr uint16_t CONFIG_SIGNATURE = 0x5350;
constexpr uint8_t CONFIG_VERSION = 4;
constexpr unsigned long LCD_MESSAGE_DURATION_MS = 3000UL;
constexpr uint16_t EEPROM_ADDRESS = 0;
constexpr size_t RX_BUFFER_SIZE = 40;
constexpr uint8_t ALL_SLOTS_ENABLED_MASK = (1 << SLOT_COUNT) - 1;
constexpr char PROTOCOL_FRAME_MARKER[] = "|||";
constexpr uint8_t PROTOCOL_FRAME_MARKER_LENGTH = 3;

// -----------------------------------------------------------------------------
// Піни паркомісць
// -----------------------------------------------------------------------------
// Для слотів 1-3 реально задані ультразвукові датчики.
// Для слотів 4-6 стоїть NO_PIN, тобто датчик відсутній.
// Це дозволяє вже зараз тримати 6 логічних місць у протоколі застосунку.
const uint8_t trigPins[SLOT_COUNT] = {7, 4, A0, NO_PIN, NO_PIN, NO_PIN};
const uint8_t echoPins[SLOT_COUNT] = {8, 6, A1, NO_PIN, NO_PIN, NO_PIN};

// -----------------------------------------------------------------------------
// Стан воріт
// -----------------------------------------------------------------------------
// CLOSED         - звичайно закриті
// TEMPORARY_OPEN - відкриті на обмежений час
// FORCED_OPEN    - примусово відкриті
// LOCKED         - примусово заблоковані
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
// Це все, що редагується з застосунку і має пережити перезавантаження.
struct PersistedConfig
{
    uint16_t signature;
    uint8_t version;

    // Параметри сервоприводу воріт.
    uint8_t servoOpenAngle;
    uint8_t servoClosedAngle;
    uint16_t servoOpenDurationMs;

    // Поріг визначення зайнятості місця ультразвуком.
    uint16_t occupiedThresholdCm;

    // Як часто слати телеметрію в застосунок.
    uint16_t telemetryIntervalMs;

    // Примусові режими воріт.
    uint8_t forceGateOpen;
    uint8_t forceGateLock;

    // Які слоти взагалі увімкнені в системі.
    uint8_t slotEnabledMask;

    // Налаштування тексту на моніторі.
    uint8_t displayForceEnabled;
    char displayForcedText[DISPLAY_TEXT_LENGTH + 1];
    char displayDefaultText[DISPLAY_TEXT_LENGTH + 1];
    char displayAllowedText[DISPLAY_TEXT_LENGTH + 1];
    char displayBlockedText[DISPLAY_TEXT_LENGTH + 1];
    char displayInvalidText[DISPLAY_TEXT_LENGTH + 1];
    char displayLockedText[DISPLAY_TEXT_LENGTH + 1];

    // Дозволені картки.
    uint8_t allowedCount;
    byte allowedCards[MAX_CARD_COUNT][UID_LENGTH];

    // Заблоковані картки.
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

// -----------------------------------------------------------------------------
// Runtime-стан паркомісць
// -----------------------------------------------------------------------------
// slotOccupiedSince - з якого моменту місце зайняте
// slotOccupied      - чи зайняте місце зараз
// slotDistanceCm    - остання виміряна відстань
unsigned long slotOccupiedSince[SLOT_COUNT] = {0};
bool slotOccupied[SLOT_COUNT] = {false};
int16_t slotDistanceCm[SLOT_COUNT] = {0};

// -----------------------------------------------------------------------------
// Буфер прийому Bluetooth-команд
// -----------------------------------------------------------------------------
char rxBuffer[RX_BUFFER_SIZE];
size_t rxIndex = 0;

// -----------------------------------------------------------------------------
// Поточний вміст LCD
// -----------------------------------------------------------------------------
#if ENABLE_LCD
char lcdLine1[17] = "Scan Card";
char lcdLine2[17] = "";
#endif

// -----------------------------------------------------------------------------
// Оголошення функцій
// -----------------------------------------------------------------------------
void setDefaultConfig();
void loadConfig();
void saveConfig();
void applyGateOutput();
void updateGateMode();
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
void handleBluetoothInput();
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
void trimLine(char *line);
bool isSlotEnabled(uint8_t slotIndex);
void setSlotEnabled(uint8_t slotIndex, bool isEnabled);

// -----------------------------------------------------------------------------
// setup
// -----------------------------------------------------------------------------
// Виконується один раз при старті контролера.
void setup()
{
    // Локальний Serial зручний для дебагу через Arduino IDE.
    Serial.begin(9600);

    // Bluetooth-канал для зв'язку із застосунком.
    btSerial.begin(BT_BAUD_RATE);

    // Ініціалізація SPI та RFID.
    SPI.begin();
    rfid.PCD_Init();

    // Ініціалізація LCD.
#if ENABLE_LCD
    lcd.init();
    lcd.backlight();
#endif

    // Підключення сервоприводу воріт.
    gateServo.attach(SERVO_PIN);

    // Налаштовуємо пін-и тільки там, де датчики реально є.
    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        if (trigPins[i] != NO_PIN && echoPins[i] != NO_PIN)
        {
            pinMode(trigPins[i], OUTPUT);
            pinMode(echoPins[i], INPUT);
        }
    }

    // Читаємо конфігурацію з EEPROM.
    // Якщо вона невалідна - ставимо дефолтну.
    loadConfig();

    // Приводимо фізичний стан воріт і логічний стан слотів до конфігу.
    updateGateMode();
    updateParkingStates();
    updateDisplayState();

    // Початкове повідомлення локально на LCD.
    showMessage("Smart Parking", "Controller Ready");

    // Початковий handshake-пакет для застосунку.
    sendHello();
}

// -----------------------------------------------------------------------------
// loop
// -----------------------------------------------------------------------------
// Основний цикл роботи контролера.
void loop()
{
    // Приймаємо та виконуємо команди з Bluetooth.
    handleBluetoothInput();

    // Реагуємо на RFID-картки.
    handleRfid();

    // Оновлюємо логіку воріт.
    updateGateMode();

    // Оновлюємо стани парковок.
    updateParkingStates();
    updateDisplayState();

    // Оновлюємо екран.
    updateLcd();

    // Періодично відправляємо телеметрію в застосунок.

    // Невелика затримка для стабільності.
    delay(20);
}

// -----------------------------------------------------------------------------
// setDefaultConfig
// -----------------------------------------------------------------------------
// Формує стартові значення конфігурації.
// Саме ці значення буде застосовано після RESET або при першому запуску.
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

    config.slotEnabledMask = ALL_SLOTS_ENABLED_MASK;
    config.displayForceEnabled = 0;
    setDisplayText(config.displayForcedText, "FORCED TEXT");
    setDisplayText(config.displayDefaultText, "SMART PARKING");
    setDisplayText(config.displayAllowedText, "ACCESS GRANTED");
    setDisplayText(config.displayBlockedText, "BLOCKED CARD");
    setDisplayText(config.displayInvalidText, "INVALID CARD");
    setDisplayText(config.displayLockedText, "ACCESS LOCKED");

    // Дефолтний список дозволених карток.
    config.allowedCount = 3;
    byte defaultAllowed[3][UID_LENGTH] = {
        {0xB0, 0x41, 0xCE, 0x32},
        {0x83, 0x68, 0x4C, 0xAB},
        {0x33, 0x04, 0x84, 0xAB}};
    memcpy(config.allowedCards, defaultAllowed, sizeof(defaultAllowed));

    // Дефолтний список заблокованих карток.
    config.blockedCount = 2;
    byte defaultBlocked[2][UID_LENGTH] = {
        {0x43, 0x36, 0x54, 0xAB},
        {0xE3, 0x6A, 0x3B, 0xAB}};
    memcpy(config.blockedCards, defaultBlocked, sizeof(defaultBlocked));
}

// -----------------------------------------------------------------------------
// loadConfig
// -----------------------------------------------------------------------------
// Читає конфіг з EEPROM.
// Якщо підпис або версія не збігаються - відновлює дефолт і одразу зберігає.
void loadConfig()
{
    EEPROM.get(EEPROM_ADDRESS, config);

    if (config.signature != CONFIG_SIGNATURE || config.version != CONFIG_VERSION)
    {
        setDefaultConfig();
        saveConfig();
    }
}

// -----------------------------------------------------------------------------
// saveConfig
// -----------------------------------------------------------------------------
// Записує поточну конфігурацію в EEPROM.
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

// -----------------------------------------------------------------------------
// updateGateMode
// -----------------------------------------------------------------------------
// Визначає, який стан воріт має бути активним зараз.
// Пріоритети такі:
// 1. forceGateLock
// 2. forceGateOpen
// 3. temporaryGateOpen
// 4. closed
void updateGateMode()
{
    GateMode previousMode = gateMode;

    if (config.forceGateLock)
    {
        gateMode = GATE_LOCKED;
        temporaryGateOpen = false;
    }
    else if (config.forceGateOpen)
    {
        gateMode = GATE_FORCED_OPEN;
        temporaryGateOpen = false;
    }
    else if (temporaryGateOpen && millis() < temporaryGateExpiresAt)
    {
        gateMode = GATE_TEMPORARY_OPEN;
    }
    else
    {
        temporaryGateOpen = false;
        gateMode = GATE_CLOSED;
    }

    if (previousMode != gateMode)
    {
        applyGateOutput();
    }
}

// -----------------------------------------------------------------------------
// applyGateOutput
// -----------------------------------------------------------------------------
// Фізично переводить ворота у відкрите або закрите положення.
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

// -----------------------------------------------------------------------------
// updateParkingStates
// -----------------------------------------------------------------------------
// Оновлює статус кожного паркомісця.
// Якщо слот вимкнений - він вважається disabled.
// Якщо датчика немає - зараз він просто не може визначати зайнятість.
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

// -----------------------------------------------------------------------------
// updateLcd
// -----------------------------------------------------------------------------
// Оновлює LCD.
// Якщо тимчасове повідомлення вже закінчилося - повертає стандартний idle-екран.
void updateLcd()
{
#if !ENABLE_LCD
    return;
#else
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
#endif
}

// -----------------------------------------------------------------------------
// showMessage
// -----------------------------------------------------------------------------
// Встановлює тимчасовий текст на LCD.
void showMessage(const char *line1, const char *line2)
{
#if !ENABLE_LCD
    (void)line1;
    (void)line2;
    return;
#else
    strncpy(lcdLine1, line1, sizeof(lcdLine1) - 1);
    strncpy(lcdLine2, line2, sizeof(lcdLine2) - 1);
    lcdLine1[sizeof(lcdLine1) - 1] = '\0';
    lcdLine2[sizeof(lcdLine2) - 1] = '\0';
    messageVisibleUntil = millis() + LCD_MESSAGE_DURATION_MS;
#endif
}

// -----------------------------------------------------------------------------
// sendOk / sendError
// -----------------------------------------------------------------------------
// Уніфіковані відповіді на команди застосунку.
void sendOk(const __FlashStringHelper *scope, const __FlashStringHelper *message)
{
    beginProtocolFrame();
    btSerial.print(F("OK|"));
    btSerial.print(scope);
    btSerial.print(F("|"));
    btSerial.print(message);
    endProtocolFrame();
}

void sendError(const __FlashStringHelper *scope, const __FlashStringHelper *message)
{
    beginProtocolFrame();
    btSerial.print(F("ERR|"));
    btSerial.print(scope);
    btSerial.print(F("|"));
    btSerial.print(message);
    endProtocolFrame();
}

void sendOk(const char *scope, const char *message)
{
    beginProtocolFrame();
    btSerial.print(F("OK|"));
    btSerial.print(scope);
    btSerial.print(F("|"));
    btSerial.print(message);
    endProtocolFrame();
}

void sendError(const char *scope, const char *message)
{
    beginProtocolFrame();
    btSerial.print(F("ERR|"));
    btSerial.print(scope);
    btSerial.print(F("|"));
    btSerial.print(message);
    endProtocolFrame();
}

// -----------------------------------------------------------------------------
// sendHello
// -----------------------------------------------------------------------------
// Перший пакет для handshake.
// По суті це відповідь "так, я саме контролер Smart Parking".
void sendHello()
{
    beginProtocolFrame();
    btSerial.print(F("HELLO_OK|device=SMART_PARKING|fw=2|slots=6|transport=HC05"));
    endProtocolFrame();
}

// -----------------------------------------------------------------------------
// sendProfile
// -----------------------------------------------------------------------------
// Короткий профіль того, що саме це за контролер.
void sendProfile()
{
    beginProtocolFrame();
#if ENABLE_LCD
    btSerial.print(F("PROFILE|board=ATmega328P|rfid=MFRC522|lcd=I2C_16X2|gate=SERVO|transport=HC05|slots=6"));
#else
    btSerial.print(F("PROFILE|board=ATmega328P|rfid=MFRC522|lcd=DISABLED|gate=SERVO|transport=HC05|slots=6"));
#endif
    endProtocolFrame();
}

// -----------------------------------------------------------------------------
// sendConfig
// -----------------------------------------------------------------------------
void sendConfig()
{
    beginProtocolFrame();
    btSerial.print(F("CONFIG|open_angle="));
    btSerial.print(config.servoOpenAngle);
    btSerial.print(F("|closed_angle="));
    btSerial.print(config.servoClosedAngle);
    btSerial.print(F("|open_duration_ms="));
    btSerial.print(config.servoOpenDurationMs);
    btSerial.print(F("|threshold_cm="));
    btSerial.print(config.occupiedThresholdCm);
    btSerial.print(F("|telemetry_ms="));
    btSerial.print(config.telemetryIntervalMs);
    btSerial.print(F("|force_open="));
    btSerial.print(config.forceGateOpen);
    btSerial.print(F("|force_lock="));
    btSerial.print(config.forceGateLock);
    endProtocolFrame();

    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        beginProtocolFrame();
        btSerial.print(F("SLOTCFG|"));
        btSerial.print(i + 1);
        btSerial.print(F("|enabled="));
        btSerial.print(isSlotEnabled(i) ? 1 : 0);
        endProtocolFrame();
    }

    beginProtocolFrame();
    btSerial.print(F("DISPLAYCFG|force="));
    btSerial.print(config.displayForceEnabled ? 1 : 0);
    btSerial.print(F("|forced_text="));
    btSerial.print(config.displayForcedText);
    endProtocolFrame();

    sendDisplayTextLine(F("DEFAULT"), config.displayDefaultText);
    sendDisplayTextLine(F("ALLOWED"), config.displayAllowedText);
    sendDisplayTextLine(F("BLOCKED"), config.displayBlockedText);
    sendDisplayTextLine(F("INVALID"), config.displayInvalidText);
    sendDisplayTextLine(F("LOCKED"), config.displayLockedText);

    sendCardsLine(F("CARDS_ALLOWED"), config.allowedCount, config.allowedCards);
    sendCardsLine(F("CARDS_BLOCKED"), config.blockedCount, config.blockedCards);
}

// -----------------------------------------------------------------------------
// sendTelemetry
// -----------------------------------------------------------------------------
void sendTelemetry()
{
    unsigned long remainingMs = 0;
    if (gateMode == GATE_TEMPORARY_OPEN && temporaryGateExpiresAt > millis())
    {
        remainingMs = temporaryGateExpiresAt - millis();
    }

    beginProtocolFrame();
    btSerial.print(F("SNAPSHOT|mode="));
    btSerial.print(getGateModeText());
    btSerial.print(F("|remaining_ms="));
    btSerial.print(remainingMs);
    btSerial.print(F("|locked="));
    btSerial.print(config.forceGateLock);
    btSerial.print(F("|force_open="));
    btSerial.print(config.forceGateOpen);
    btSerial.print(F("|open_angle="));
    btSerial.print(config.servoOpenAngle);
    btSerial.print(F("|closed_angle="));
    btSerial.print(config.servoClosedAngle);
    btSerial.print(F("|open_duration_ms="));
    btSerial.print(config.servoOpenDurationMs);
    btSerial.print(F("|threshold_cm="));
    btSerial.print(config.occupiedThresholdCm);
    btSerial.print(F("|telemetry_ms="));
    btSerial.print(config.telemetryIntervalMs);
    endProtocolFrame();

    beginProtocolFrame();
    btSerial.print(F("DISPLAY|text="));
    btSerial.print(currentDisplayText);
    btSerial.print(F("|forced="));
    btSerial.print(currentDisplayForced ? 1 : 0);
    endProtocolFrame();

    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        writeSlotLine(i);
    }

    beginProtocolFrame();
    btSerial.print(F("COUNTS|allowed="));
    btSerial.print(config.allowedCount);
    btSerial.print(F("|blocked="));
    btSerial.print(config.blockedCount);
    endProtocolFrame();
}

void sendCardsLine(const __FlashStringHelper *prefix, byte count, const byte list[][UID_LENGTH])
{
    beginProtocolFrame();
    btSerial.print(prefix);
    btSerial.print(F("|count="));
    btSerial.print(count);

    for (byte i = 0; i < count; i++)
    {
        btSerial.print(F("|"));
        printUidHex(btSerial, list[i]);
    }

    endProtocolFrame();
}

void sendDisplayTextLine(const __FlashStringHelper *key, const char *value)
{
    beginProtocolFrame();
    btSerial.print(F("DISPLAYTEXT|key="));
    btSerial.print(key);
    btSerial.print(F("|value="));
    btSerial.print(value);
    endProtocolFrame();
}

void writeSlotLine(uint8_t slotIndex)
{
    beginProtocolFrame();
    btSerial.print(F("SLOT|"));
    btSerial.print(slotIndex + 1);
    btSerial.print(F("|state="));
    btSerial.print(getSlotStateText(slotIndex));
    btSerial.print(F("|enabled="));
    btSerial.print(isSlotEnabled(slotIndex) ? 1 : 0);
    btSerial.print(F("|distance_cm="));
    btSerial.print(slotDistanceCm[slotIndex]);
    btSerial.print(F("|occupied_ms="));

    if (slotOccupied[slotIndex] && slotOccupiedSince[slotIndex] != 0)
    {
        btSerial.print(millis() - slotOccupiedSince[slotIndex]);
    }
    else
    {
        btSerial.print(0);
    }
    endProtocolFrame();
}

void beginProtocolFrame()
{
    btSerial.print(PROTOCOL_FRAME_MARKER);
}

void endProtocolFrame()
{
    btSerial.print(PROTOCOL_FRAME_MARKER);
    btSerial.print('\n');
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

// -----------------------------------------------------------------------------
// handleBluetoothInput
// -----------------------------------------------------------------------------
// Приймає символи з Bluetooth і збирає з них рядкові команди.
// Команда вважається завершеною по '\n'.
void handleBluetoothInput()
{
    while (btSerial.available() > 0)
    {
        char incoming = static_cast<char>(btSerial.read());
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
            // Якщо команда надто довга - очищаємо буфер і повідомляємо про помилку.
            rxIndex = 0;
        sendError(F("RX"), F("BUFFER_OVERFLOW"));
        }
    }
}

// -----------------------------------------------------------------------------
// dispatchCommand
// -----------------------------------------------------------------------------
// Розкидає вхідні команди по потрібних обробниках.
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
        btSerial.print(F("RECEIVED|"));
        btSerial.print(originalLine);
        endProtocolFrame();
        sendError(F("CMD"), F("UNKNOWN_COMMAND"));
    }
}

// -----------------------------------------------------------------------------
// handleHelloCommand
// -----------------------------------------------------------------------------
// Підтвердження handshake з правильним токеном.
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

// -----------------------------------------------------------------------------
// handleGetCommand
// -----------------------------------------------------------------------------
// Повертає те, що просить застосунок: snapshot, config або profile.
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

// -----------------------------------------------------------------------------
// handleGateCommand
// -----------------------------------------------------------------------------
// Команди ручного керування воротами.
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

        // Якщо увімкнули force open - lock треба скинути, бо режими конфліктують.
        if (config.forceGateOpen)
        {
            config.forceGateLock = 0;
        }

        updateGateMode();
        sendOk("GATE", "FORCE_OPEN_UPDATED");
    }
    else if (strcmp(action, "OPEN_TEMP") == 0)
    {
        // Тимчасово відкрити не можна, якщо ворота заблоковані.
        if (config.forceGateLock)
        {
            sendError("GATE", "LOCKED");
            return;
        }

        config.forceGateOpen = 0;
        temporaryGateOpen = true;
        temporaryGateExpiresAt = millis() + config.servoOpenDurationMs;
        updateGateMode();
        sendOk("GATE", "TEMP_OPEN_STARTED");
    }
    else if (strcmp(action, "CLOSE") == 0)
    {
        // Примусово закриваємо, скинувши open-стани.
        config.forceGateOpen = 0;
        temporaryGateOpen = false;
        temporaryGateExpiresAt = 0;
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

// -----------------------------------------------------------------------------
// handleConfigCommand
// -----------------------------------------------------------------------------
// Редагування конфігурації контролера.
// SAVE пише в EEPROM.
// RESET повертає дефолт.
// Решта команд змінюють поля в RAM і чекають окремого SAVE.
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
    else
    {
        sendError("CONFIG", "UNKNOWN_FIELD");
        return;
    }
}

// -----------------------------------------------------------------------------
// handleParkingCommand
// -----------------------------------------------------------------------------
// Вмикає або вимикає конкретний логічний слот.
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

// -----------------------------------------------------------------------------
// handleCardsCommand
// -----------------------------------------------------------------------------
// Редагує списки ALLOWED / BLOCKED карток.
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

// -----------------------------------------------------------------------------
// handleRfid
// -----------------------------------------------------------------------------
// Обробляє піднесення RFID-картки.
// Логіка така:
// - якщо картка дозволена і система не locked, відкриваємо ворота
// - якщо картка заблокована, відмовляємо
// - якщо картка невідома, теж відмовляємо
void handleRfid()
{
    if (!rfid.PICC_IsNewCardPresent() || !rfid.PICC_ReadCardSerial())
    {
        return;
    }

    byte uid[UID_LENGTH];
    memcpy(uid, rfid.uid.uidByte, UID_LENGTH);

    beginProtocolFrame();
    btSerial.print(F("EVENT|card="));
    if (compareUid(uid, config.allowedCards, config.allowedCount))
    {
        if (!config.forceGateLock)
        {
            // Якщо force open вже активний - просто залишаємо ворота відкритими.
            // Якщо ні - запускаємо тимчасове відкриття.
            if (!config.forceGateOpen)
            {
                temporaryGateOpen = true;
                temporaryGateExpiresAt = millis() + config.servoOpenDurationMs;
            }

            updateGateMode();
            showMessage("Access Granted", "Gate Open");
            setTransientDisplayText(config.displayAllowedText);
            btSerial.print(F("ALLOWED|uid="));
        }
        else
        {
            showMessage("Access Locked", "Gate Blocked");
            setTransientDisplayText(config.displayLockedText);
            btSerial.print(F("LOCKED|uid="));
        }
    }
    else if (compareUid(uid, config.blockedCards, config.blockedCount))
    {
        showMessage("Blocked Card", "Access Denied");
        setTransientDisplayText(config.displayBlockedText);
        btSerial.print(F("BLOCKED|uid="));
    }
    else
    {
        showMessage("Invalid Card", "Access Denied");
        setTransientDisplayText(config.displayInvalidText);
        btSerial.print(F("INVALID|uid="));
    }

    printUidHex(btSerial, uid);
    endProtocolFrame();

    rfid.PICC_HaltA();
    rfid.PCD_StopCrypto1();
}

// -----------------------------------------------------------------------------
// compareUid
// -----------------------------------------------------------------------------
// Перевіряє, чи є даний UID у списку.
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

// -----------------------------------------------------------------------------
// parseUidHex
// -----------------------------------------------------------------------------
// Перетворює текстовий UID у байтовий.
// Підтримує формати:
// - B041CE32
// - B0 41 CE 32
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

// -----------------------------------------------------------------------------
// findUid
// -----------------------------------------------------------------------------
// Повертає індекс UID у списку або -1, якщо не знайдено.
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

// -----------------------------------------------------------------------------
// addUid
// -----------------------------------------------------------------------------
// Додає UID у список, якщо там ще є місце і такого UID ще не було.
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

// -----------------------------------------------------------------------------
// removeUid
// -----------------------------------------------------------------------------
// Видаляє UID зі списку, якщо він там є.
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

// -----------------------------------------------------------------------------
// printUidHex
// -----------------------------------------------------------------------------
// Виводить UID суцільним HEX-рядком, наприклад B041CE32.
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

// -----------------------------------------------------------------------------
// getGateModeText
// -----------------------------------------------------------------------------
// Текстовий стан воріт для телеметрії.
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

// -----------------------------------------------------------------------------
// getSlotStateText
// -----------------------------------------------------------------------------
// Текстовий стан конкретного паркомісця для телеметрії.
const __FlashStringHelper *getSlotStateText(uint8_t slotIndex)
{
    if (!isSlotEnabled(slotIndex))
    {
        return F("DISABLED");
    }

    return slotOccupied[slotIndex] ? F("OCCUPIED") : F("FREE");
}

// -----------------------------------------------------------------------------
// readDistanceCm
// -----------------------------------------------------------------------------
// Зчитування відстані з HC-SR04.
// Повертає:
// - реальну відстань у см
// - -1, якщо зчитування невалідне
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

// -----------------------------------------------------------------------------
// trimLine
// -----------------------------------------------------------------------------
// Прибирає пробіли з початку та кінця вхідного рядка.
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
