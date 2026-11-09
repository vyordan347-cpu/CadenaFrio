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
- `CadenaFrio.Domain`: entidades (incluye `RegistroAuditoria`), enums (incluye `CategoriaAuditoria`), `LimitesTemperatura` (Clasificar, Interseccion, PorDefecto), `CatalogoAnexo13` (listas oficiales, Aplica, Validar, plazo de 24 h), `ClavesDispositivo`, `ContrasenasTemporales`, `INotificadorAlertas`.
- `CadenaFrio.Infrastructure`: DbContext y migraciones, `MotorAlertas`, `RegistroLecturas`, `DetectorSilencio`, `GestionRangos`, `EscaladorAlertas`, `ReconocimientoAlertas`, `Bitacora` (registra acciones) y `ConsultaBitacora` (filtros, páginas de 50), `RespaldoBaseDatos` (API de respaldo de SQLite, depura, valida nombres), `CalculoIndicadores` (indicadores y resumen por equipo en cualquier periodo), notificadores (Registro, Telegram, Correo, Compuesto), opciones (`OpcionesMonitoreo`, `OpcionesEstablecimiento`, `OpcionesRespaldo`).
- `CadenaFrio.Shared`: DTOs y contratos compartidos con la web (la web no referencia Domain: el catálogo del Anexo 13 llega por `GET /api/alertas/catalogo-anexo13`), `ConversionTemperatura` (°C/°F y sugerencia de redondeo).
- `CadenaFrio.Api`: controladores (incluye `AdminBitacoraController`, solo lectura, y `AdminRespaldosController`), `SesionUsuario` (cookie + sello de seguridad), `VigilanteSenal`, `RespaldoDiario`, `BotTelegram` (long polling), `ReportesController` y `Reportes/` (`FormatoAnexo13`, `ReporteExcel`, `EstiloExcel` con el formato común de los Excel).
- `CadenaFrio.Web`: páginas Blazor (incluye `Reportes` y `Alertas`), componentes (`Icono`, `IndicadorTemperatura`, `GraficoHistorial`, `MiniGrafico`, `SelectorUnidad`, `EntradaTemperatura`, `SelectorTema`, `IndicadorAlertas`, `DialogoConfirmacion`), `Utilidades/EstadoEquipo.cs`, `wwwroot/css/sistema.css`, `wwwroot/js/` (`tema.js`, `interfaz.js`).
- `CadenaFrio.Tests`: 98 pruebas, todas correctas.
- `docs/`: `protocolo-prueba-de-campo.md` y `restaurar-respaldo.md`.

