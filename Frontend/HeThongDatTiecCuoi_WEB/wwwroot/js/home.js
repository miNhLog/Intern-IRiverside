(() => {
    'use strict';

    const page = document.querySelector('.home-page');
    if (!page) return;
    document.body.classList.add('home-public');

    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (!reducedMotion.matches) {
        document.body.classList.add('motion-enabled');
    }
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

    function initCinemaHero() {
        const hero = $('.cinema-hero');
        if (!hero) return;

        const scenes = $$('.cinema-scene', hero);
        const stories = $$('.cinema-story', hero);
        const tabs = $$('.cinema-nav-tab', hero);
        const count = Math.min(scenes.length, stories.length);
        if (count === 0) return;

        let currentIndex = 0;
        let isHovered = false;
        let animFrameId = null;
        let startTime = null;
        const duration = 4000; // 4s per scene (reduced per user request)

        const goToScene = (index) => {
            currentIndex = (index + count) % count;

            scenes.forEach((scene, i) => {
                scene.classList.toggle('is-active', i === currentIndex);
            });

            stories.forEach((story, i) => {
                story.classList.toggle('is-active', i === currentIndex);
            });

            tabs.forEach((tab, i) => {
                const isActive = (i === currentIndex);
                tab.classList.toggle('is-active', isActive);
                tab.setAttribute('aria-selected', String(isActive));
                const fill = $('.cinema-progress-fill', tab);
                if (fill) {
                    fill.style.width = isActive ? '0%' : (i < currentIndex ? '100%' : '0%');
                }
            });

            startTime = performance.now();
        };

        const tick = (now) => {
            if (!isHovered) {
                if (!startTime) startTime = now;
                const elapsed = now - startTime;
                const progress = Math.min(elapsed / duration, 1);

                const activeTab = tabs[currentIndex];
                const activeFill = activeTab ? $('.cinema-progress-fill', activeTab) : null;
                if (activeFill) {
                    activeFill.style.width = `${progress * 100}%`;
                }

                if (elapsed >= duration) {
                    goToScene(currentIndex + 1);
                }
            } else if (isHovered && startTime) {
                startTime = now;
            }
            animFrameId = requestAnimationFrame(tick);
        };

        tabs.forEach((tab, i) => {
            tab.addEventListener('click', () => {
                goToScene(i);
            });
        });

        const interactiveAreas = $$('.cinema-nav, .cinema-actions', hero);
        interactiveAreas.forEach(area => {
            area.addEventListener('mouseenter', () => { isHovered = true; });
            area.addEventListener('mouseleave', () => {
                isHovered = false;
                startTime = performance.now();
            });
        });

        goToScene(0);
        if (count === 1) return;
        animFrameId = requestAnimationFrame(tick);
    }

    function initReveals() {
        const elements = $$('[data-reveal]');
        if (!elements.length) return;
        if (reducedMotion.matches || !('IntersectionObserver' in window)) {
            elements.forEach(el => el.classList.add('is-revealed'));
            return;
        }

        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add('is-revealed');
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.08, rootMargin: '0px 0px -50px 0px' });

        elements.forEach(element => {
            const rect = element.getBoundingClientRect();
            if (rect.top < window.innerHeight - 80 && rect.bottom > 0) {
                element.classList.add('is-revealed');
            } else {
                observer.observe(element);
            }
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
                $$('a', card).forEach(link => {
                    link.tabIndex = position === 0 ? 0 : -1;
                });
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
            if (deck.hasPointerCapture?.(pointerId)) {
                try { deck.releasePointerCapture(pointerId); } catch (_) {}
            }
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
            suppressClick = false;
        });
        deck.addEventListener('pointermove', event => {
            if (pointerId === null || event.pointerId !== pointerId || busy) return;
            deltaX = event.clientX - startX;
            deltaY = event.clientY - startY;
            if (!horizontalDrag && Math.abs(deltaY) > Math.abs(deltaX) && Math.abs(deltaY) > 7) return;
            if (Math.abs(deltaX) > 6) {
                if (!horizontalDrag) {
                    horizontalDrag = true;
                    suppressClick = true;
                    try { deck.setPointerCapture(pointerId); } catch (_) {}
                    order[0]?.classList.add('is-dragging');
                    deck.classList.add('is-dragging');
                }
            }
            if (!horizontalDrag) return;
            if (event.cancelable) event.preventDefault();
            const rotation = clamp(deltaX / 24, -13, 13);
            order[0].style.transform = `translate(${deltaX}px, ${Math.min(Math.abs(deltaX) * -.035, -1)}px) rotate(${rotation}deg) scale(1.018)`;
        });
        deck.addEventListener('pointerup', releasePointer);
        deck.addEventListener('pointercancel', event => {
            resetDrag(order[0]);
            if (pointerId !== null && deck.hasPointerCapture?.(pointerId)) {
                try { deck.releasePointerCapture(pointerId); } catch (_) {}
            }
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

    function initFaq() {
        const section = $('[data-home-faq]');
        if (!section) return;
        const extra = $('#home-faq-extra', section);
        const button = $('[data-faq-toggle]', section);
        const label = $('[data-faq-toggle-label]', section);
        const symbol = $('.home-faq-toggle-symbol', section);
        if (!extra || !button || !label || !symbol) return;

        extra.hidden = true;
        button.hidden = false;
        button.addEventListener('click', () => {
            const expanded = extra.hidden;
            if (!expanded) {
                $$('details', extra).forEach(item => { item.open = false; });
            }
            extra.hidden = !expanded;
            button.setAttribute('aria-expanded', String(expanded));
            label.textContent = expanded ? 'Thu gọn' : 'Xem thêm';
            symbol.textContent = expanded ? '−' : '+';
        });
    }

    const showToast = initToast();
    const closeMobileMenu = initMobileNav();
    initCinemaHero();
    initReveals();
    initImageFallback();
    initHallDeck();
    initFaq();

    $$('[data-coming-soon]').forEach(element => {
        element.addEventListener('click', event => {
            event.preventDefault();
            showToast('Chức năng đang được phát triển.');
            closeMobileMenu();
        });
    });
    $$('[data-open-ai-new]').forEach(button => button.addEventListener('click', () => {
        if (window.AiChatWidget && typeof window.AiChatWidget.open === 'function') window.AiChatWidget.open();
        closeMobileMenu();
    }));
})();
