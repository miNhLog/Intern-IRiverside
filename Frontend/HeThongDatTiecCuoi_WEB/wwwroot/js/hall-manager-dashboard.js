(() => {
    const root = document.querySelector('.hm-dashboard');
    if (!root) return;
    const input = document.getElementById('scheduleDate');
    const body = document.getElementById('scheduleBody');
    const label = document.getElementById('scheduleDateLabel');
    const message = document.getElementById('scheduleMessage');
    const endpoint = root.dataset.scheduleUrl;
    const stateClass = value => `state-${String(value || '').toLowerCase().replaceAll('_', '-')}`;
    const localDate = value => {
        const [year, month, day] = value.split('-').map(Number);
        return new Date(year, month - 1, day);
    };
    const isoDate = date => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

    document.querySelectorAll('[data-dashboard-reload]').forEach(button =>
        button.addEventListener('click', () => window.location.reload()));

    function appendSlot(cell, slot) {
        const badge = document.createElement('span');
        badge.className = `hm-state ${stateClass(slot.stateCode)}`;
        badge.textContent = slot.stateName;
        cell.appendChild(badge);
        if (slot.bookingCode) {
            const detail = document.createElement('small');
            detail.textContent = `${slot.bookingCode} · ${slot.customerName || ''}`;
            cell.appendChild(detail);
        }
    }

    function render(data) {
        body.replaceChildren();
        if (!data.halls?.length) {
            const row = body.insertRow();
            const cell = row.insertCell();
            cell.colSpan = 3;
            cell.className = 'hm-empty';
            cell.textContent = 'Không có sảnh trong phạm vi phụ trách.';
            return;
        }
        data.halls.forEach(hall => {
            const row = body.insertRow();
            const name = row.insertCell();
            const strong = document.createElement('strong');
            strong.textContent = hall.hallName;
            name.appendChild(strong);
            appendSlot(row.insertCell(), hall.lunch);
            appendSlot(row.insertCell(), hall.dinner);
        });
    }

    async function load(date) {
        message.hidden = true;
        body.classList.add('hm-loading');
        try {
            const response = await fetch(`${endpoint}?date=${encodeURIComponent(date)}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok) {
                const problem = await response.json().catch(() => ({}));
                throw new Error(problem.message || 'Không thể tải lịch sảnh.');
            }
            const data = await response.json();
            render(data);
            label.textContent = new Intl.DateTimeFormat('vi-VN', {
                weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric'
            }).format(localDate(date));
        } catch (error) {
            message.textContent = `${error.message} Vui lòng chọn lại ngày hoặc tải lại trang.`;
            message.hidden = false;
        } finally {
            body.classList.remove('hm-loading');
        }
    }

    input?.addEventListener('change', () => input.value && load(input.value));
    document.querySelectorAll('[data-date-step]').forEach(button => button.addEventListener('click', () => {
        const date = localDate(input.value);
        date.setDate(date.getDate() + Number(button.dataset.dateStep));
        input.value = isoDate(date);
        load(input.value);
    }));
    document.querySelector('[data-today]')?.addEventListener('click', event => {
        input.value = event.currentTarget.dataset.today;
        load(input.value);
    });
})();
