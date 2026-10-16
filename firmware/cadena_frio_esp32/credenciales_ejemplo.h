// Copie este archivo como "credenciales.h" (en la misma carpeta) y complete los datos.
// "credenciales.h" NO se sube a GitHub: contiene la contraseña del WiFi.

#pragma once

// Red WiFi (la misma a la que está conectada la PC que ejecuta la API).
// El ESP32 solo funciona con redes de 2.4 GHz.
const char* WIFI_NOMBRE = "NOMBRE_DE_LA_RED";
const char* WIFI_CLAVE = "CONTRASENA_DE_LA_RED";

// Dirección de la API: IP de la PC servidor (comando ipconfig) y su puerto.
const char* URL_API = "http://192.168.1.45:5067/api/lecturas";

// Clave del equipo registrado en la base de datos (el equipo de prueba).
const char* CLAVE_DISPOSITIVO = "clave-prueba-001";
