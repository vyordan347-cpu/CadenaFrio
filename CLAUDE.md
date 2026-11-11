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
- `CadenaFrio.Domain`: entidades (incluye `RegistroAuditoria`), enums (incluye `CategoriaAuditoria`), `LimitesTemperatura` (Clasificar, Interseccion, PorDefecto, DentroDe), `CatalogoAnexo13` (listas oficiales, Aplica, Validar, plazo de 24 h), `ClavesDispositivo`, `ContrasenasTemporales`, `INotificadorAlertas`.
- `CadenaFrio.Infrastructure`: DbContext y migraciones, `MotorAlertas`, `RegistroLecturas`, `DetectorSilencio`, `GestionRangos`, `EscaladorAlertas`, `ReconocimientoAlertas`, `Bitacora` (registra acciones) y `ConsultaBitacora` (filtros, páginas de 50), `RespaldoBaseDatos` (API de respaldo de SQLite, depura, valida nombres), `CalculoIndicadores` (indicadores y resumen por equipo en cualquier periodo), `HistoriaRangos` (qué rango regía en cada momento: `En(fecha)`, `Tramos(desde, hasta)`, `Contar(lecturas)`), `GestionRangos.ReferenciaAjusteAsync`/`ValidarAjusteManual` (regla del ajuste manual), notificadores (Registro, Telegram, Correo, Compuesto), `ResumenDiario` y `MensajeResumenDiario` (resumen diario por Telegram), opciones (`OpcionesMonitoreo`, `OpcionesEstablecimiento`, `OpcionesRespaldo`, `OpcionesResumenDiario`).
- `CadenaFrio.Shared`: DTOs y contratos compartidos con la web (la web no referencia Domain: el catálogo del Anexo 13 llega por `GET /api/alertas/catalogo-anexo13`), `ConversionTemperatura` (°C/°F y sugerencia de redondeo), `Contracts/Seguridad.cs` (`Roles`, nombres visibles y `Politicas.RolesPermitidos` + `Politicas.Configurar`: la única matriz de permisos, la usan la API y la web), `Utilidades/CompatibilidadVacunas.cs` (qué vacuna no se cruza con las marcadas y por qué).
- `CadenaFrio.Api`: controladores (incluye `AdminBitacoraController`, solo lectura, y `AdminRespaldosController`), `SesionUsuario` (cookie + sello de seguridad), `VigilanteSenal`, `RespaldoDiario`, `EnvioResumenDiario`, `BotTelegram` (long polling; `/resumen` y `/resumen hoy`), `ReportesController` y `Reportes/` (`FormatoAnexo13`, `ReporteExcel`, `EstiloExcel` con el formato común de los Excel).
- `CadenaFrio.Web`: páginas Blazor (incluye `Reportes` y `Alertas`), componentes (`Icono`, `IndicadorTemperatura` (velocímetro: panel y detalle), `MiniGrafico` (ya no se usa), `GraficoHistorial`, `GraficoUltimas24h`, `SelectorUnidad`, `EntradaTemperatura`, `SelectorTema`, `IndicadorAlertas`, `DialogoConfirmacion`, `EscalaTermica`), `Utilidades/EstadoEquipo.cs`, `wwwroot/css/sistema.css`, `wwwroot/js/` (`tema.js`, `interfaz.js`).
- `CadenaFrio.Tests`: 171 pruebas, todas correctas (incluye `AutorizacionTests`: la matriz de permisos por rol).
- `docs/`: `protocolo-prueba-de-campo.md` y `restaurar-respaldo.md`.

