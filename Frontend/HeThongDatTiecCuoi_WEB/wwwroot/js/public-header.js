(() => {
    'use strict';

    const header = document.querySelector('.home-header--overlay');
    if (!header) return;

    let scheduled = false;

    const update = () => {
        header.classList.toggle('is-scrolled', window.scrollY > 40);
        scheduled = false;
    };

    update();
    window.addEventListener('scroll', () => {
        if (scheduled) return;
        scheduled = true;
        window.requestAnimationFrame(update);
    }, { passive: true });
})();
