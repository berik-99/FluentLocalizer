(() => {
    const storageKey = "fluentlocalizer.theme";
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    let preference = "system";

    try {
        const saved = window.localStorage.getItem(storageKey);
        if (["light", "dark", "system"].includes(saved)) preference = saved;
    } catch { /* Storage may be unavailable. The system theme still works. */ }

    const apply = () => {
        document.documentElement.dataset.theme = preference === "system"
            ? (media.matches ? "dark" : "light")
            : preference;
    };

    media.addEventListener("change", apply);
    apply();

    window.fluentSite = {
        getTheme: () => preference,
        setTheme: value => {
            if (!["light", "dark", "system"].includes(value)) return;
            preference = value;
            try { window.localStorage.setItem(storageKey, value); } catch { /* Keep this session's choice. */ }
            apply();
        },
        prefersReducedMotion: () => window.matchMedia("(prefers-reduced-motion: reduce)").matches,
        copyText: text => navigator.clipboard.writeText(text)
    };
})();
