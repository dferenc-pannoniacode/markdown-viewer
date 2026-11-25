import "./plantuml/viz-global.js";
import { renderToString } from "./plantuml/plantuml.js";

const supportScripts = new Map([
    ["emoji.js", () => import("./plantuml/emoji.js")],
    ["openiconic.js", () => import("./plantuml/openiconic.js")],
    ["themes.js", () => import("./plantuml/themes.js")]
]);

globalThis.PLANTUML_STDLIB_LOADER = (name, ready, fail) => {
    const load = supportScripts.get(name);
    if (load) load().then(ready, () => fail("Couldn’t load PlantUML resources."));
    else fail("PlantUML library is unavailable.");
};

export function renderPlantUml(source) {
    const lines = source.trim().split(/\r\n|\r|\n/);
    // The browser engine expects literal delimiters and has no output filename to name.
    if (/^@startuml(?:\s|$)/i.test(lines[0])) lines.shift();
    if (/^\s*@enduml\s*$/i.test(lines.at(-1))) lines.pop();

    return new Promise((resolve, reject) => renderToString(["@startuml", ...lines, "@enduml"], resolve, reject));
}
