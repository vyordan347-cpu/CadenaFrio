// Cadena de Frío - Firmware de prueba para ESP32
// Lee la temperatura y la envía a la API cada cierto tiempo (HU01).
//
// Antes de compilar: copie "credenciales_ejemplo.h" como "credenciales.h"
// y complete los datos de su red WiFi y de la API.

#include <WiFi.h>
#include <HTTPClient.h>
#include "DHT.h"
#include "credenciales.h"

// --- Sensor ---
// Pin seguro GPIO 13 (no interfiere con el arranque ni con la cámara del ESP32-CAM).
#define DHTPIN 13
#define DHTTYPE DHT11
DHT dht(DHTPIN, DHTTYPE);

// --- Envío ---
// En el sistema real se envía una lectura por minuto (RNF02).
// Para las pruebas se usan 10 segundos y así se ve el cambio más rápido en la web.
const unsigned long INTERVALO_ENVIO_MS = 10000;
unsigned long ultimoEnvio = 0;

void conectarWiFi() {
  if (WiFi.status() == WL_CONNECTED) {
    return;
  }

  Serial.print("Conectando a WiFi \"");
  Serial.print(WIFI_NOMBRE);
  Serial.print("\"");

  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_NOMBRE, WIFI_CLAVE);

  // Espera hasta 15 segundos; si no conecta, se reintentará en el siguiente ciclo.
  unsigned long inicio = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - inicio < 15000) {
    delay(500);
    Serial.print(".");
  }

  if (WiFi.status() == WL_CONNECTED) {
    Serial.print("\nConectado. IP del ESP32: ");
    Serial.println(WiFi.localIP());
  } else {
    Serial.println("\nNo se pudo conectar al WiFi. Se reintentará.");
  }
}

void enviarLectura(float temperatura) {
  HTTPClient http;
  http.setTimeout(10000);
  http.begin(URL_API);

  // Encabezados que espera la API: tipo de contenido y clave del dispositivo (RNF06).
  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Clave-Dispositivo", CLAVE_DISPOSITIVO);

  // Primera prueba: sin "fechaMedicion"; la API usa su propia hora.
  String cuerpo = "{\"temperatura\":" + String(temperatura, 2) + "}";

  int codigo = http.POST(cuerpo);

  Serial.print("Enviado ");
  Serial.print(cuerpo);
  Serial.print(" -> respuesta ");
  Serial.println(codigo);

  if (codigo == 200) {
    Serial.println("Lectura guardada correctamente.");
  } else if (codigo == 401) {
    Serial.println("Error 401: la clave del dispositivo no es válida.");
  } else if (codigo == 400) {
    Serial.println("Error 400: la API rechazó el dato. Detalle: " + http.getString());
  } else if (codigo < 0) {
    // Códigos negativos: no hubo comunicación con el servidor.
    Serial.println("Sin conexión con la API: " + http.errorToString(codigo));
    Serial.println("Revise la IP, el puerto, el firewall y que estén en la misma red.");
  }

  http.end();
}

void setup() {
  Serial.begin(115200);
  delay(500); // Pausa de estabilización eléctrica

  Serial.println("--- Cadena de Frío: ESP32 + DHT11 ---");
  dht.begin();
  conectarWiFi();
}

void loop() {
  // millis() en lugar de delay(): el programa no se queda bloqueado esperando.
  if (millis() - ultimoEnvio < INTERVALO_ENVIO_MS) {
    return;
  }
  ultimoEnvio = millis();

  float temperatura = dht.readTemperature();

  if (isnan(temperatura)) {
    Serial.println("Error: no se pudo leer el sensor DHT11. No se envía nada.");
    return;
  }

  Serial.print("Temperatura leída: ");
  Serial.print(temperatura);
  Serial.println(" °C");

  // Si el WiFi se cayó, se intenta reconectar antes de enviar.
  conectarWiFi();
  if (WiFi.status() == WL_CONNECTED) {
    enviarLectura(temperatura);
  }
}
