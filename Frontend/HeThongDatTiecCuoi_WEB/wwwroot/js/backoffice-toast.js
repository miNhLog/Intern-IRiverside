(() => {
    'use strict';
    const container = document.getElementById('rpToastContainer');
    if (!container) return;
    const defaults = {
        success: { title: 'Thành công', duration: 4000, icon: 'fa-circle-check' },
        error: { title: 'Có lỗi xảy ra', duration: 8000, icon: 'fa-circle-xmark' },
        warning: { title: 'Cảnh báo', duration: 6000, icon: 'fa-triangle-exclamation' },
        info: { title: 'Thông báo', duration: 5000, icon: 'fa-circle-info' }
    };
    const queue = [];
    const recent = new Map();
    const maxVisible = 3;

    function ensureVisibleHost() {
        const openDialogs = [...document.querySelectorAll('dialog[open]')];
        const host = openDialogs.at(-1) || document.body;
        if (container.parentElement !== host) host.append(container);
    }

    function close(toast) {
        if (!toast || toast.dataset.closing === 'true') return;
        toast.dataset.closing = 'true';
        clearTimeout(toast._timer);
        toast.classList.add('is-leaving');
        const remove = () => { toast.remove(); drain(); };
        toast.addEventListener('transitionend', remove, { once: true });
        setTimeout(remove, 320);
    }

    function schedule(toast, duration) {
        let remaining = duration; let started = Date.now();
        const start = () => { started = Date.now(); toast._timer = setTimeout(() => close(toast), remaining); };
        const pause = () => { clearTimeout(toast._timer); remaining = Math.max(250, remaining - (Date.now() - started)); };
        toast.addEventListener('mouseenter', pause); toast.addEventListener('focusin', pause);
        toast.addEventListener('mouseleave', start); toast.addEventListener('focusout', event => { if (!toast.contains(event.relatedTarget)) start(); });
        start();
    }

    function render(item) {
        ensureVisibleHost();
        const preset = defaults[item.type];
        const toast = document.createElement('div');
        toast.className = `rp-toast rp-toast--${item.type}`;
        toast.setAttribute('role', item.type === 'error' ? 'alert' : 'status');
        toast.setAttribute('aria-live', item.type === 'error' ? 'assertive' : 'polite');
        toast.style.setProperty('--toast-duration', `${item.duration}ms`);
        const icon = document.createElement('span'); icon.className = 'rp-toast__icon'; icon.setAttribute('aria-hidden', 'true');
        const iconGlyph = document.createElement('i'); iconGlyph.className = `fa-solid ${preset.icon}`; icon.append(iconGlyph);
        const content = document.createElement('div'); content.className = 'rp-toast__content';
        const title = document.createElement('strong'); title.className = 'rp-toast__title'; title.textContent = item.title;
        const message = document.createElement('p'); message.className = 'rp-toast__message'; message.textContent = item.message;
        content.append(title, message);
        const button = document.createElement('button'); button.type = 'button'; button.className = 'rp-toast__close'; button.setAttribute('aria-label', 'Đóng thông báo');
        const closeIcon = document.createElement('i'); closeIcon.className = 'fa-solid fa-xmark'; closeIcon.setAttribute('aria-hidden', 'true'); button.append(closeIcon); button.addEventListener('click', () => close(toast));
        toast.append(icon, content, button); container.append(toast);
        requestAnimationFrame(() => toast.classList.add('is-visible'));
        if (item.duration > 0) schedule(toast, item.duration);
    }

    function drain() { while (queue.length && container.children.length < maxVisible) render(queue.shift()); }
    function show(type, message, options = {}) {
        const normalized = String(message ?? '').trim(); if (!normalized) return null;
        type = defaults[type] ? type : 'info';
        const key = options.id || `${type}:${normalized}`; const now = Date.now();
        if (recent.has(key) && now - recent.get(key) < 2500) return null;
        recent.set(key, now); setTimeout(() => recent.delete(key), 3000);
        queue.push({ type, message: normalized, title: options.title || defaults[type].title, duration: Number.isFinite(options.duration) ? options.duration : defaults[type].duration });
        drain(); return key;
    }
    window.RiversideToast = { show, success: (m,o) => show('success',m,o), error: (m,o) => show('error',m,o), warning: (m,o) => show('warning',m,o), info: (m,o) => show('info',m,o), clear: () => { queue.length = 0; [...container.children].forEach(close); } };

    document.addEventListener('close', () => setTimeout(ensureVisibleHost), true);

    document.querySelectorAll('#rpFlashMessages [data-toast-message]').forEach(node => show(node.dataset.toastType || 'info', node.textContent || '', { id: node.dataset.toastId }));
})();
