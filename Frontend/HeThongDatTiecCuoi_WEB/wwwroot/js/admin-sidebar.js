(() => {
    const storageKey = "sidebarState";
    const root = document.documentElement;
    const sidebar = document.getElementById("adminSidebar");
    const desktopToggle = document.getElementById("sidebarToggle");
    const mobileToggle = document.getElementById("mobileSidebarToggle");
    const backdrop = document.getElementById("sidebarBackdrop");
    const tooltip = document.getElementById("sidebarTooltip");
    const toast = document.getElementById("globalToast");
    const desktopMedia = window.matchMedia("(min-width: 851px)");

    if (!sidebar || !desktopToggle || !mobileToggle || !backdrop || !tooltip) {
        return;
    }

    const desktopToggleIcon = desktopToggle.querySelector("i");
    const isCollapsed = () => root.classList.contains("sidebar-is-collapsed");
    const isMobileOpen = () => root.classList.contains("sidebar-mobile-open");

    const saveDesktopState = () => {
        try {
            localStorage.setItem(storageKey, isCollapsed() ? "collapsed" : "expanded");
        } catch (error) {
            // Giao diện vẫn hoạt động trong phiên hiện tại khi localStorage bị chặn.
        }
    };

    const hideTooltip = () => {
        tooltip.classList.remove("show");
    };

    const syncControls = () => {
        const desktop = desktopMedia.matches;
        const collapsed = isCollapsed();
        const mobileOpen = isMobileOpen();

        desktopToggle.setAttribute("aria-expanded", String(desktop ? !collapsed : mobileOpen));
        mobileToggle.setAttribute("aria-expanded", String(mobileOpen));

        if (desktop) {
            const label = collapsed
                ? "Mở rộng thanh điều hướng"
                : "Thu gọn thanh điều hướng";

            desktopToggle.setAttribute("aria-label", label);
            desktopToggle.title = label;
            desktopToggleIcon?.classList.toggle("fa-angles-left", !collapsed);
            desktopToggleIcon?.classList.toggle("fa-angles-right", collapsed);
            desktopToggleIcon?.classList.remove("fa-xmark");
        } else {
            desktopToggle.setAttribute("aria-label", "Đóng thanh điều hướng");
            desktopToggle.title = "Đóng thanh điều hướng";
            desktopToggleIcon?.classList.remove("fa-angles-left", "fa-angles-right");
            desktopToggleIcon?.classList.add("fa-xmark");
        }
    };

    const closeMobileSidebar = () => {
        root.classList.remove("sidebar-mobile-open");
        syncControls();
    };

    const expandDesktopSidebar = () => {
        if (!desktopMedia.matches || !isCollapsed()) {
            return;
        }

        root.classList.remove("sidebar-is-collapsed");
        saveDesktopState();
        hideTooltip();
        syncControls();
    };

    desktopToggle.addEventListener("click", event => {
        event.stopPropagation();
        hideTooltip();

        if (!desktopMedia.matches) {
            closeMobileSidebar();
            mobileToggle.focus();
            return;
        }

        root.classList.toggle("sidebar-is-collapsed");
        saveDesktopState();
        syncControls();
    });

    mobileToggle.addEventListener("click", () => {
        root.classList.add("sidebar-mobile-open");
        syncControls();
        desktopToggle.focus();
    });

    backdrop.addEventListener("click", closeMobileSidebar);

    sidebar.addEventListener("click", event => {
        if (!desktopMedia.matches || !isCollapsed()) {
            return;
        }

        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const brand = target.closest(".sidebar-brand");
        if (brand) {
            event.preventDefault();
        }

        expandDesktopSidebar();
    });

    let toastTimer;
    document.querySelectorAll(".developing").forEach(button => {
        button.addEventListener("click", () => {
            if (!toast) {
                return;
            }

            window.clearTimeout(toastTimer);
            toast.classList.add("show");
            toastTimer = window.setTimeout(() => {
                toast.classList.remove("show");
            }, 2000);
        });
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && isMobileOpen()) {
            closeMobileSidebar();
            mobileToggle.focus();
        }
    });

    document.querySelectorAll("[data-sidebar-tooltip]").forEach(element => {
        const showTooltip = () => {
            if (!desktopMedia.matches || !isCollapsed()) {
                return;
            }

            const text = element.getAttribute("data-sidebar-tooltip");
            if (!text) {
                return;
            }

            const bounds = element.getBoundingClientRect();
            tooltip.textContent = text;
            tooltip.style.left = `${bounds.right + 10}px`;
            tooltip.style.top = `${bounds.top + bounds.height / 2}px`;
            tooltip.classList.add("show");
        };

        element.addEventListener("mouseenter", showTooltip);
        element.addEventListener("focusin", showTooltip);
        element.addEventListener("mouseleave", hideTooltip);
        element.addEventListener("focusout", hideTooltip);
    });

    const handleViewportChange = () => {
        hideTooltip();
        root.classList.remove("sidebar-mobile-open");
        syncControls();
    };

    if (typeof desktopMedia.addEventListener === "function") {
        desktopMedia.addEventListener("change", handleViewportChange);
    } else {
        desktopMedia.addListener(handleViewportChange);
    }

    syncControls();
})();
