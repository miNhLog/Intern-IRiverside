(() => {
    const gallery = document.querySelector('[data-hall-gallery]');
    const lightbox = document.querySelector('[data-gallery-lightbox]');
    if (!gallery || !lightbox) return;

    let images = [];
    try {
        images = JSON.parse(gallery.querySelector('[data-gallery-images]')?.textContent || '[]');
    } catch {
        return;
    }
    if (!Array.isArray(images) || images.length === 0) return;

    const mainImage = gallery.querySelector('[data-gallery-main-image]');
    const position = gallery.querySelector('[data-gallery-position]');
    const thumbnails = [...gallery.querySelectorAll('[data-gallery-thumbnail]')];
    const lightboxImage = lightbox.querySelector('[data-gallery-lightbox-image]');
    const lightboxCaption = lightbox.querySelector('[data-gallery-lightbox-caption]');
    const hallName = gallery.dataset.hallName || 'Sảnh tiệc';
    let currentIndex = 0;
    let lastFocusedElement = null;

    const normalizeIndex = index => (index + images.length) % images.length;
    const render = index => {
        currentIndex = normalizeIndex(index);
        mainImage.src = images[currentIndex];
        mainImage.alt = `${hallName} - ảnh ${currentIndex + 1}`;
        lightboxImage.src = images[currentIndex];
        lightboxImage.alt = `${hallName} - ảnh ${currentIndex + 1}`;
        if (position) position.textContent = String(currentIndex + 1);
        lightboxCaption.textContent = `${currentIndex + 1} / ${images.length}`;
        thumbnails.forEach((thumbnail, thumbnailIndex) => {
            const active = thumbnailIndex === currentIndex;
            thumbnail.classList.toggle('active', active);
            thumbnail.setAttribute('aria-current', active ? 'true' : 'false');
            if (active) thumbnail.scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
        });
    };
    const open = () => {
        lastFocusedElement = document.activeElement;
        lightbox.hidden = false;
        lightbox.setAttribute('aria-hidden', 'false');
        document.body.classList.add('hall-gallery-open');
        lightbox.querySelector('[data-gallery-close]')?.focus();
    };
    const close = () => {
        lightbox.hidden = true;
        lightbox.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('hall-gallery-open');
        lastFocusedElement?.focus();
    };

    gallery.querySelector('[data-gallery-open]')?.addEventListener('click', open);
    gallery.querySelector('[data-gallery-previous]')?.addEventListener('click', () => render(currentIndex - 1));
    gallery.querySelector('[data-gallery-next]')?.addEventListener('click', () => render(currentIndex + 1));
    thumbnails.forEach(thumbnail => thumbnail.addEventListener('click', () => render(Number(thumbnail.dataset.index))));
    lightbox.querySelector('[data-gallery-close]')?.addEventListener('click', close);
    lightbox.querySelector('[data-gallery-lightbox-previous]')?.addEventListener('click', () => render(currentIndex - 1));
    lightbox.querySelector('[data-gallery-lightbox-next]')?.addEventListener('click', () => render(currentIndex + 1));
    lightbox.addEventListener('click', event => { if (event.target === lightbox) close(); });
    document.addEventListener('keydown', event => {
        if (lightbox.hidden) return;
        if (event.key === 'Escape') close();
        if (event.key === 'ArrowLeft') render(currentIndex - 1);
        if (event.key === 'ArrowRight') render(currentIndex + 1);
    });
})();