## Interfaz (rediseño)
- `sistema.css` es el sistema de diseño: tokens (colores, tipografía `--t-*`, espaciado `--e-*`, radios, sombras), tema oscuro y componentes (`.boton*`, `.tarjeta`, `.aviso*`, `.insignia-*`, `.tabla`, `.estado-vacio`, `.esqueleto`, `.dialogo`) y estructura de página (`.pagina`, `.encabezado`, `.formulario`, `.campos`, `.acciones`). Los `.razor.css` solo llevan lo propio de cada página; usar `::deep` para componentes hijos.
- Tema Sistema/Claro/Oscuro: atributo `data-theme` en `<html>`, guardado en `localStorage` (`cadenafrio-tema`) y aplicado por un script en `index.html`.
- Menú lateral (`MainLayout`): completo desde 1280 px, solo iconos de 960 a 1279 px, cajón debajo de 960 px. Grupos Monitoreo, Gestión ("Rangos de temperatura", para todos) y Administración (solo administrador).
- Inicio de sesión: foto `wwwroot/img/fondo-login.webp` con capa oscura, tarjeta translúcida con colores fijos (igual en ambos temas), escudo `usmp-escudo.png` en círculo blanco y botón morado como la referencia.
- **Rangos de conservación** (menú "Rangos de temperatura", ruta `/configuracion` sin cambiar): todos los roles la ven; quien no tiene `PuedeConfigurarRangos` la ve en solo lectura. Vacunas a la izquierda (las que no se cruzan con las marcadas quedan bloqueadas con el motivo en palabras), "Ajuste avanzado" debajo, y a la derecha "Rango del equipo": vigente frente al calculado y `EscalaTermica` (escala horizontal con zonas y trama, marcador calculado, fecha, comunicación; lectura antigua con marcador hueco y "última conocida"). En esta pantalla no se usa el velocímetro.
- Centro de monitoreo: equipos uno debajo de otro (los que requieren atención primero), en tarjetas horizontales con tres áreas: **Temperatura** (velocímetro compacto, estado en grande, "Medida hace…" o, si es antigua, atenuada y "Última conocida · hace…", y el rango "Permitido… · ideal…"), **Estado del equipo** (comunicación, tiempo sin datos, lecturas de la última hora y mínima/máxima de 24 h solo con 30 lecturas o más) y **Atención** (alertas por revisar del equipo, quién lo atiende, qué hacer en una frase solo si hay un problema, Ver detalle). El reparto depende del ancho de la columna (`container-type` en `.equipos` y `@container`): tres áreas en fila, temperatura arriba y las otras dos debajo, o una columna en celular. La mínima y máxima de 24 h se piden con `lecturas-recientes?minutos=1440`, una vez por equipo y como mucho una vez por minuto.
- Detalle del equipo (mini dashboard): el velocímetro (con la tendencia de la última hora) junto a tres tarjetas separadas (Temperatura, Comunicación, Incidentes); debajo `EscalaTermica`, mensaje de acción solo si hay un problema, "Lo estoy atendiendo" y `GraficoUltimas24h` (franja óptima, límites, línea cortada donde faltan datos, fuera del límite en rojo). El gráfico solo se dibuja con lecturas recientes y al menos 30 en 24 h (`lecturas-recientes?minutos=1440`); si no, se explica por qué. Debajo, las alertas por revisar en dos columnas; la que se revisa ocupa todo el ancho.
- `/alertas`: activas (desde `api/equipos/estado`) y por revisar (`api/alertas/pendientes`); las atendidas siguen en Historial. Sin rutas nuevas en la API. Filtros por tipo y por equipo, y "Mostrar 10 más" en el navegador (la API no pagina).
- Panel: muestra las 5 alertas más urgentes (plazo del Anexo 13 más próximo primero) y enlaza a `/alertas`.
- `EstadoEquipo` separa el estado térmico del de comunicación; una lectura antigua (más allá de `MinutosSinSenal`) se muestra atenuada como "Última conocida", con la insignia de borde discontinuo ("Ruptura · hace 18 días") y un aviso de "último estado conocido" en vez de "active el plan de contingencia".
- Código QR por equipo: Administración → Equipos → "Código QR" (`/admin/equipos/{id}/etiqueta`). Etiqueta para imprimir con el QR que abre `/equipos/{id}`. El QR se genera en el navegador con QRCoder (MIT) en `Shared/Utilidades/EtiquetaEquipo.cs`. La dirección para el celular se elige de las detectadas por `GET /api/admin/equipos/direcciones` (red local primero, sin 169.254.x) y se recuerda en el navegador; con localhost se avisa que no sirve en el celular. Para imprimir, la sección 14 de `sistema.css` oculta el menú y lo marcado con `no-imprimir`.
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
Usuarios de prueba: `enfermera`/`Enfermera123!`, `responsable`/`Responsable123!`, `medico`/`Medico123!`, `admin`/`Admin123!` (el sembrador solo los crea en una base vacía: en una base existente, crear `medico` en Administración → Usuarios).
Credenciales (token de Telegram, correo) en **User Secrets**, sección `Notificaciones`. Nunca en archivos del repositorio.

