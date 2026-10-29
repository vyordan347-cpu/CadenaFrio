// Avisos del panel: sonido de alarma y pantalla completa.
// Módulo de JavaScript que Blazor carga solo cuando lo necesita (JS interop).

let contexto = null;

// Los navegadores solo permiten sonido después de que el usuario interactúa con la página.
// Por eso el panel tiene un botón "Activar sonido": ese clic habilita el audio.
export function activarSonido() {
    if (!contexto) {
        contexto = new (window.AudioContext || window.webkitAudioContext)();
    }
    if (contexto.state === "suspended") {
        contexto.resume();
    }
    tono(880, 0, 0.12);
    return true;
}

// Alarma: tres pitidos dobles, agudos y cortos, fáciles de distinguir de otros sonidos.
export function sonarAlarma() {
    if (!contexto) {
        return false;
    }
    for (let i = 0; i < 3; i++) {
        tono(988, i * 0.6, 0.15);
        tono(784, i * 0.6 + 0.2, 0.15);
    }
    return true;
}

function tono(frecuencia, retraso, duracion) {
    const inicio = contexto.currentTime + retraso;
    const oscilador = contexto.createOscillator();
    const volumen = contexto.createGain();
    oscilador.type = "sine";
    oscilador.frequency.value = frecuencia;
    volumen.gain.setValueAtTime(0.0001, inicio);
    volumen.gain.exponentialRampToValueAtTime(0.35, inicio + 0.02);
    volumen.gain.exponentialRampToValueAtTime(0.0001, inicio + duracion);
    oscilador.connect(volumen).connect(contexto.destination);
    oscilador.start(inicio);
    oscilador.stop(inicio + duracion + 0.05);
}

// Pantalla completa, pensada para un monitor fijo en la sala de inmunizaciones.
export async function alternarPantallaCompleta() {
    if (document.fullscreenElement) {
        await document.exitFullscreen();
        return false;
    }
    await document.documentElement.requestFullscreen();
    return true;
}
