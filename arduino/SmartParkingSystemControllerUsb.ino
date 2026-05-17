#include <EEPROM.h>
#include <Adafruit_NeoPixel.h>
#include <MFRC522.h>
#include <Servo.h>
#include <SPI.h>
#include <ctype.h>
#include <stdlib.h>
#include <string.h>

#include <LiquidCrystal_I2C.h>

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
// 5. Спілкується із застосунком тільки через USB-CDC (Serial). Bluetooth не задіяний.
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
// - Для слотів 4-10 прошивка зараз тримає "логічне" місце без датчика.
// -----------------------------------------------------------------------------

// -----------------------------------------------------------------------------
// Канал зв'язку із застосунком (тільки USB-CDC)
// -----------------------------------------------------------------------------
// Цей варіант прошивки слухає виключно USB-CDC (Serial).
// HC-05 / Serial1 свідомо не задіюється — підходить для дротового тесту
// через Arduino IDE Serial Monitor або через MAUI по USB-кабелю.
#define btSerial Serial

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
LiquidCrystal_I2C lcd(0x27, 16, 2);

// -----------------------------------------------------------------------------
// Servo воріт
// -----------------------------------------------------------------------------
Servo gateServo;

// -----------------------------------------------------------------------------
// Базові системні константи
// -----------------------------------------------------------------------------
constexpr uint8_t SERVO_PIN = 5;
constexpr uint8_t SLOT_COUNT = 10;
constexpr uint8_t MAX_ALLOWED_CARD_COUNT = 20;
constexpr uint8_t MAX_BLOCKED_CARD_COUNT = 20;
constexpr uint8_t UID_LENGTH = 4;
constexpr uint8_t DISPLAY_TEXT_LENGTH = 16;
constexpr uint8_t NO_PIN = 255;
constexpr long BT_BAUD_RATE = 9600;
constexpr uint16_t CONFIG_SIGNATURE = 0x5350;
constexpr uint8_t CONFIG_VERSION = 9;
constexpr unsigned long LCD_MESSAGE_DURATION_MS = 3000UL;
constexpr uint16_t EEPROM_ADDRESS = 0;
constexpr size_t RX_BUFFER_SIZE = 80;
constexpr uint16_t DEFAULT_SLOT_ENABLED_MASK = 0x0007;
constexpr char PROTOCOL_FRAME_MARKER[] = "|||";
constexpr uint8_t PROTOCOL_FRAME_MARKER_LENGTH = 3;
constexpr unsigned long GATE_PASSAGE_AUTO_EXIT_COOLDOWN_MS = 3000UL;
constexpr uint8_t GATE_PASSAGE_STABILITY_READS = 2;
constexpr uint8_t SLOT_STABILITY_READS = 2;
constexpr uint8_t ROUTE_LED_SLOT_COUNT = 3;
constexpr uint8_t ROUTE_LED_COUNT = 15;
constexpr uint8_t ROUTE_LED_BRIGHTNESS = 80;
constexpr uint8_t ROUTE_LED_PIN = 22;
constexpr uint8_t ROUTE_LED_EXTRA_ROUTE_START = 10;
constexpr uint8_t DISABLED_ROUTE_LED_COUNT = 7;

// -----------------------------------------------------------------------------
// Піни паркомісць
// -----------------------------------------------------------------------------
// Для слотів 1-3 реально задані ультразвукові датчики.
// Для слотів 4-10 стоїть NO_PIN, тобто датчик відсутній.
// Це дозволяє вже зараз тримати 10 логічних місць у протоколі застосунку.
const uint8_t trigPins[SLOT_COUNT] = {7, 4, A0, NO_PIN, NO_PIN, NO_PIN, NO_PIN, NO_PIN, NO_PIN, NO_PIN};
const uint8_t echoPins[SLOT_COUNT] = {8, 6, A1, NO_PIN, NO_PIN, NO_PIN, NO_PIN, NO_PIN, NO_PIN, NO_PIN};
constexpr uint8_t GATE_PASSAGE_TRIG_PIN = A2;
constexpr uint8_t GATE_PASSAGE_ECHO_PIN = A3;
constexpr uint8_t FRONT_ACCESS_TRIG_PIN = 25;
constexpr uint8_t FRONT_ACCESS_ECHO_PIN = 26;

