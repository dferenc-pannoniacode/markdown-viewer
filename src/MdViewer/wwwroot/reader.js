const documentLimit = 8 * 1024 * 1024;
let files = new Map();
const images = new Map();

export function readPreference(name) {
    try {
        return localStorage.getItem(`viewer-${name}`);
    } catch {
        return null;
    }
}

export function savePreference(name, value) {
    try {
        localStorage.setItem(`viewer-${name}`, value);
    } catch {
        // The reader can still use the preference for this page.
    }
}

export function selectFolder(input) {
    const selected = Array.from(input.files);
    input.value = "";
    if (selected.length === 0) return null;

    for (const url of images.values()) URL.revokeObjectURL(url);
    images.clear();
    files = new Map();

    for (const file of selected) {
        const [, ...segments] = file.webkitRelativePath.split("/");
        if (segments.length > 0 && segments.every(segment => segment && !segment.startsWith("."))) {
            files.set(segments.join("/"), file);
        }
    }

    return Array.from(files.keys()).filter(path => /\.md$/i.test(path));
}

export async function readDocument(path) {
    const file = files.get(path);
    if (!file) return null;
    if (file.size > documentLimit) throw new Error("This document exceeds 8 MiB.");
    return await file.text();
}

export function imageUrl(path, contentType) {
    const file = files.get(path);
    if (!file) return null;
    if (!images.has(path)) {
        images.set(path, URL.createObjectURL(file.slice(0, file.size, contentType)));
    }
    return images.get(path);
}

export function scrollDocument(fragment) {
    if (!fragment) {
        window.scrollTo(0, 0);
        return;
    }

    document.querySelector(".markdown-body #" + CSS.escape(fragment))?.scrollIntoView();
}
