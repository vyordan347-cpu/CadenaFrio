// Animación del número del indicador: cuenta desde el valor anterior hasta el nuevo.
// Es un módulo de JavaScript que Blazor carga solo cuando lo necesita (JS interop).
export function contar(elemento, desde, hasta, duracionMs) {
    if (!elemento) {
        return;
    }

    // Si el usuario pidió reducir animaciones en su sistema, se muestra el valor final.
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        elemento.textContent = hasta.toFixed(1);
        return;
    }

    const inicio = performance.now();
    const paso = (ahora) => {
        const avance = Math.min(1, (ahora - inicio) / duracionMs);
        const suavizado = 1 - Math.pow(1 - avance, 3);
        elemento.textContent = (desde + (hasta - desde) * suavizado).toFixed(1);
        if (avance < 1) {
            requestAnimationFrame(paso);
        }
    };
    requestAnimationFrame(paso);
}
