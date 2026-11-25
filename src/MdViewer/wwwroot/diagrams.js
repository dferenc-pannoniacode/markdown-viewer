export async function renderDiagrams() {
    const blocks = document.querySelectorAll('.markdown-body pre > code[class~="language-plantuml" i]');
    if (blocks.length === 0) return;

    let renderPlantUml;
    try {
        ({ renderPlantUml } = await import("./plantuml-renderer.js"));
    } catch {
        for (const code of blocks) showError(code.parentElement);
        return;
    }

    for (const code of blocks) {
        if (!code.isConnected) return;
        const pre = code.parentElement;
        try {
            const svg = await renderPlantUml(code.textContent);
            if (!pre.isConnected) return;

            // Loading SVG as an image disables scripts and external resources within it.
            const image = new Image();
            image.className = "plantuml-diagram";
            image.alt = "PlantUML diagram";
            image.src = `data:image/svg+xml,${encodeURIComponent(svg)}`;
            await image.decode();
            pre.replaceWith(image);
        } catch {
            showError(pre);
        }
    }
}

function showError(pre) {
    if (!pre.isConnected) return;
    const message = document.createElement("p");
    message.setAttribute("role", "status");
    message.textContent = "Couldn’t render this PlantUML diagram.";
    pre.before(message);
}