// Single addressable route strip on D22.
// Physical parking LEDs go 1..10 around the layout; logical slots map to LEDs 1, 5 and 8.
const uint8_t routeLedSlotIndexes[ROUTE_LED_SLOT_COUNT] = {0, 4, 7};
const uint8_t routeLedExtraEndIndexes[ROUTE_LED_SLOT_COUNT] = {11, 13, 14};
const uint8_t disabledRouteLedIndexes[DISABLED_ROUTE_LED_COUNT] = {1, 2, 3, 5, 6, 8, 9};
Adafruit_NeoPixel routeLedStrip(ROUTE_LED_COUNT, ROUTE_LED_PIN, NEO_GRB + NEO_KHZ800);

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
    uint8_t autoExitOpenEnabled;
    uint8_t autoCloseAfterPassEnabled;
    uint16_t gatePassageThresholdCm;

    // Які слоти взагалі увімкнені в системі.
    uint16_t slotEnabledMask;

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
    byte allowedCards[MAX_ALLOWED_CARD_COUNT][UID_LENGTH];

    // Заблоковані картки.
    uint8_t blockedCount;
    byte blockedCards[MAX_BLOCKED_CARD_COUNT][UID_LENGTH];
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
bool frontAccessOccupied = false;
bool previousFrontAccessOccupied = false;
bool pendingFrontAccessOccupied = false;
uint8_t frontAccessStableReads = 0;
int16_t frontAccessDistanceCm = -1;
uint16_t frontAccessCounter = 0;
unsigned long lastAutoExitOpenAt = 0;

// -----------------------------------------------------------------------------
// Runtime-стан паркомісць
// -----------------------------------------------------------------------------
// slotOccupiedSince - з якого моменту місце зайняте
// slotOccupied      - чи зайняте місце зараз
// slotDistanceCm    - остання виміряна відстань
unsigned long slotOccupiedSince[SLOT_COUNT] = {0};
bool slotOccupied[SLOT_COUNT] = {false};
bool pendingSlotOccupied[SLOT_COUNT] = {false};
uint8_t slotStableReads[SLOT_COUNT] = {0};
int16_t slotDistanceCm[SLOT_COUNT] = {0};
uint8_t activeRouteSlot = 0;
char lastAccessUid[UID_LENGTH * 2 + 1] = "";
char lastAccessResult[9] = "";
uint16_t lastAccessCounter = 0;

// -----------------------------------------------------------------------------
// Буфер прийому Bluetooth-команд
// -----------------------------------------------------------------------------
char rxBuffer[RX_BUFFER_SIZE];
size_t rxIndex = 0;

// -----------------------------------------------------------------------------
// Поточний вміст LCD
// -----------------------------------------------------------------------------
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
void updateFrontAccessSensorState();
void updateGatePassageState();
void updateGatePassageAutomation();
void updateParkingStates();
void setupRouteLedStrips();
void clearRouteLedStrips();
void refreshRouteLedStrips();
void renderDisabledRouteLeds();
void renderActiveRouteSlotLeds();
void renderActiveRouteSlotLed(uint8_t slotIndex, uint32_t color);
void showRouteToSlot(uint8_t slotIndex);
void updateLcd();
void setAvailabilityDisplayLine2();
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
bool addUid(byte list[][UID_LENGTH], byte &count, const byte uid[UID_LENGTH], byte capacity);
bool removeUid(byte list[][UID_LENGTH], byte &count, const byte uid[UID_LENGTH]);
void printUidHex(Stream &stream, const byte uid[UID_LENGTH]);
const __FlashStringHelper *getGateModeText();
const __FlashStringHelper *getSlotStateText(uint8_t slotIndex);
void writeSlotLine(uint8_t slotIndex);
long readDistanceCm(uint8_t trigPin, uint8_t echoPin);
void startTemporaryGateOpen(bool armAutoClose);
void resetGatePassageAutomation();
void setLastAccessEvent(const byte uid[UID_LENGTH], const char *result);
void trimLine(char *line);
bool isSlotEnabled(uint8_t slotIndex);
void setSlotEnabled(uint8_t slotIndex, bool isEnabled);

