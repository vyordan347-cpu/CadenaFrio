// Preferencias de la interfaz que se recuerdan en este navegador.
// Por ahora: si la barra lateral está plegada (solo íconos) en escritorio.

const CLAVE_LATERAL = "cadenafrio-lateral-plegada";

export function lateralPlegada() {
    try {
        return localStorage.getItem(CLAVE_LATERAL) === "si";
    } catch {
        return false;
    }
}

export function guardarLateralPlegada(plegada) {
    try {
        if (plegada) {
            localStorage.setItem(CLAVE_LATERAL, "si");
        } else {
            localStorage.removeItem(CLAVE_LATERAL);
        }
    } catch {
        // Sin almacenamiento (modo privado): se recuerda solo hasta recargar.
    }
}

// Abre un <dialog> como modal: el navegador bloquea el resto de la página, mantiene el foco
// dentro y lo cierra con Escape. Al cerrarse, avisa a Blazor con el botón usado ("si" o "no").
export function abrirDialogo(dialogo, componente) {
    dialogo.returnValue = "";
    dialogo.addEventListener("close", () => {
        componente.invokeMethodAsync("AlCerrar", dialogo.returnValue);
    }, { once: true });
    dialogo.showModal();
}

// Lleva el foco a un elemento (por ejemplo, el primer enlace del menú móvil al abrirlo).
export function enfocar(selector) {
    document.querySelector(selector)?.focus();
}

// Avisa a un componente el ancho real de un elemento, al empezar y cada vez que cambia
// (por ejemplo, el gráfico del historial se dibuja a tamaño real para que el texto se lea
// también en el celular). Devuelve un objeto con dejar() para dejar de observar.
export function observarAncho(elemento, componente) {
    let ultimo = 0;
    let pendiente = false;
    const avisar = () => {
        pendiente = false;
        const ancho = Math.round(elemento.getBoundingClientRect().width);
        if (ancho > 0 && ancho !== ultimo) {
            ultimo = ancho;
            componente.invokeMethodAsync("CambiarAncho", ancho);
        }
    };
    const observador = new ResizeObserver(() => {
        // Un aviso cada 50 ms como mucho, aunque el tamaño cambie varias veces seguidas.
        if (!pendiente) {
            pendiente = true;
            setTimeout(avisar, 50);
        }
    });
    observador.observe(elemento);
    avisar();
    return { dejar: () => observador.disconnect() };
}
