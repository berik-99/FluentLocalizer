(() => {
    const storageKey = "fluentlocalizer.theme";
    const themes = ["light", "dark", "system"];
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    let preference = "system";

    try {
        const saved = window.localStorage.getItem(storageKey);
        if (themes.includes(saved)) preference = saved;
    } catch { /* Storage may be unavailable. The system theme still works. */ }

    const apply = () => {
        document.documentElement.dataset.theme = preference === "system"
            ? (media.matches ? "dark" : "light")
            : preference;
    };

    media.addEventListener("change", apply);
    apply();

    // Blazor cannot cancel a keydown conditionally, so keys handled by keyboard widgets
    // are cancelled here to stop them from scrolling the page.
    const widgetKeys = [
        { selector: ".dropdown-trigger", keys: ["ArrowUp", "ArrowDown", "Home", "End", "Enter", " "] },
        { selector: '[role="tab"]', keys: ["ArrowLeft", "ArrowRight", "Home", "End"] }
    ];
    document.addEventListener("keydown", event => {
        if (!(event.target instanceof Element) || event.altKey || event.ctrlKey || event.metaKey) return;
        if (widgetKeys.some(({ selector, keys }) => keys.includes(event.key) && event.target.matches(selector)))
            event.preventDefault();
    }, true);

    window.fluentSite = {
        getTheme: () => preference,
        setTheme: value => {
            if (!themes.includes(value)) return;
            preference = value;
            try { window.localStorage.setItem(storageKey, value); } catch { /* Keep this session's choice. */ }
            apply();
        },
        prefersReducedMotion: () => window.matchMedia("(prefers-reduced-motion: reduce)").matches,
        copyText: text => navigator.clipboard.writeText(text),

        // True when a dropdown menu fits above its trigger but not below it.
        shouldDropUp: dropdown => {
            const trigger = dropdown.getBoundingClientRect();
            const menu = dropdown.querySelector(".dropdown-menu").offsetHeight + 12;
            return window.innerHeight - trigger.bottom < menu && trigger.top > menu;
        },

        // Marks the link of the topmost section crossing the upper part of the viewport.
        watchSections: nav => {
            const links = [...nav.querySelectorAll('a[href*="#"]')];
            const sections = links.map(link => document.getElementById(decodeURIComponent(link.hash.slice(1)))).filter(Boolean);
            const visible = new Set();
            const observer = new IntersectionObserver(entries => {
                for (const entry of entries)
                    entry.isIntersecting ? visible.add(entry.target) : visible.delete(entry.target);
                const current = sections.find(section => visible.has(section));
                if (!current) return;
                for (const link of links) {
                    const active = link.hash === `#${current.id}`;
                    link.classList.toggle("is-active", active);
                    active ? link.setAttribute("aria-current", "location") : link.removeAttribute("aria-current");
                }
            }, { rootMargin: "-20% 0px -60% 0px" });
            sections.forEach(section => observer.observe(section));
            return { dispose: () => observer.disconnect() };
        }
    };
})();
