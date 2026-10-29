# Protocolo de prueba de campo

**Sistema IoT para el Monitoreo de la Cadena de Frío de Vacunas**
Prototipo: ESP32 + sensor DHT11 · API ASP.NET Core + Blazor · SQLite

## 1. Objetivo

Verificar, durante un periodo continuo de 24 a 72 horas, que el sistema mide, transmite, detecta y avisa correctamente en condiciones reales, y obtener cifras que respalden su funcionamiento.

## 2. Preparación

1. Base de datos limpia: detener la API, borrar `cadenafrio.db` y ejecutar `dotnet ef database update`.
2. Registrar el equipo en Administración y cargar su clave en `credenciales.h` del ESP32.
3. Asignar al equipo la vacuna "Esquema regular (PNI)" (óptimo 3–7 °C, límite 2–8 °C).
4. Vincular Telegram en al menos dos usuarios: uno que recibe alertas y uno de escalamiento.
5. Colocar el sensor dentro del cooler o la refrigeradora, junto a un termómetro de referencia.
6. Anotar la fecha y hora de inicio.

## 3. Pruebas durante el periodo

| N° | Prueba | Cómo se provoca | Resultado esperado | Resultado obtenido | ¿Cumple? |
|---|---|---|---|---|---|
| 1 | Exactitud del sensor | Comparar con el termómetro de referencia 3 veces al día | Diferencia de ±2 °C o menos (precisión del DHT11) | | |
| 2 | Advertencia | Abrir la tapa 2 a 3 minutos | Estado "Advertencia" en el panel; no llega aviso al celular | | |
| 3 | Ruptura | Mantener la tapa abierta o calentar el sensor hasta superar 8 °C | Aviso de Telegram y correo en menos de 1 minuto | | |
| 4 | Atención | Presionar "Lo estoy atendiendo" | El panel muestra quién atiende; se avisa a los demás | | |
| 5 | Escalamiento | Provocar una ruptura y no atenderla | El contacto de escalamiento recibe el aviso a los 15 minutos | | |
| 6 | Pérdida de señal | Desconectar el ESP32 | "Sin señal" a los 5 minutos y aviso al celular | | |
| 7 | Recuperación | Volver a conectar el ESP32 | "Señal recuperada" y lecturas normales | | |
| 8 | Revisión | Registrar la revisión de cada ruptura | Las rupturas dejan de estar pendientes | | |

## 4. Indicadores a registrar al final

| Indicador | Cómo se obtiene | Valor |
|---|---|---|
| Duración de la prueba | Fecha de fin menos fecha de inicio | |
| Disponibilidad de datos | Panel → indicador "lecturas recibidas" | |
| Tiempo en rango óptimo | Historial del periodo | |
| Rupturas detectadas / rupturas provocadas | Historial y anotaciones | |
| Falsas alarmas (alertas sin causa real) | Comparar alertas con anotaciones | |
| Tiempo promedio de aviso | Hora del mensaje de Telegram menos hora de la lectura | |
| Tiempo promedio de respuesta | Panel → indicador "tiempo de respuesta" | |
| Diferencia promedio con el termómetro de referencia | Prueba N° 1 | |

## 5. Evidencias

- Capturas del panel, del historial y de los mensajes de Telegram (sin mostrar tokens ni contraseñas).
- Archivo de Excel exportado desde el Historial al final del periodo.
- Resultado de las pruebas automatizadas (`dotnet test`).

## 6. Conclusión

Resumir si el sistema cumplió cada prueba, los problemas encontrados y las mejoras propuestas (por ejemplo, el reemplazo del DHT11 por un DS18B20).
