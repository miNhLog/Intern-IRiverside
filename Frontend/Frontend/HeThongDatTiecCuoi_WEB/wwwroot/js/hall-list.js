(() => {
    const page = document.querySelector('.hall-list-page, .hall-detail-page, .menu-list-page, .menu-detail-page');
    if (!page) return;

    const menuButton = page.querySelector('.mobile-menu-toggle');
    const mobileMenu = page.querySelector('.mobile-nav');
    const mobileBackdrop = page.querySelector('.mobile-nav-backdrop');
    const closeMobileMenu = () => {
        mobileMenu.hidden = true;
        mobileBackdrop.hidden = true;
        menuButton.setAttribute('aria-expanded', 'false');
        menuButton.setAttribute('aria-label', 'Mở menu');
        menuButton.querySelector('.material-symbols-outlined').textContent = 'menu';
    };

    menuButton.addEventListener('click', () => {
        const opening = mobileMenu.hidden;
        mobileMenu.hidden = !opening;
        mobileBackdrop.hidden = !opening;
        menuButton.setAttribute('aria-expanded', String(opening));
        menuButton.setAttribute('aria-label', opening ? 'Đóng menu' : 'Mở menu');
        menuButton.querySelector('.material-symbols-outlined').textContent = opening ? 'close' : 'menu';
    });
    mobileBackdrop.addEventListener('click', closeMobileMenu);

    page.querySelectorAll('img[data-fallback]').forEach(image => {
        const useFallback = () => {
            if (image.src !== image.dataset.fallback) image.src = image.dataset.fallback;
        };
        image.addEventListener('error', useFallback);
        if (image.complete && image.naturalWidth === 0) useFallback();
    });

    page.querySelector('[data-sort-form] select')?.addEventListener('change', event => {
        event.currentTarget.form.submit();
    });

    const toast = page.querySelector('.home-toast');
    let toastTimer;
    page.querySelectorAll('[data-coming-soon], [data-open-ai], [data-toast-message]').forEach(element => {
        element.addEventListener('click', event => {
            event.preventDefault();
            clearTimeout(toastTimer);
            toast.textContent = element.dataset.toastMessage || 'Chức năng đang được phát triển.';
            toast.hidden = false;
            requestAnimationFrame(() => toast.classList.add('visible'));
            toastTimer = setTimeout(() => {
                toast.classList.remove('visible');
                setTimeout(() => { toast.hidden = true; }, 220);
            }, 2800);
            closeMobileMenu();
        });
    });
})();
