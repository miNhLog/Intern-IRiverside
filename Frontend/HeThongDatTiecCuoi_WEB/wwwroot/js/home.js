(() => {
    'use strict';

    const page = document.querySelector('.home-page');
    if (!page) return;
    document.body.classList.add('home-public');

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    const $ = (selector, root = page) => root.querySelector(selector);
    const $$ = (selector, root = page) => [...root.querySelectorAll(selector)];
    const clamp = (value, min = 0, max = 1) => Math.min(max, Math.max(min, value));

    function initToast() {
        const toast = $('.home-toast');
        let showTimer;
        let hideTimer;
        return message => {
            if (!toast) return;
            clearTimeout(showTimer);
            clearTimeout(hideTimer);
            toast.textContent = message;
            toast.hidden = false;
            requestAnimationFrame(() => toast.classList.add('visible'));
            showTimer = window.setTimeout(() => {
                toast.classList.remove('visible');
                hideTimer = window.setTimeout(() => { toast.hidden = true; }, 220);
            }, 2800);
        };
    }

    function initMobileNav() {
        const button = $('.mobile-menu-toggle');
        const menu = $('.mobile-nav');
        const backdrop = $('.mobile-nav-backdrop');
        const account = $('.account-menu');
        if (!button || !menu || !backdrop) return () => {};

        const close = () => {
            menu.hidden = true;
            backdrop.hidden = true;
            button.setAttribute('aria-expanded', 'false');
            button.setAttribute('aria-label', 'Mở menu');
            const icon = $('.material-symbols-outlined', button);
            if (icon) icon.textContent = 'menu';
        };
        button.addEventListener('click', () => {
            const opening = menu.hidden;
            menu.hidden = !opening;
            backdrop.hidden = !opening;
            button.setAttribute('aria-expanded', String(opening));
            button.setAttribute('aria-label', opening ? 'Đóng menu' : 'Mở menu');
            const icon = $('.material-symbols-outlined', button);
            if (icon) icon.textContent = opening ? 'close' : 'menu';
        });
        backdrop.addEventListener('click', close);
        document.addEventListener('click', event => {
            if (!menu.hidden && !$('.home-header')?.contains(event.target)) close();
        });
        document.addEventListener('keydown', event => {
            if (event.key !== 'Escape') return;
            close();
            if (account) account.removeAttribute('open');
        });
        window.matchMedia('(max-width: 1023px)').addEventListener('change', event => {
            if (!event.matches) close();
        });
        return close;
    }

    function initHeroPortal() {
        const hero = $('[data-portal]');
        if (!hero || reducedMotion.matches) return;

        page.classList.add('motion-enabled');
        let ticking = false;
        const update = () => {
            const maxTravel = Math.max(1, hero.offsetHeight - window.innerHeight);
            const progress = clamp((window.scrollY - hero.offsetTop) / maxTravel);
            page.style.setProperty('--portal-progress', progress.toFixed(4));
            ticking = false;
        };
        const requestUpdate = () => {
            if (ticking) return;
            ticking = true;
            requestAnimationFrame(update);
        };
        window.addEventListener('scroll', requestUpdate, { passive: true });
        window.addEventListener('resize', requestUpdate, { passive: true });
        update();
    }

    function initReveals() {
        const elements = $$('[data-reveal]');
        if (!elements.length || reducedMotion.matches || !('IntersectionObserver' in window)) return;

        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-revealed');
                observer.unobserve(entry.target);
            });
        }, { threshold: .14, rootMargin: '0px 0px -7% 0px' });
        elements.forEach((element, index) => {
            element.style.transitionDelay = `${Math.min(index % 3, 2) * 80}ms`;
            observer.observe(element);
        });
    }

    function initImageFallback() {
        $$('img[data-fallback]').forEach(image => {
            const useFallback = () => {
                const fallback = image.dataset.fallback;
                if (fallback && image.getAttribute('src') !== fallback) image.src = fallback;
            };
            image.addEventListener('error', useFallback);
            if (image.complete && image.naturalWidth === 0) useFallback();
        });
    }

    function initHallDeck() {
        const deck = $('#featuredHallDeck');
        if (!deck) return;
        const cards = $$('[data-hall-card]', deck);
        const previous = $('[data-hall-prev]');
        const next = $('[data-hall-next]');
        const current = $('[data-deck-current]');
        if (!cards.length) return;

        let order = cards.slice();
        let busy = false;
        let pointerId = null;
        let startX = 0;
        let startY = 0;
        let deltaX = 0;
        let deltaY = 0;
        let horizontalDrag = false;
        let suppressClick = false;

        const render = () => {
            order.forEach((card, position) => {
                card.style.setProperty('--stack-index', String(Math.min(position, 3)));
                card.style.zIndex = String(order.length - position);
                card.style.opacity = position > 3 ? '0' : '1';
                card.classList.toggle('is-top', position === 0);
                card.setAttribute('aria-hidden', String(position !== 0));
                const link = $('a', card);
                if (link) link.tabIndex = position === 0 ? 0 : -1;
            });
            if (current) current.textContent = String(Number(order[0].dataset.hallIndex) + 1).padStart(2, '0');
            const disabled = order.length < 2;
            if (previous) previous.disabled = disabled;
            if (next) next.disabled = disabled;
        };

        const resetDrag = card => {
            if (!card) return;
            card.classList.remove('is-dragging');
            card.style.removeProperty('transform');
            card.style.removeProperty('opacity');
            deck.classList.remove('is-dragging');
        };

        const throwTop = (logicalDirection, visualDirection = logicalDirection) => {
            if (busy || order.length < 2) return;
            busy = true;
            const card = order[0];
            const distance = Math.max(deck.clientWidth + 150, 560);
            card.classList.remove('is-dragging');
            card.classList.add('is-throwing');
            card.style.transform = `translate(${visualDirection * distance}px, -34px) rotate(${visualDirection * 14}deg) scale(.98)`;
            card.style.opacity = '0';
            const finish = () => {
                card.classList.remove('is-throwing');
                card.style.removeProperty('transform');
                card.style.removeProperty('opacity');
                if (logicalDirection > 0) order.push(order.shift());
                else order.unshift(order.pop());
                render();
                busy = false;
            };
            window.setTimeout(finish, reducedMotion.matches ? 0 : 430);
        };

        const releasePointer = event => {
            if (pointerId === null || event.pointerId !== pointerId) return;
            const top = order[0];
            const threshold = deck.clientWidth * .11;
            if (horizontalDrag && Math.abs(deltaX) >= threshold) {
                suppressClick = true;
                throwTop(deltaX < 0 ? 1 : -1, deltaX < 0 ? -1 : 1);
            } else {
                resetDrag(top);
            }
            if (deck.hasPointerCapture?.(pointerId)) deck.releasePointerCapture(pointerId);
            pointerId = null;
            horizontalDrag = false;
            window.setTimeout(() => { suppressClick = false; }, 80);
        };

        deck.addEventListener('pointerdown', event => {
            if (busy || order.length < 2 || event.button !== 0) return;
            pointerId = event.pointerId;
            startX = event.clientX;
            startY = event.clientY;
            deltaX = 0;
            deltaY = 0;
            horizontalDrag = false;
            deck.setPointerCapture(pointerId);
            order[0].classList.add('is-dragging');
            deck.classList.add('is-dragging');
        });
        deck.addEventListener('pointermove', event => {
            if (pointerId === null || event.pointerId !== pointerId || busy) return;
            deltaX = event.clientX - startX;
            deltaY = event.clientY - startY;
            if (!horizontalDrag && Math.abs(deltaY) > Math.abs(deltaX) && Math.abs(deltaY) > 7) return;
            if (Math.abs(deltaX) > 6) horizontalDrag = true;
            if (!horizontalDrag) return;
            if (event.cancelable) event.preventDefault();
            suppressClick = true;
            const rotation = clamp(deltaX / 24, -13, 13);
            order[0].style.transform = `translate(${deltaX}px, ${Math.min(Math.abs(deltaX) * -.035, -1)}px) rotate(${rotation}deg) scale(1.018)`;
        });
        deck.addEventListener('pointerup', releasePointer);
        deck.addEventListener('pointercancel', event => {
            resetDrag(order[0]);
            if (pointerId !== null && deck.hasPointerCapture?.(pointerId)) deck.releasePointerCapture(pointerId);
            pointerId = null;
            horizontalDrag = false;
        });
        deck.addEventListener('click', event => {
            if (!suppressClick) return;
            event.preventDefault();
            event.stopPropagation();
        }, true);
        deck.addEventListener('keydown', event => {
            if (event.key === 'ArrowLeft') {
                event.preventDefault();
                throwTop(-1, -1);
            } else if (event.key === 'ArrowRight') {
                event.preventDefault();
                throwTop(1, 1);
            }
        });
        previous?.addEventListener('click', () => throwTop(-1, -1));
        next?.addEventListener('click', () => throwTop(1, 1));
        render();
    }

    const showToast = initToast();
    const closeMobileMenu = initMobileNav();
    initHeroPortal();
    initReveals();
    initImageFallback();
    initHallDeck();

    $$('[data-coming-soon]').forEach(element => {
        element.addEventListener('click', event => {
            event.preventDefault();
            showToast('Chức năng đang được phát triển.');
            closeMobileMenu();
        });
    });
    $('[data-scroll-inquiry]')?.addEventListener('click', event => {
        const hero = $('[data-portal]');
        const inquiry = $('#booking-inquiry');
        if (!hero || !inquiry || reducedMotion.matches) return;
        event.preventDefault();
        const maxTravel = Math.max(0, hero.offsetHeight - window.innerHeight);
        window.scrollTo({ top: hero.offsetTop + maxTravel * .92, behavior: 'auto' });
    });
    $('[data-search]')?.addEventListener('click', () => showToast('Chức năng tìm kiếm sảnh đang được phát triển.'));
    $$('[data-open-ai-new]').forEach(button => button.addEventListener('click', () => {
        if (window.AiChatWidget && typeof window.AiChatWidget.open === 'function') window.AiChatWidget.open();
        closeMobileMenu();
    }));
})();
