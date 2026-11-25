window.viewerTheme = {
    read() {
        try {
            const value = localStorage.getItem("viewer-theme");
            return value === "light" || value === "dark" ? value : "system";
        } catch {
            return "system";
        }
    },

    set(value) {
        document.documentElement.dataset.theme = value;
        try {
            localStorage.setItem("viewer-theme", value);
        } catch {
            // Theme selection still applies for this page.
        }
    }
};

document.documentElement.dataset.theme = window.viewerTheme.read();
