# CadenaFrio: Sistema IoT de monitoreo de cadena de frío de vacunas

Proyecto del curso Ingeniería de Software II (USMP). Jordan Viza Huanca hace el software; Dayron Apaza Acosta, el hardware.

## Forma de trabajo
- Responder y escribir comentarios en español. Nombres de clases y variables en español, como en el resto del código.
- **Preguntar antes de escribir código:** proponer el diseño y esperar aprobación.
- Después de cada cambio: `dotnet build` y `dotnet test` desde `backend/`.
- Cambios pequeños y explicados; no reescribir archivos completos sin necesidad.

## Stack
- ASP.NET Core .NET 10 (API) + Blazor WebAssembly alojado por la API (un solo `dotnet run`).
- SQLite + EF Core (`cadenafrio.db`, no se sube a GitHub).
- ESP32 + DHT11 (prototipo). **El firmware NO se modifica.**
- ClosedXML para Excel. Telegram Bot API (HTTP directo) y SMTP de Gmail para avisos.
- xUnit en `CadenaFrio.Tests` (SQLite en memoria).

## Estructura (`backend/`)
- `CadenaFrio.Domain`: entidades, enums, `LimitesTemperatura` (Clasificar, Interseccion, PorDefecto), `ClavesDispositivo`, `ContrasenasTemporales`, `INotificadorAlertas`.
- `CadenaFrio.Infrastructure`: DbContext y migraciones, `MotorAlertas`, `RegistroLecturas`, `DetectorSilencio`, `GestionRangos`, `EscaladorAlertas`, `ReconocimientoAlertas`, notificadores (Registro, Telegram, Correo, Compuesto).
- `CadenaFrio.Shared`: DTOs y contratos compartidos con la web.
- `CadenaFrio.Api`: controladores, `SesionUsuario` (cookie + sello de seguridad), `VigilanteSenal`, `BotTelegram` (long polling).
- `CadenaFrio.Web`: páginas Blazor, componentes (`Icono`, `IndicadorTemperatura`, `GraficoHistorial`, `MiniGrafico`), `wwwroot/css/sistema.css`.
- `CadenaFrio.Tests`: 37 pruebas, todas correctas.

## Ejecutar
```
cd backend/CadenaFrio.Api
dotnet ef database update --project ../CadenaFrio.Infrastructure
dotnet run --urls "http://0.0.0.0:5067"
```
Usuarios de prueba: `enfermera`/`Enfermera123!`, `responsable`/`Responsable123!`, `admin`/`Admin123!`.
Credenciales (token de Telegram, correo) en **User Secrets**, sección `Notificaciones`. Nunca en archivos del repositorio.

## Reglas de negocio
- Rangos por defecto según la **NTS N° 242-MINSA/DGIESP-2026**: óptimo 3–7 °C, límite 2–8 °C (ruptura fuera de 2–8; alerta de 2–3 y 7–8).
- Alertas en dos niveles: Advertencia (fuera del óptimo) y Ruptura (fuera del límite). Una alerta nunca baja de nivel; se cierra al volver al óptimo.
- Rupturas y silencios de más de 30 min exigen revisión (RF08). Las advertencias no.
- Sin señal: más de 5 min sin datos. Sensor sin variación: desactivado (`MinutosSensorSinVariacion: 0`) porque el DHT11 mide en pasos de 1 °C.
- El rango de cada equipo se calcula por intersección de las vacunas que guarda.
- Avisos externos (Telegram + correo) solo para lo urgente: ruptura, sin señal, sensor. Botón "Lo estoy atendiendo"; escalamiento a los 15 min.

## Trampas conocidas
- Razor: un `<text>` de SVG dentro de `@if`/`@foreach` debe envolverse en `<g>`.
- .NET 10 cambia el nombre de los módulos JS (fingerprinting): importar con `"./js/archivo.js?v=1"`.
- Migraciones con datos existentes pueden necesitar `migrationBuilder.Sql(...)` manual.
- Fechas: se guardan en UTC; Perú es UTC-5 (sin horario de verano).

## Pendiente (trabajo futuro)
- Sensor DS18B20; firmware con almacenamiento sin conexión, hora NTP y alarma local.
- Notificación de ruptura según el Anexo N° 13 de la NTS 242 (causa y acciones de listas oficiales, plazo de 24 h, formato en Excel).
- Bitácora de auditoría y respaldos automáticos de la base.
