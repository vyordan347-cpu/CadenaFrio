# Restaurar un respaldo de la base de datos

**Sistema IoT para el Monitoreo de la Cadena de Frío de Vacunas**

Los respaldos se crean solos cada día (y con "Respaldar ahora" en Administración → Respaldos).
Restaurar es reemplazar `cadenafrio.db` por uno de ellos. **Se pierde todo lo que ocurrió después
del respaldo** (lecturas, alertas, revisiones, cambios de usuarios), así que solo se hace si la base
actual está dañada o se borraron datos por error.

No hay botón para restaurar desde la web: reemplazar la base con el servidor encendido puede dañarla.

## Dónde están los respaldos

- En la PC: `backend/CadenaFrio.Api/respaldos/` (carpeta configurable en `appsettings.json`, sección `Respaldos`).
- Nombres: `auto-2026-10-30_030000.db` (automático) o `manual-2026-10-30_154210.db` (manual), con fecha y hora de Perú.
- O un respaldo descargado desde Administración → Respaldos y guardado fuera de la PC.

## Pasos (PowerShell, desde la carpeta `backend/CadenaFrio.Api`)

1. **Detener el servidor.** En la terminal donde corre, presionar `Ctrl+C`. Confirmar que ya no está:

   ```powershell
   Get-Process CadenaFrio.Api -ErrorAction SilentlyContinue
   ```

   No debe mostrar nada.

2. **Guardar aparte la base actual**, por si hay que volver atrás:

   ```powershell
   New-Item -ItemType Directory -Force antes-de-restaurar
   Move-Item cadenafrio.db* antes-de-restaurar
   ```

   Esto mueve también `cadenafrio.db-wal` y `cadenafrio.db-shm`. Es importante: si quedan junto al
   respaldo restaurado, SQLite intentaría aplicarlos sobre una base que no les corresponde.

3. **Copiar el respaldo elegido como `cadenafrio.db`:**

   ```powershell
   Copy-Item respaldos\auto-2026-10-30_030000.db cadenafrio.db
   ```

4. **Aplicar las migraciones**, por si el respaldo es de una versión anterior del sistema
   (si ya está al día, no cambia nada):

   ```powershell
   dotnet ef database update --project ../CadenaFrio.Infrastructure
   ```

5. **Encender el servidor y revisar:**

   ```powershell
   dotnet run --urls "http://0.0.0.0:5067"
   ```

   Iniciar sesión como administrador y comprobar en el Historial que están los datos hasta la fecha del respaldo.

6. **Cuando todo esté bien**, se puede borrar la carpeta `antes-de-restaurar`. Si algo salió mal,
   detener el servidor, borrar `cadenafrio.db` y volver a mover los archivos de esa carpeta a su lugar.

## Después de restaurar

- El respaldo automático de ese día se vuelve a hacer solo si todavía no existe uno de hoy en la carpeta.
- Los dispositivos (ESP32) siguen funcionando: sus claves están en la base, y la del respaldo es la misma
  salvo que se haya regenerado una clave después. En ese caso, regenerarla de nuevo y cargarla en el dispositivo.
- Anotar en el cuaderno de la posta qué respaldo se restauró y por qué (la bitácora del sistema también
  vuelve a la fecha del respaldo).
