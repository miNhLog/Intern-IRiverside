(() => {
    'use strict';

    const dateInputs = new Set();

    const formatDate = value => {
        if (!value) return '';
        const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(String(value));
        return match ? `${match[3]}/${match[2]}/${match[1]}` : String(value);
    };

    const syncDate = input => {
        const display = input.closest('.vi-date-field')?.querySelector('.vi-date-display');
        if (!display) return;
        display.value = formatDate(input.value);
        display.placeholder = input.placeholder || 'dd/MM/yyyy';
        display.disabled = input.disabled;
    };

    const enhanceDate = input => {
        if (input.dataset.viDateReady === 'true' || input.closest('.relative')?.querySelector('#newEventDateFormatted')) return;
        input.dataset.viDateReady = 'true';

        const wrapper = document.createElement('span');
        wrapper.className = 'vi-date-field';
        const display = document.createElement('input');
        display.type = 'text';
        display.readOnly = true;
        display.tabIndex = -1;
        display.setAttribute('aria-hidden', 'true');
        display.className = `${input.className || ''} vi-date-display`.trim();

        input.parentNode.insertBefore(wrapper, input);
        wrapper.append(display, input);
        input.className = '';
        input.lang = 'vi-VN';
        input.addEventListener('input', () => syncDate(input));
        input.addEventListener('change', () => syncDate(input));
        input.addEventListener('click', () => {
            if (!input.disabled && typeof input.showPicker === 'function') {
                try { input.showPicker(); } catch { /* Trình duyệt sẽ dùng hành vi native. */ }
            }
        });
        dateInputs.add(input);
        syncDate(input);
    };

    const enhanceAllDates = root => {
        if (root instanceof HTMLInputElement && root.type === 'date') enhanceDate(root);
        root.querySelectorAll?.('input[type="date"]').forEach(enhanceDate);
    };

    document.addEventListener('DOMContentLoaded', () => {
        enhanceAllDates(document);
        new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(node => {
            if (node instanceof Element) enhanceAllDates(node);
        }))).observe(document.body, { childList: true, subtree: true });

        // Một số màn hình gán value bằng JavaScript mà không phát sự kiện change.
        // Đồng bộ nhẹ để phần hiển thị dd/MM/yyyy luôn theo kịp giá trị kỹ thuật yyyy-MM-dd.
        window.setInterval(() => dateInputs.forEach(syncDate), 400);
    });

    window.RiversideFormat = Object.freeze({
        date: formatDate,
        number: value => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(Number(value || 0)),
        integer: value => new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(Number(value || 0)),
        money: value => `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(Number(value || 0))} đ`
    });
})();