## Reglas de negocio
- Rangos por defecto según la **NTS N° 242-MINSA/DGIESP-2026**: óptimo 3–7 °C, límite 2–8 °C (ruptura fuera de 2–8; alerta de 2–3 y 7–8).
- Alertas en dos niveles: Advertencia (fuera del óptimo) y Ruptura (fuera del límite). Una alerta nunca baja de nivel; se cierra al volver al óptimo.
- Rupturas y silencios de más de 30 min exigen revisión (RF08). Las advertencias no.
- Sin señal: más de 5 min sin datos. Sensor sin variación: desactivado (`MinutosSensorSinVariacion: 0`) porque el DHT11 mide en pasos de 1 °C.
- **Roles según la NTS 242** (el administrador puede todo):
  - `Enfermera` (se ve como "Técnico de enfermería", registro diario delegado, 6.3.1.3): consulta, "Lo estoy atendiendo" y revisión de silencios y advertencias.
  - `ResponsableInmunizaciones` (Lic. en enfermería, 5.1.43 y 5.1.44): además, configura rangos y revisa rupturas (Anexo 13).
  - `MedicoJefe` = 4 ("Médico jefe del establecimiento", 6.10.2: notifica a través del responsable): además de consultar, revisa rupturas; no configura rangos.
  - `Administrador`: todo, incluida la administración. Políticas: `PuedeConfigurarRangos`, `PuedeRevisarRupturas`, `EsAdministrador`. El rol se guarda como número: no cambiar los valores del enum.