// -----------------------------------------------------------------------------
// setup
// -----------------------------------------------------------------------------
// Виконується один раз при старті контролера.
void setup()
{
    // Тільки USB-CDC канал. HC-05 / Serial1 не задіяний.
    btSerial.begin(BT_BAUD_RATE);

    // Ініціалізація SPI та RFID.
    SPI.begin();
    rfid.PCD_Init();

    // Ініціалізація LCD.
    lcd.init();
    lcd.backlight();

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

    pinMode(GATE_PASSAGE_TRIG_PIN, OUTPUT);
    pinMode(GATE_PASSAGE_ECHO_PIN, INPUT);
    pinMode(FRONT_ACCESS_TRIG_PIN, OUTPUT);
    pinMode(FRONT_ACCESS_ECHO_PIN, INPUT);
    setupRouteLedStrips();

    // Читаємо конфігурацію з EEPROM.
    // Якщо вона невалідна - ставимо дефолтну.
    loadConfig();

    // Приводимо фізичний стан воріт і логічний стан слотів до конфігу.
    updateFrontAccessSensorState();
    updateGatePassageState();
    updateGateMode();
    applyGateOutput();
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

    updateFrontAccessSensorState();
    updateGatePassageState();
    updateGatePassageAutomation();

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
    config.servoClosedAngle = 180;
    config.servoOpenDurationMs = 8000;
    config.occupiedThresholdCm = 10;
    config.telemetryIntervalMs = 500;
    config.forceGateOpen = 0;
    config.forceGateLock = 0;
    config.autoExitOpenEnabled = 1;
    config.autoCloseAfterPassEnabled = 1;
    config.gatePassageThresholdCm = 10;

    config.slotEnabledMask = DEFAULT_SLOT_ENABLED_MASK;
    config.displayForceEnabled = 0;
    setDisplayText(config.displayForcedText, "Forced Text");
    setDisplayText(config.displayDefaultText, "Smart Parking");
    setDisplayText(config.displayAllowedText, "Access Granted");
    setDisplayText(config.displayBlockedText, "Blocked Card");
    setDisplayText(config.displayInvalidText, "Invalid Card");
    setDisplayText(config.displayLockedText, "Access Locked");

    // Дефолтний список дозволених карток.
    config.allowedCount = 3;
    byte defaultAllowed[3][UID_LENGTH] = {
        {0x42, 0xA0, 0x74, 0x06},
        {0x8A, 0x31, 0x08, 0x35},
        {0xFC, 0x64, 0x74, 0x06}};
    memcpy(config.allowedCards, defaultAllowed, sizeof(defaultAllowed));

    // Дефолтний список заблокованих карток.
    config.blockedCount = 1;
    byte defaultBlocked[1][UID_LENGTH] = {
        {0x25, 0x9F, 0x15, 0xE0}};
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

    if (millis() <= messageVisibleUntil && lcdLine1[0] != '\0')
    {
        setDisplayText(currentDisplayText, lcdLine1);
        return;
    }

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

void updateFrontAccessSensorState()
{
    previousFrontAccessOccupied = frontAccessOccupied;

    long distance = readDistanceCm(FRONT_ACCESS_TRIG_PIN, FRONT_ACCESS_ECHO_PIN);
    frontAccessDistanceCm = distance;
    bool measuredOccupied = distance != -1 && distance < config.gatePassageThresholdCm;

    if (measuredOccupied == pendingFrontAccessOccupied)
    {
        if (frontAccessStableReads < GATE_PASSAGE_STABILITY_READS)
        {
            frontAccessStableReads++;
        }
    }
    else
    {
        pendingFrontAccessOccupied = measuredOccupied;
        frontAccessStableReads = 1;
    }

    if (frontAccessStableReads >= GATE_PASSAGE_STABILITY_READS)
    {
        frontAccessOccupied = pendingFrontAccessOccupied;
    }

    if (frontAccessOccupied && !previousFrontAccessOccupied)
    {
        frontAccessCounter++;
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

void setLastAccessEvent(const byte uid[UID_LENGTH], const char *result)
{
    for (uint8_t i = 0; i < UID_LENGTH; i++)
    {
        sprintf(&lastAccessUid[i * 2], "%02X", uid[i]);
    }

    lastAccessUid[UID_LENGTH * 2] = '\0';
    strncpy(lastAccessResult, result, sizeof(lastAccessResult) - 1);
    lastAccessResult[sizeof(lastAccessResult) - 1] = '\0';
    lastAccessCounter++;
}

// -----------------------------------------------------------------------------
// updateParkingStates
// -----------------------------------------------------------------------------
// Оновлює статус кожного паркомісця.
// Якщо слот вимкнений - він вважається disabled.
// Якщо датчика немає - зараз він просто не може визначати зайнятість.
void updateParkingStates()
{
    bool routeStateChanged = false;
    bool routeShouldClear = false;

    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        if (!isSlotEnabled(i))
        {
            routeStateChanged = routeStateChanged || slotOccupied[i];
            slotDistanceCm[i] = -1;
            slotOccupied[i] = false;
            pendingSlotOccupied[i] = false;
            slotStableReads[i] = 0;
            slotOccupiedSince[i] = 0;
            continue;
        }

        if (trigPins[i] == NO_PIN || echoPins[i] == NO_PIN)
        {
            routeStateChanged = routeStateChanged || slotOccupied[i];
            slotDistanceCm[i] = -1;
            slotOccupied[i] = false;
            pendingSlotOccupied[i] = false;
            slotStableReads[i] = 0;
            slotOccupiedSince[i] = 0;
            continue;
        }

        long distance = readDistanceCm(trigPins[i], echoPins[i]);
        slotDistanceCm[i] = distance;
        if (distance == -1)
        {
            continue;
        }

        bool measuredOccupied = distance < config.occupiedThresholdCm;
        if (measuredOccupied == pendingSlotOccupied[i])
        {
            if (slotStableReads[i] < SLOT_STABILITY_READS)
            {
                slotStableReads[i]++;
            }
        }
        else
        {
            pendingSlotOccupied[i] = measuredOccupied;
            slotStableReads[i] = 1;
        }

        if (slotStableReads[i] < SLOT_STABILITY_READS)
        {
            continue;
        }

        if (measuredOccupied && !slotOccupied[i])
        {
            slotOccupiedSince[i] = millis();
        }

        if (!measuredOccupied)
        {
            slotOccupiedSince[i] = 0;
        }

        if (slotOccupied[i] != measuredOccupied)
        {
            routeStateChanged = true;
            slotOccupied[i] = measuredOccupied;
            routeShouldClear = routeShouldClear || (measuredOccupied && i < ROUTE_LED_SLOT_COUNT && activeRouteSlot != 0);
        }
    }

    if (routeShouldClear)
    {
        clearRouteLedStrips();
    }
    else if (routeStateChanged)
    {
        refreshRouteLedStrips();
    }
}

void setupRouteLedStrips()
{
    routeLedStrip.begin();
    routeLedStrip.setBrightness(ROUTE_LED_BRIGHTNESS);
    routeLedStrip.clear();
    renderDisabledRouteLeds();
    renderActiveRouteSlotLeds();
    routeLedStrip.show();
}

void clearRouteLedStrips()
{
    activeRouteSlot = 0;

    routeLedStrip.clear();
    renderDisabledRouteLeds();
    renderActiveRouteSlotLeds();
    routeLedStrip.show();
}

void refreshRouteLedStrips()
{
    if (activeRouteSlot >= 1 && activeRouteSlot <= ROUTE_LED_SLOT_COUNT)
    {
        showRouteToSlot(activeRouteSlot - 1);
        return;
    }

    clearRouteLedStrips();
}

void renderDisabledRouteLeds()
{
    uint32_t disabledColor = routeLedStrip.Color(180, 0, 0);
    for (uint8_t i = 0; i < DISABLED_ROUTE_LED_COUNT; i++)
    {
        routeLedStrip.setPixelColor(disabledRouteLedIndexes[i], disabledColor);
    }
}

void renderActiveRouteSlotLeds()
{
    uint32_t freeColor = routeLedStrip.Color(255, 90, 0);
    uint32_t occupiedColor = routeLedStrip.Color(0, 80, 200);
    for (uint8_t slotIndex = 0; slotIndex < ROUTE_LED_SLOT_COUNT; slotIndex++)
    {
        renderActiveRouteSlotLed(slotIndex, slotOccupied[slotIndex] ? occupiedColor : freeColor);
    }
}

void renderActiveRouteSlotLed(uint8_t slotIndex, uint32_t color)
{
    if (slotIndex < ROUTE_LED_SLOT_COUNT)
    {
        routeLedStrip.setPixelColor(routeLedSlotIndexes[slotIndex], color);
    }
}

void showRouteToSlot(uint8_t slotIndex)
{
    if (slotIndex >= ROUTE_LED_SLOT_COUNT)
    {
        clearRouteLedStrips();
        return;
    }

    activeRouteSlot = slotIndex + 1;
    uint32_t routeColor = routeLedStrip.Color(0, 180, 80);
    uint8_t routeEndIndex = routeLedExtraEndIndexes[slotIndex];

    routeLedStrip.clear();
    for (uint8_t ledIndex = ROUTE_LED_EXTRA_ROUTE_START; ledIndex <= routeEndIndex; ledIndex++)
    {
        routeLedStrip.setPixelColor(ledIndex, routeColor);
    }

    renderDisabledRouteLeds();
    renderActiveRouteSlotLeds();
    renderActiveRouteSlotLed(slotIndex, routeColor);
    routeLedStrip.show();
}

// -----------------------------------------------------------------------------
// updateLcd
// -----------------------------------------------------------------------------
// Оновлює LCD.
// Якщо тимчасове повідомлення вже закінчилося - повертає стандартний idle-екран.
void updateLcd()
{
    if (config.displayForceEnabled)
    {
        setDisplayText(lcdLine1, config.displayForcedText);
        setAvailabilityDisplayLine2();
    }
    else if (millis() <= messageVisibleUntil)
    {
        // Keep the explicit two-line message set by showMessage().
    }
    else if (transientDisplayUntil > millis() && transientDisplayText[0] != '\0')
    {
        setDisplayText(lcdLine1, transientDisplayText);
        setAvailabilityDisplayLine2();
    }
    else
    {
        setDisplayText(lcdLine1, config.displayDefaultText);
        setAvailabilityDisplayLine2();
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

void setAvailabilityDisplayLine2()
{
    uint8_t freeCount = 0;
    uint8_t enabledCount = 0;
    for (uint8_t i = 0; i < SLOT_COUNT; i++)
    {
        if (isSlotEnabled(i))
        {
            enabledCount++;
            if (!slotOccupied[i])
            {
                freeCount++;
            }
        }
    }

    snprintf(lcdLine2, sizeof(lcdLine2), "Free: %u / %u", freeCount, enabledCount);
}

// -----------------------------------------------------------------------------
// showMessage
// -----------------------------------------------------------------------------
// Встановлює тимчасовий текст на LCD.
void showMessage(const char *line1, const char *line2)
{
    strncpy(lcdLine1, line1, sizeof(lcdLine1) - 1);
    strncpy(lcdLine2, line2, sizeof(lcdLine2) - 1);
    lcdLine1[sizeof(lcdLine1) - 1] = '\0';
    lcdLine2[sizeof(lcdLine2) - 1] = '\0';
    messageVisibleUntil = millis() + LCD_MESSAGE_DURATION_MS;
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
    btSerial.print(F("HELLO_OK|device=SMART_PARKING|fw=2|slots="));
    btSerial.print(SLOT_COUNT);
    btSerial.print(F("|transport=HC05+USB"));
    endProtocolFrame();
}

// -----------------------------------------------------------------------------
// sendProfile
// -----------------------------------------------------------------------------
// Короткий профіль того, що саме це за контролер.
void sendProfile()
{
    beginProtocolFrame();
    btSerial.print(F("PROFILE|board=ArduinoMega|rfid=MFRC522|lcd=I2C_16X2|gate=SERVO|transport=HC05+USB|slots="));
    btSerial.print(SLOT_COUNT);
    btSerial.print(F("|route_led_strips=1|route_leds=15|front_sensor=1"));
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
    btSerial.print(F("|auto_exit_open="));
    btSerial.print(config.autoExitOpenEnabled);
    btSerial.print(F("|auto_close_after_pass="));
    btSerial.print(config.autoCloseAfterPassEnabled);
    btSerial.print(F("|passage_threshold_cm="));
    btSerial.print(config.gatePassageThresholdCm);
    btSerial.print(F("|route_slot="));
    btSerial.print(activeRouteSlot);
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
    btSerial.print(F("|passage_occupied="));
    btSerial.print(gatePassageOccupied ? 1 : 0);
    btSerial.print(F("|passage_distance_cm="));
    btSerial.print(gatePassageDistanceCm);
    btSerial.print(F("|front_occupied="));
    btSerial.print(frontAccessOccupied ? 1 : 0);
    btSerial.print(F("|front_distance_cm="));
    btSerial.print(frontAccessDistanceCm);
    btSerial.print(F("|front_counter="));
    btSerial.print(frontAccessCounter);
    btSerial.print(F("|last_access_uid="));
    btSerial.print(lastAccessUid);
    btSerial.print(F("|last_access_result="));
    btSerial.print(lastAccessResult);
    btSerial.print(F("|last_access_counter="));
    btSerial.print(lastAccessCounter);
    endProtocolFrame();

    beginProtocolFrame();
    btSerial.print(F("DISPLAY|text="));
    btSerial.print(currentDisplayText);
    btSerial.print(F("|line2="));
    btSerial.print(lcdLine2);
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
    delay(15);
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
// Приймає символи з USB-CDC (Serial) і збирає з них рядкові команди.
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

    sendError(F("HELLO"), F("INVALID_TOKEN"));
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
        sendError(F("GET"), F("MISSING_TARGET"));
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
        sendError(F("GET"), F("UNKNOWN_TARGET"));
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
        sendError(F("GATE"), F("MISSING_ACTION"));
        return;
    }

    if (strcmp(action, "FORCE_OPEN") == 0)
    {
        char *value = strtok_r(nullptr, " ", &context);
        if (value == nullptr)
        {
            sendError(F("GATE"), F("MISSING_FORCE_OPEN_VALUE"));
            return;
        }

        config.forceGateOpen = strcmp(value, "ON") == 0 ? 1 : 0;

        // Якщо увімкнули force open - lock треба скинути, бо режими конфліктують.
        if (config.forceGateOpen)
        {
            config.forceGateLock = 0;
        }

        resetGatePassageAutomation();
        updateGateMode();
        sendOk(F("GATE"), F("FORCE_OPEN_UPDATED"));
    }
    else if (strcmp(action, "OPEN_TEMP") == 0)
    {
        // Тимчасово відкрити не можна, якщо ворота заблоковані.
        if (config.forceGateLock)
        {
            sendError(F("GATE"), F("LOCKED"));
            return;
        }

        startTemporaryGateOpen(true);
        updateGateMode();
        sendOk(F("GATE"), F("TEMP_OPEN_STARTED"));
    }
    else if (strcmp(action, "CLOSE") == 0)
    {
        // Примусово закриваємо, скинувши open-стани.
        config.forceGateOpen = 0;
        temporaryGateOpen = false;
        temporaryGateExpiresAt = 0;
        resetGatePassageAutomation();
        updateGateMode();
        sendOk(F("GATE"), F("CLOSED"));
    }
    else if (strcmp(action, "LOCK") == 0)
    {
        char *value = strtok_r(nullptr, " ", &context);
        if (value == nullptr)
        {
            sendError(F("GATE"), F("MISSING_LOCK_VALUE"));
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
            sendError(F("GATE"), F("INVALID_LOCK_VALUE"));
            return;
        }

        updateGateMode();
        sendOk(F("GATE"), F("LOCK_UPDATED"));
    }
    else
    {
        sendError(F("GATE"), F("UNKNOWN_ACTION"));
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
        sendError(F("CONFIG"), F("MISSING_ACTION"));
        return;
    }

    if (strcmp(action, "SAVE") == 0)
    {
        saveConfig();
        sendOk(F("CONFIG"), F("SAVED"));
        return;
    }

    if (strcmp(action, "RESET") == 0)
    {
        setDefaultConfig();
        saveConfig();
        updateGateMode();
        updateParkingStates();
        updateDisplayState();
        sendOk(F("CONFIG"), F("RESET_TO_DEFAULTS"));
        return;
    }

    char *value = strtok_r(nullptr, " ", &context);
    if (value == nullptr)
    {
        sendError(F("CONFIG"), F("MISSING_VALUE"));
        return;
    }

    long parsedValue = atol(value);

    if (strcmp(action, "OPEN_ANGLE") == 0)
    {
        config.servoOpenAngle = constrain(parsedValue, 0, 180);
        updateGateMode();
        sendOk(F("CONFIG"), F("OPEN_ANGLE_UPDATED"));
    }
    else if (strcmp(action, "CLOSED_ANGLE") == 0)
    {
        config.servoClosedAngle = constrain(parsedValue, 0, 180);
        updateGateMode();
        sendOk(F("CONFIG"), F("CLOSED_ANGLE_UPDATED"));
    }
    else if (strcmp(action, "OPEN_DURATION_MS") == 0)
    {
        config.servoOpenDurationMs = max(250L, parsedValue);
        updateGateMode();
        sendOk(F("CONFIG"), F("OPEN_DURATION_UPDATED"));
    }
    else if (strcmp(action, "THRESHOLD_CM") == 0)
    {
        config.occupiedThresholdCm = max(1L, parsedValue);
        updateGateMode();
        sendOk(F("CONFIG"), F("THRESHOLD_UPDATED"));
    }
    else if (strcmp(action, "TELEMETRY_MS") == 0)
    {
        config.telemetryIntervalMs = max(250L, parsedValue);
        updateGateMode();
        sendOk(F("CONFIG"), F("TELEMETRY_UPDATED"));
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
            sendError(F("CONFIG"), F("INVALID_AUTO_EXIT_OPEN_VALUE"));
            return;
        }

        sendOk(F("CONFIG"), F("AUTO_EXIT_OPEN_UPDATED"));
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
            sendError(F("CONFIG"), F("INVALID_AUTO_CLOSE_AFTER_PASS_VALUE"));
            return;
        }

        sendOk(F("CONFIG"), F("AUTO_CLOSE_AFTER_PASS_UPDATED"));
    }
    else if (strcmp(action, "PASSAGE_THRESHOLD_CM") == 0)
    {
        config.gatePassageThresholdCm = max(1L, parsedValue);
        updateGatePassageState();
        sendOk(F("CONFIG"), F("PASSAGE_THRESHOLD_UPDATED"));
    }
    else
    {
        sendError(F("CONFIG"), F("UNKNOWN_FIELD"));
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
    if (action == nullptr)
    {
        sendError(F("PARKING"), F("INVALID_COMMAND"));
        return;
    }

    if (strcmp(action, "ROUTE_CLEAR") == 0)
    {
        clearRouteLedStrips();
        sendOk(F("PARKING"), F("ROUTE_CLEARED"));
        return;
    }

    if (slotToken == nullptr)
    {
        sendError(F("PARKING"), F("INVALID_COMMAND"));
        return;
    }

    int slotNumber = atoi(slotToken);
    if (slotNumber < 1 || slotNumber > SLOT_COUNT)
    {
        sendError(F("PARKING"), F("INVALID_SLOT"));
        return;
    }

    uint8_t index = static_cast<uint8_t>(slotNumber - 1);
    if (strcmp(action, "ENABLE") == 0)
    {
        setSlotEnabled(index, true);
        sendOk(F("PARKING"), F("SLOT_ENABLED"));
    }
    else if (strcmp(action, "DISABLE") == 0)
    {
        setSlotEnabled(index, false);
        sendOk(F("PARKING"), F("SLOT_DISABLED"));
    }
    else if (strcmp(action, "ROUTE") == 0)
    {
        if (index >= ROUTE_LED_SLOT_COUNT)
        {
            sendError(F("PARKING"), F("ROUTE_SLOT_HAS_NO_STRIP"));
            return;
        }

        showRouteToSlot(index);
        sendOk(F("PARKING"), F("ROUTE_ENABLED"));
    }
    else
    {
        sendError(F("PARKING"), F("UNKNOWN_ACTION"));
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
        sendError(F("CARDS"), F("INVALID_COMMAND"));
        return;
    }

    byte (*targetList)[UID_LENGTH] = nullptr;
    byte *targetCount = nullptr;
    byte targetCapacity = 0;

    if (strcmp(listType, "ALLOWED") == 0)
    {
        targetList = config.allowedCards;
        targetCount = &config.allowedCount;
        targetCapacity = MAX_ALLOWED_CARD_COUNT;
    }
    else if (strcmp(listType, "BLOCKED") == 0)
    {
        targetList = config.blockedCards;
        targetCount = &config.blockedCount;
        targetCapacity = MAX_BLOCKED_CARD_COUNT;
    }
    else
    {
        sendError(F("CARDS"), F("UNKNOWN_LIST"));
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
        sendError(F("CARDS"), F("MISSING_UID"));
        return;
    }

    byte uid[UID_LENGTH];
    if (!parseUidHex(uidToken, uid))
    {
        sendError(F("CARDS"), F("INVALID_UID"));
        return;
    }

    if (strcmp(action, "ADD") == 0)
    {
        if (addUid(targetList, *targetCount, uid, targetCapacity))
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
        sendError(F("CARDS"), F("UNKNOWN_ACTION"));
    }
}

void handleDisplayCommand(char *context)
{
    char *action = strtok_r(nullptr, " ", &context);
    if (action == nullptr)
    {
        sendError(F("DISPLAY"), F("MISSING_ACTION"));
        return;
    }

    if (strcmp(action, "FORCE") == 0)
    {
        char *value = strtok_r(nullptr, " ", &context);
        if (value == nullptr)
        {
            sendError(F("DISPLAY"), F("MISSING_FORCE_VALUE"));
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
            sendError(F("DISPLAY"), F("INVALID_FORCE_VALUE"));
            return;
        }

        updateDisplayState();
        sendOk(F("DISPLAY"), F("FORCE_UPDATED"));
        return;
    }

    if (strcmp(action, "SHOW") == 0)
    {
        if (context == nullptr)
        {
            sendError(F("DISPLAY"), F("MISSING_SHOW_TEXT"));
            return;
        }

        trimLine(context);
        if (context[0] == '\0')
        {
            sendError(F("DISPLAY"), F("MISSING_SHOW_TEXT"));
            return;
        }

        setTransientDisplayText(context);
        sendOk(F("DISPLAY"), F("SHOWN"));
        return;
    }

    if (strcmp(action, "TEXT") == 0)
    {
        char *key = strtok_r(nullptr, " ", &context);
        if (key == nullptr)
        {
            sendError(F("DISPLAY"), F("MISSING_TEXT_KEY"));
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
            sendError(F("DISPLAY"), F("UNKNOWN_TEXT_KEY"));
            return;
        }

        updateDisplayState();
        if (strcmp(key, "FORCED") == 0)
        {
            sendOk(F("DISPLAY"), F("TEXT_FORCED_UPDATED"));
        }
        else if (strcmp(key, "DEFAULT") == 0)
        {
            sendOk(F("DISPLAY"), F("TEXT_DEFAULT_UPDATED"));
        }
        else if (strcmp(key, "ALLOWED") == 0)
        {
            sendOk(F("DISPLAY"), F("TEXT_ALLOWED_UPDATED"));
        }
        else if (strcmp(key, "BLOCKED") == 0)
        {
            sendOk(F("DISPLAY"), F("TEXT_BLOCKED_UPDATED"));
        }
        else if (strcmp(key, "INVALID") == 0)
        {
            sendOk(F("DISPLAY"), F("TEXT_INVALID_UPDATED"));
        }
        else
        {
            sendOk(F("DISPLAY"), F("TEXT_LOCKED_UPDATED"));
        }
        return;
    }

    sendError(F("DISPLAY"), F("UNKNOWN_ACTION"));
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
                startTemporaryGateOpen(true);
            }

            updateGateMode();
            setTransientDisplayText(config.displayAllowedText);
            showMessage(config.displayAllowedText, "Gate Open");
            btSerial.print(F("ALLOWED|uid="));
            setLastAccessEvent(uid, "ALLOWED");
        }
        else
        {
            setTransientDisplayText(config.displayLockedText);
            showMessage(config.displayLockedText, "Gate Blocked");
            btSerial.print(F("LOCKED|uid="));
            setLastAccessEvent(uid, "LOCKED");
        }
    }
    else if (compareUid(uid, config.blockedCards, config.blockedCount))
    {
        setTransientDisplayText(config.displayBlockedText);
        showMessage(config.displayBlockedText, "Access Denied");
        btSerial.print(F("BLOCKED|uid="));
        setLastAccessEvent(uid, "BLOCKED");
    }
    else
    {
        setTransientDisplayText(config.displayInvalidText);
        showMessage(config.displayInvalidText, "Access Denied");
        btSerial.print(F("INVALID|uid="));
        setLastAccessEvent(uid, "INVALID");
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
bool addUid(byte list[][UID_LENGTH], byte &count, const byte uid[UID_LENGTH], byte capacity)
{
    if (count >= capacity || findUid(uid, list, count) != -1)
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
        && trigPins[slotIndex] != NO_PIN
        && echoPins[slotIndex] != NO_PIN
        && (config.slotEnabledMask & (static_cast<uint16_t>(1) << slotIndex)) != 0;
}

void setSlotEnabled(uint8_t slotIndex, bool isEnabled)
{
    if (slotIndex >= SLOT_COUNT)
    {
        return;
    }

    if (isEnabled)
    {
        config.slotEnabledMask |= (static_cast<uint16_t>(1) << slotIndex);
    }
    else
    {
        config.slotEnabledMask &= static_cast<uint16_t>(~(static_cast<uint16_t>(1) << slotIndex));
    }
}
