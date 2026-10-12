(() => {
    const page = document.querySelector('.home-page');
    if (!page) return;

    const header = page.querySelector('.home-header');
    const toggle = header?.querySelector('.mobile-menu-toggle');
    const menu = header?.querySelector('.mobile-nav');
    const backdrop = page.querySelector('.mobile-nav-backdrop');
    if (!toggle || !menu || !backdrop) return;

    const firstLink = menu.querySelector('a');
    const closeMenu = (restoreFocus = false) => {
        if (menu.hidden) return;
        menu.hidden = true;
        backdrop.hidden = true;
        toggle.setAttribute('aria-expanded', 'false');
        toggle.setAttribute('aria-label', 'Mở menu');
        toggle.querySelector('.material-symbols-outlined').textContent = 'menu';
        if (restoreFocus) toggle.focus();
    };

    toggle.addEventListener('click', () => {
        const opening = menu.hidden;
        menu.hidden = !opening;
        backdrop.hidden = !opening;
        toggle.setAttribute('aria-expanded', String(opening));
        toggle.setAttribute('aria-label', opening ? 'Đóng menu' : 'Mở menu');
        toggle.querySelector('.material-symbols-outlined').textContent = opening ? 'close' : 'menu';
        if (opening) firstLink?.focus();
    });
    backdrop.addEventListener('click', () => closeMenu(true));
    menu.addEventListener('click', event => {
        if (event.target.closest('a')) closeMenu();
    });
    document.addEventListener('click', event => {
        if (!menu.hidden && !header.contains(event.target)) closeMenu(true);
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !menu.hidden) {
            event.preventDefault();
            closeMenu(true);
        }
    });
    window.matchMedia('(min-width: 1100px)').addEventListener('change', event => {
        if (event.matches) closeMenu();
    });

    page.querySelectorAll('img[data-fallback]').forEach(image => {
        const useFallback = () => {
            const fallback = image.dataset.fallback;
            if (fallback && image.getAttribute('src') !== fallback) image.src = fallback;
        };
        image.addEventListener('error', useFallback);
        if (image.complete && image.naturalWidth === 0) useFallback();
    });
})();