- El rango de cada equipo se calcula por intersección de las vacunas que guarda (los óptimos deben cruzarse en un tramo). La web bloquea las combinaciones imposibles; la API vuelve a validar.
- Ajuste manual ("Ajuste avanzado"): motivo obligatorio (10–250 caracteres) y confirmación; queda como "Ajuste manual: motivo" en el historial y en la bitácora. Solo puede ser **igual o más estricto** que el rango de las vacunas del equipo (o el de la NTS 242 si no tiene), nunca más amplio: la API responde 422.
- Cada lectura se juzga con el rango que regía cuando se tomó (`HistoriaRangos`, a partir de `RangoTemperatura.FechaCambio`; antes del primer rango guardado rige el de la norma). Lo usan Historial (API, Excel y web), Reportes (`CalculoIndicadores` y su Excel). Si el rango cambió en el periodo, el gráfico escalona la franja y los límites, y el resumen y el Excel lo avisan. `HistorialDto.Rangos` trae los tramos y `Estadisticas.CantidadFueraDelLimite` el conteo exacto.
- Avisos externos (Telegram + correo) solo para lo urgente: ruptura, sin señal, sensor. Botón "Lo estoy atendiendo"; escalamiento a los 15 min.
- Resumen diario por Telegram (sección `ResumenDiario` de `appsettings.json`: `Activo`, `Hora` = 7): a esa hora de Perú, el del día anterior (mínima, máxima, % en óptimo, lecturas recibidas frente a esperadas, rupturas y revisiones pendientes con su plazo) a los usuarios con Telegram vinculado y "Recibe alertas". Si el servidor está apagado a esa hora, ese día no se envía. Solo hechos, nunca "las vacunas están seguras". Los chats vinculados también pueden pedirlo con `/resumen` (ayer) o `/resumen hoy`.
- Revisión de rupturas de temperatura (solo `PuedeRevisarRupturas`; los demás ven quién la registra) con el **Anexo N° 13** (NTS 242, págs. 81–83 de los anexos): una causa (lista según refrigeradora o termo), una forma de restablecimiento y "¿vacunas inmovilizadas?". "Otro/Otra causa" exige detalle. Los silencios y advertencias se revisan con texto libre (mín. 10 caracteres).
- Plazo de notificación: 24 h (numeral 6.10.2.1) contadas **desde que se detecta** la ruptura (6.10.1 "en cuanto se detecta" y "Fecha en que detectó" del Anexo 13). Detección = hora en que llegó al servidor la lectura que la provocó (`ConsultasCadenaFrio.ObtenerDeteccionRupturaAsync`, con `Lectura.FechaRecepcion`; sin migración). En vivo coincide con la ruptura; si se conoció al reconectarse, la web y el Anexo 13 dicen "ocurrió… se conoció…". Se muestra en cada ruptura pendiente. El Excel del Anexo 13 se descarga desde el Historial; lo que el sistema no sabe (marca, lotes, stock, cargo) queda en celdas amarillas.
- Datos del establecimiento para el Anexo 13 y el Anexo 10: sección `Establecimiento` de `appsettings.json`.
- Hoja del **Anexo N° 10** (control y registro diario de la temperatura): en Historial, botón "Hoja del Anexo 10" con selector de mes (`GET /api/historial/anexo10?equipoId=1&mes=2026-11`). Un día por fila, turnos mañana (8–14 h) y tarde (14–20 h) de Perú; en cada turno, la primera lectura del sensor, su estado con el rango de su momento, "salió del límite en el turno" si hubo algún pico, y "Sin datos" si no llegó ninguna lectura. Cálculo en `HojaControlTemperatura` (Infrastructure); Excel en `Reportes/FormatoAnexo10.cs`. Lo que el sistema no sabe (tipo, marca, código patrimonial, responsable) va en celdas amarillas.
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
- Algunos rangos guardados antes de la migración `DosNiveles` quedaron con óptimo 0–0 en la base real: `HistoriaRangos` los lee con la regla de esa migración (óptimo = límite), sin modificar la base.
- Migraciones con datos existentes pueden necesitar `migrationBuilder.Sql(...)` manual.
- Dos servidores con el mismo token de Telegram a la vez (por ejemplo, el de Jordan y el de Dayron) se pisan: el bot solo funciona con uno. Para pruebas, arrancar con `Notificaciones__Telegram__Token=""`.
- Sin `--urls "http://0.0.0.0:5067"` el servidor solo escucha en localhost (así está `launchSettings.json`): el celular (QR) y el ESP32 no llegan.
- Fechas: se guardan en UTC; Perú es UTC-5 (sin horario de verano).
- Con el servidor encendido, `dotnet build` falla al copiar los `.dll` (bloqueados): detenerlo o compilar con `--configuration Release` (también para `dotnet ef`).
- Después de crear una migración, aplicarla a `cadenafrio.db` (con respaldo previo) antes de usar la versión nueva; si no, todas las pantallas fallan.

## Pendiente (trabajo futuro)
- Sensor DS18B20; firmware con almacenamiento sin conexión, hora NTP y alarma local.
- Lecturas que llegan tarde (al reconectarse): el motor de alertas las clasifica con el rango vigente al recibirlas, no con el de su hora (caso raro: solo importa si el rango cambió durante la desconexión).
- Llenar `docs/protocolo-prueba-de-campo.md` con los resultados reales del ESP32.
- Anexo 13A (evaluación de la ruptura): lo hace la DIRESA, fuera del alcance por ahora.
