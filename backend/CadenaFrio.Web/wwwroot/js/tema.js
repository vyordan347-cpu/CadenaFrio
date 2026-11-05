// Tema de la interfaz: "sistema" (sigue al sistema operativo), "claro" u "oscuro".
// Se guarda en este navegador. index.html lo aplica antes de dibujar, para que no parpadee;
// este módulo lo cambia cuando la persona elige otro en el selector.

const CLAVE = "cadenafrio-tema";

export function obtener() {
    try {
        const tema = localStorage.getItem(CLAVE);
        return tema === "claro" || tema === "oscuro" ? tema : "sistema";
    } catch {
        return "sistema";
    }
}

export function aplicar(tema) {
    const raiz = document.documentElement;
    if (tema === "claro" || tema === "oscuro") {
        raiz.dataset.theme = tema;
    } else {
        delete raiz.dataset.theme;
    }

    try {
        if (tema === "claro" || tema === "oscuro") {
            localStorage.setItem(CLAVE, tema);
        } else {
            localStorage.removeItem(CLAVE);
        }
    } catch {
        // Sin almacenamiento (modo privado): el tema dura hasta recargar la página.
    }
}