## Interfaz (rediseño)
- `sistema.css` es el sistema de diseño: tokens (colores, tipografía `--t-*`, espaciado `--e-*`, radios, sombras), tema oscuro y componentes (`.boton*`, `.tarjeta`, `.aviso*`, `.insignia-*`, `.tabla`, `.estado-vacio`, `.esqueleto`, `.dialogo`) y estructura de página (`.pagina`, `.encabezado`, `.formulario`, `.campos`, `.acciones`). Los `.razor.css` solo llevan lo propio de cada página; usar `::deep` para componentes hijos.
- Tema Sistema/Claro/Oscuro: atributo `data-theme` en `<html>`, guardado en `localStorage` (`cadenafrio-tema`) y aplicado por un script en `index.html`.
- Menú lateral (`MainLayout`): completo desde 1280 px, solo iconos de 960 a 1279 px, cajón debajo de 960 px. Grupos Monitoreo, Gestión y Administración según la política del usuario.
- `/alertas`: activas (desde `api/equipos/estado`) y por revisar (`api/alertas/pendientes`); las atendidas siguen en Historial. Sin rutas nuevas en la API. Filtros por tipo y por equipo, y "Mostrar 10 más" en el navegador (la API no pagina).
- Panel: muestra las 5 alertas más urgentes (plazo del Anexo 13 más próximo primero) y enlaza a `/alertas`.
- `EstadoEquipo` separa el estado térmico del de comunicación; una lectura antigua (más allá de `MinutosSinSenal`) se muestra atenuada como "Última conocida", con la insignia de borde discontinuo ("Ruptura · hace 18 días") y un aviso de "último estado conocido" en vez de "active el plan de contingencia".
- Administración → Equipos: tres columnas separadas (Habilitación, Conexión, Temperatura); un equipo habilitado no implica que se esté comunicando.
- Acciones delicadas (retirar, desactivar, restablecer, nueva clave) se confirman con `DialogoConfirmacion.PreguntarAsync(...)` (`<dialog>` nativo), no con `confirm()`.
- Nunca solo color: icono + palabra + estilo de borde. Contraste WCAG AA comprobado en ambos temas.
- Microinteracciones solo en CSS (sección 13 de `sistema.css`), breves y apagadas con `prefers-reduced-motion`. Sin GSAP ni Lenis.
- Historial, pensado para el personal de postas: arriba un resumen en palabras simples (`Utilidades/ResumenHistorial.cs`: solo hechos, nunca "las vacunas están seguras", los periodos sin datos siempre se dicen) y el estado de la información (lecturas recibidas frente a las esperadas, una por minuto; aviso si la última lectura no está al día); después "Qué hacer" (también sin lecturas) y 4 tarjetas en lenguaje cotidiano. Los textos de "Qué hacer" están solo en `Utilidades/AccionesRecomendadas.cs` y debe validarlos la persona responsable de inmunizaciones: solo repiten lo que ya aplica el sistema (Anexo 13, plazo de 24 h, revisar el equipo y el termómetro). Los huecos se calculan en `Utilidades/TramosHistorial.cs`, compartido por el resumen y el gráfico.
- `GraficoHistorial` (minimalista, dos modos; dibuja su propia leyenda): se dibuja al ancho real (`observarAncho` en `interfaz.js`). **Vista sencilla**: franja verde tenue del rango recomendado, límites con líneas discontinuas, temperaturas en turquesa (línea con 3 tramos seguidos o más; si no, puntos) y en rojo solo lo que salió del límite (incluido el extremo de un tramo cuyo promedio esté dentro). Cada punto va en el centro de su tramo; la línea se corta si falta un tramo (y más de 5 min) y una barra gris fina sobre el eje marca "sin datos". **Vista detallada**: suma la banda de mínimo y máximo, las alertas como barras con su duración, "Ajustar a los datos", los datos técnicos (promedio, cumplimiento; la página los pasa en `DatosTecnicos`) y la lista de periodos sin datos con fechas exactas. Cada lectura por separado solo está en el Excel (el periodo mínimo del Historial es de 24 h, así que los puntos siempre agrupan lecturas). No volver a agregar tramas, franjas de color ni etiquetas de más.
- Cumplimiento térmico = **% de las lecturas** en rango óptimo (no % del tiempo); así se rotula en Historial y Reportes.

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
- Revisión de rupturas de temperatura con el **Anexo N° 13** (NTS 242, págs. 81–83 de los anexos): una causa (lista según refrigeradora o termo), una forma de restablecimiento y "¿vacunas inmovilizadas?". "Otro/Otra causa" exige detalle. Los silencios y advertencias se revisan con texto libre (mín. 10 caracteres).
- Plazo de notificación: 24 h desde la ruptura (numeral 6.10.2.1); se muestra en cada ruptura pendiente. El Excel del Anexo 13 se descarga desde el Historial; lo que el sistema no sabe (marca, lotes, stock, cargo) queda en celdas amarillas.
- Datos del establecimiento para el Anexo 13: sección `Establecimiento` de `appsettings.json`.
- Bitácora de auditoría: toda acción del personal (sesión, intentos fallidos, rangos, revisiones, "Lo estoy atendiendo", usuarios, equipos, vacunas) se registra con `Bitacora.Registrar(...)` en el mismo `SaveChanges` que la acción. Nunca se guardan contraseñas ni claves de dispositivo. Las consultas (ver historial, descargar Excel) no se registran. Solo el administrador la ve, en Administración → Bitácora; no se puede editar ni borrar.
- Respaldos (`backend/CadenaFrio.Api/respaldos/`, sección `Respaldos` de `appsettings.json`): uno automático al día desde las 03:00 de Perú (o al encender, si ese día no se hizo) y "Respaldar ahora" en Administración → Respaldos. Se conservan los últimos 7 de cada tipo (automático y manual por separado). Crear y descargar quedan en la bitácora. Restaurar solo a mano, con el servidor detenido, según `docs/restaurar-respaldo.md`.
- Reportes (`/reportes`, todos los roles): indicadores y Excel del periodo y equipo elegidos. Días completos en hora de Perú; el periodo es `[desde, hasta)`, máximo 92 días. El Excel incluye cada lectura solo si el periodo es de 7 días o menos. El panel ya no muestra indicadores: lo primero son los equipos.
- Temperaturas en °F: solo para **escribir** rangos (Vacunas y ajuste manual), copiados de insertos importados. Se convierten y se guardan en °C con una décima; si el resultado queda a ≤ 0.3 °C de un entero, se sugiere el entero (36–46 °F → 2–8 °C). Todo lo demás se muestra en °C.

## Trampas conocidas
- Razor: un `<text>` de SVG dentro de `@if`/`@foreach` debe envolverse en `<g>`.
- .NET 10 cambia el nombre de los módulos JS (fingerprinting): importar con `"./js/archivo.js?v=1"`.
- Archivos estáticos con `app.MapStaticAssets()` (no `UseStaticFiles` ni `UseBlazorFrameworkFiles`: juntos dan error 500 en `_framework`). Tras el cambio, Ctrl+F5 una vez en cada navegador para soltar el CSS viejo en caché.
- `GraficoHistorial` repite la cuenta de tramos de `HistorialController.Resumir` (periodo / 300, mínimo 1 min): si cambia en la API, cambiarla también en el gráfico.
- El navegador integrado de pruebas con el panel oculto no dibuja: el evento `close` de `<dialog>` y los avisos de `ResizeObserver` no llegan. Probar diálogos y cambios de tamaño con el panel visible.
- Migraciones con datos existentes pueden necesitar `migrationBuilder.Sql(...)` manual.
- Fechas: se guardan en UTC; Perú es UTC-5 (sin horario de verano).
- Con el servidor encendido, `dotnet build` falla al copiar los `.dll` (bloqueados): detenerlo o compilar con `--configuration Release` (también para `dotnet ef`).
- Después de crear una migración, aplicarla a `cadenafrio.db` (con respaldo previo) antes de usar la versión nueva; si no, todas las pantallas fallan.

## Pendiente (trabajo futuro)
- Sensor DS18B20; firmware con almacenamiento sin conexión, hora NTP y alarma local.
- Juzgar cada lectura (Historial y Reportes) con el rango vigente en ese momento, no con el actual.
- Llenar `docs/protocolo-prueba-de-campo.md` con los resultados reales del ESP32.
- Plazo del Anexo 13 en alertas detectadas tarde: hoy cuenta desde la ruptura, no desde la sincronización (haría falta un campo nuevo en `Alerta`).
- Anexo 13A (evaluación de la ruptura): lo hace la DIRESA, fuera del alcance por ahora.
