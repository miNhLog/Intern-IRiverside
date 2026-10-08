(() => {
    const storageKey = "sidebarState";
    const root = document.documentElement;
    const sidebar = document.getElementById("adminSidebar");
    const desktopToggle = document.getElementById("sidebarToggle");
    const mobileToggle = document.getElementById("mobileSidebarToggle");
    const backdrop = document.getElementById("sidebarBackdrop");
    const tooltip = document.getElementById("sidebarTooltip");
    const toast = document.getElementById("globalToast");
    const searchInput = document.getElementById("backofficeSearch");
    const searchResults = document.getElementById("backofficeSearchResults");
    const notificationButton = document.getElementById("notificationButton");
    const notificationPanel = document.getElementById("notificationPanel");
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

    const normalizeSearchText = value => value
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/đ/g, "d")
        .toLowerCase()
        .trim();

    const searchTargets = [...document.querySelectorAll(".sidebar-menu a[href], .sidebar-brand[href]")]
        .map(link => ({
            label: link.textContent.replace(/\s+/g, " ").trim(),
            url: link.href,
            icon: link.querySelector("i")?.className || "fa-solid fa-arrow-right"
        }))
        .filter((target, index, targets) => target.label && targets.findIndex(item => item.url === target.url) === index);

    const closeSearchResults = () => {
        searchResults?.classList.remove("is-open");
        searchInput?.setAttribute("aria-expanded", "false");
    };

    const renderSearchResults = () => {
        if (!searchInput || !searchResults) return;

        const query = normalizeSearchText(searchInput.value);
        if (!query) {
            closeSearchResults();
            searchResults.replaceChildren();
            return;
        }

        const matches = searchTargets.filter(target => normalizeSearchText(target.label).includes(query)).slice(0, 7);
        searchResults.replaceChildren();

        if (!matches.length) {
            const empty = document.createElement("span");
            empty.className = "topbar-search-empty";
            empty.textContent = "Không tìm thấy chức năng phù hợp.";
            searchResults.append(empty);
        } else {
            matches.forEach(target => {
                const result = document.createElement("button");
                result.type = "button";
                result.className = "topbar-search-result";
                result.innerHTML = `<i class="${target.icon}" aria-hidden="true"></i><span></span>`;
                result.querySelector("span").textContent = target.label;
                result.addEventListener("click", () => { window.location.assign(target.url); });
                searchResults.append(result);
            });
        }

        searchResults.classList.add("is-open");
        searchInput.setAttribute("aria-expanded", "true");
    };

    searchInput?.addEventListener("input", renderSearchResults);
    searchInput?.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            closeSearchResults();
            searchInput.blur();
        }
    });
    document.addEventListener("click", event => {
        if (!(event.target instanceof Node) || !searchInput?.closest(".topbar-search")?.contains(event.target)) {
            closeSearchResults();
        }
    });

    const closeNotifications = () => {
        if (!notificationButton || !notificationPanel) return;

        notificationPanel.hidden = true;
        notificationButton.setAttribute("aria-expanded", "false");
        notificationButton.setAttribute("aria-label", "Mở thông báo");
    };

    notificationButton?.addEventListener("click", event => {
        event.stopPropagation();
        if (!notificationPanel) return;

        const isOpen = !notificationPanel.hidden;
        notificationPanel.hidden = isOpen;
        notificationButton.setAttribute("aria-expanded", String(!isOpen));
        notificationButton.setAttribute("aria-label", isOpen ? "Mở thông báo" : "Đóng thông báo");
    });

    document.addEventListener("click", event => {
        if (!(event.target instanceof Node) || !notificationButton?.closest(".topbar-notifications")?.contains(event.target)) {
            closeNotifications();
        }
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

        // Giữ nguyên trạng thái thu gọn khi người dùng chọn chức năng.
        // Chỉ nhấp vào vùng trống của sidebar mới mở rộng thanh điều hướng.
        if (target.closest("a[href], button, input, select, textarea, label")) {
            return;
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
        if (event.key === "Escape" && notificationPanel && !notificationPanel.hidden) {
            closeNotifications();
            notificationButton?.focus();
            return;
        }

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
