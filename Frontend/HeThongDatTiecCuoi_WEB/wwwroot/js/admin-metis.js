(() => {
    const storageKey = "adminColorMode";
    const fullscreenStorageKey = "adminFullscreenPreferred";
    const root = document.documentElement;
    const themeButton = document.getElementById("adminThemeToggle");
    const fullscreenButton = document.getElementById("adminFullscreenToggle");
    let removeFullscreenRestoreListeners = null;

    if (!themeButton || !fullscreenButton) return;

    const currentTheme = () => root.dataset.adminTheme === "dark" ? "dark" : "light";
    const fullscreenPreferred = () => {
        try {
            return sessionStorage.getItem(fullscreenStorageKey) === "true";
        } catch (error) {
            return false;
        }
    };
    const setFullscreenPreferred = preferred => {
        try {
            if (preferred) sessionStorage.setItem(fullscreenStorageKey, "true");
            else sessionStorage.removeItem(fullscreenStorageKey);
        } catch (error) {
            // Chế độ toàn màn hình vẫn hoạt động trong trang hiện tại.
        }
    };

    const syncThemeButton = () => {
        const dark = currentTheme() === "dark";
        themeButton.setAttribute("aria-label", dark ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối");
        themeButton.title = dark ? "Giao diện sáng" : "Giao diện tối";
        const icon = themeButton.querySelector("i");
        icon?.classList.toggle("bi-moon", !dark);
        icon?.classList.toggle("bi-sun", dark);
    };

    const syncFullscreenButton = () => {
        const active = Boolean(document.fullscreenElement);
        const restorePending = !active && fullscreenPreferred();
        fullscreenButton.setAttribute("aria-label", active
            ? "Thoát chế độ toàn màn hình"
            : restorePending ? "Khôi phục chế độ toàn màn hình" : "Bật chế độ toàn màn hình");
        fullscreenButton.title = active
            ? "Thoát toàn màn hình"
            : restorePending ? "Khôi phục toàn màn hình" : "Toàn màn hình";
        fullscreenButton.classList.toggle("is-restore-pending", restorePending);
        const icon = fullscreenButton.querySelector("i");
        icon?.classList.toggle("bi-arrows-fullscreen", !active);
        icon?.classList.toggle("bi-fullscreen-exit", active);
    };

    const disarmFullscreenRestore = () => {
        removeFullscreenRestoreListeners?.();
        removeFullscreenRestoreListeners = null;
    };

    const armFullscreenRestore = () => {
        if (!fullscreenPreferred() || document.fullscreenElement || removeFullscreenRestoreListeners) return;

        const restoreFromGesture = async event => {
            if (!fullscreenPreferred() || document.fullscreenElement) {
                disarmFullscreenRestore();
                return;
            }

            if (event.type === "pointerdown") {
                const target = event.target instanceof Element ? event.target : null;
                if (target?.closest("#adminFullscreenToggle, a[href], button[type='submit'], input[type='submit']")) return;
            }

            if (event.type === "keydown") {
                if (event.key === "Escape" || event.ctrlKey || event.metaKey || event.altKey) return;
                const target = event.target instanceof Element ? event.target : null;
                if (target?.matches("input, textarea, select, [contenteditable='true']")) return;
            }

            try {
                await root.requestFullscreen();
                disarmFullscreenRestore();
            } catch (error) {
                syncFullscreenButton();
            }
        };

        document.addEventListener("pointerdown", restoreFromGesture, true);
        document.addEventListener("keydown", restoreFromGesture, true);
        removeFullscreenRestoreListeners = () => {
            document.removeEventListener("pointerdown", restoreFromGesture, true);
            document.removeEventListener("keydown", restoreFromGesture, true);
        };
    };

    const restoreFullscreen = async () => {
        if (!fullscreenPreferred() || document.fullscreenElement) return;
        if (!document.fullscreenEnabled) {
            setFullscreenPreferred(false);
            syncFullscreenButton();
            return;
        }
        try {
            await root.requestFullscreen();
        } catch (error) {
            armFullscreenRestore();
        } finally {
            syncFullscreenButton();
        }
    };

    themeButton.addEventListener("click", () => {
        const nextTheme = currentTheme() === "dark" ? "light" : "dark";
        if (nextTheme === "dark") root.dataset.adminTheme = "dark";
        else delete root.dataset.adminTheme;

        try {
            localStorage.setItem(storageKey, nextTheme);
        } catch (error) {
            // Chế độ giao diện vẫn hoạt động trong phiên hiện tại.
        }

        syncThemeButton();
        window.dispatchEvent(new CustomEvent("admin-theme-change", { detail: { theme: nextTheme } }));
    });

    fullscreenButton.addEventListener("click", async () => {
        try {
            if (document.fullscreenElement) {
                setFullscreenPreferred(false);
                disarmFullscreenRestore();
                await document.exitFullscreen();
            } else {
                setFullscreenPreferred(true);
                await root.requestFullscreen();
            }
        } catch (error) {
            if (!document.fullscreenElement) setFullscreenPreferred(false);
            fullscreenButton.classList.add("is-unavailable");
            window.setTimeout(() => fullscreenButton.classList.remove("is-unavailable"), 800);
        } finally {
            syncFullscreenButton();
        }
    });

    document.addEventListener("fullscreenchange", () => {
        if (document.fullscreenElement) {
            setFullscreenPreferred(true);
            disarmFullscreenRestore();
        }
        syncFullscreenButton();
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && document.fullscreenElement) {
            setFullscreenPreferred(false);
            disarmFullscreenRestore();
        }
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
            event.preventDefault();
            document.getElementById("backofficeSearch")?.focus();
        }
    }, true);
    window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", event => {
        try {
            if (localStorage.getItem(storageKey)) return;
        } catch (error) {
            return;
        }

        root.dataset.adminTheme = event.matches ? "dark" : "light";
        syncThemeButton();
        window.dispatchEvent(new CustomEvent("admin-theme-change", {
            detail: { theme: currentTheme() }
        }));
    });
    syncThemeButton();
    syncFullscreenButton();
    restoreFullscreen();
})();
