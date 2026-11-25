import { renderDiagrams } from "./diagrams.js";

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

export function startNavigation() {
    document.addEventListener("click", followDocumentLink, true);
}

export function stopNavigation() {
    document.removeEventListener("click", followDocumentLink, true);
}

function followDocumentLink(event) {
    if (event.defaultPrevented || event.button !== 0 || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;

    const link = event.target.closest?.("a[href]");
    if (!link || (link.target && link.target !== "_self") || link.hasAttribute("download")) return;

    const destination = new URL(link.href);
    const base = new URL(document.baseURI);
    if (destination.origin !== base.origin || destination.pathname !== base.pathname) return;

    const current = new URL(location.href);
    const article = document.querySelector(".markdown-body");
    const documentPath = article?.getAttribute("aria-label");
    const destinationPath = destination.searchParams.get("file") ?? article?.dataset.defaultDocument;
    const sameDocument = destination.search === current.search || (
        destinationPath === documentPath &&
        destination.searchParams.get("source") === current.searchParams.get("source") &&
        !destination.searchParams.has("wiki") && !destination.searchParams.has("missing")
    );
    let steps = backSteps();
    if (documentPath && !sameDocument) {
        steps = 1;
    } else if (steps > 0) {
        steps++;
    }

    event.preventDefault();
    saveScrollPosition();
    Blazor.navigateTo(destination.href, { historyEntryState: String(steps) });
}

function backSteps() {
    // Blazor stores the supplied historyEntryState on the browser entry as userState.
    return Number(history.state?.userState) || 0;
}

function saveScrollPosition() {
    history.scrollRestoration = "manual";
    history.replaceState({ ...history.state, readerScrollY: window.scrollY }, "");
}

export function canGoBack() {
    return backSteps() > 0;
}

export function goBack() {
    const steps = backSteps();
    if (steps > 0) {
        saveScrollPosition();
        history.go(-steps);
    }
}

export function showDocument(fragment) {
    renderDiagrams();
    const savedPosition = history.state?.readerScrollY;
    if (typeof savedPosition === "number") {
        window.scrollTo(0, savedPosition);
        return;
    }

    if (!fragment) {
        window.scrollTo(0, 0);
        return;
    }

    document.querySelector(".markdown-body #" + CSS.escape(fragment))?.scrollIntoView();
}
