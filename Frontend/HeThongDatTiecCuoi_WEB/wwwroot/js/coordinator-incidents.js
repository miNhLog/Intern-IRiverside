(() => {
    const page = document.querySelector('.incident-page');
    if (!page) return;
    const formModal = document.getElementById('incidentFormModal');
    const detailModal = document.getElementById('incidentDetailModal');
    const form = document.getElementById('incidentForm');
    const errorBox = document.getElementById('incidentFormError');
    const idInput = document.getElementById('incidentId');
    const title = document.getElementById('incidentFormTitle');
    const booking = document.getElementById('incidentBooking');
    const type = document.getElementById('incidentType');
    const severity = document.getElementById('incidentSeverity');
    const occurred = document.getElementById('incidentOccurredAt');
    const description = document.getElementById('incidentDescription');
    let formTrigger = null;
    let detailTrigger = null;

    const localDateTime = () => {
        const date = new Date();
        date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
        return date.toISOString().slice(0, 16);
    };
    const setOpenState = () => document.body.classList.toggle('incident-modal-open', !formModal.hidden || !detailModal.hidden);
    const openForm = (data) => {
        formTrigger = document.activeElement;
        form.reset(); errorBox.hidden = true; idInput.value = '';
        booking.disabled = false; title.textContent = 'Báo cáo sự cố'; occurred.value = localDateTime();
        document.querySelector('#saveIncident span').textContent = 'Gửi báo cáo';
        if (data) {
            idInput.value = data.id; booking.value = data.bookingId; booking.disabled = true;
            type.value = data.type; severity.value = data.severity; occurred.value = data.occurred;
            description.value = data.description; title.textContent = 'Chỉnh sửa báo cáo';
            document.querySelector('#saveIncident span').textContent = 'Lưu thay đổi';
        }
        formModal.hidden = false; setOpenState(); setTimeout(() => type.focus(), 30);
    };
    const closeForm = () => {
        if (formModal.hidden) return;
        formModal.hidden = true; setOpenState();
        if (formTrigger instanceof HTMLElement) formTrigger.focus();
    };
    const closeDetail = () => {
        if (detailModal.hidden) return;
        detailModal.hidden = true; setOpenState();
        if (detailTrigger instanceof HTMLElement) detailTrigger.focus();
    };
    const notify = (message, isError = false) => isError
        ? window.RiversideToast?.error(message)
        : window.RiversideToast?.success(message);
    const readMessage = async response => {
        try { const body = await response.json(); return body.message || 'Yêu cầu không thành công.'; }
        catch { return 'Không thể kết nối đến hệ thống.'; }
    };
    const showDetailMessage = (iconClass, message) => {
        const body = document.getElementById('incidentDetailBody');
        const icon = document.createElement('i');
        icon.className = iconClass;
        body.className = 'detail-loading';
        body.replaceChildren(icon, document.createTextNode(` ${message}`));
    };
    const openDetail = async id => {
        detailTrigger = document.activeElement;
        detailModal.hidden = false; setOpenState();
        showDetailMessage('fa-solid fa-spinner fa-spin', 'Đang tải chi tiết...');
        try {
            const response = await fetch(`/dieu-phoi/su-co/${id}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) { showDetailMessage('fa-solid fa-circle-exclamation', await readMessage(response)); return; }
            const body = document.getElementById('incidentDetailBody');
            body.className = ''; body.innerHTML = await response.text();
            body.querySelector('[data-close-detail]')?.focus();
        } catch {
            showDetailMessage('fa-solid fa-circle-exclamation', 'Không thể kết nối đến hệ thống. Vui lòng thử lại.');
        }
    };
    const editFromDetail = () => {
        const root = document.querySelector('.incident-detail');
        const button = root?.querySelector('.js-edit-from-detail');
        if (!root || !button) return;
        const data = { id: button.dataset.id, bookingId: root.dataset.bookingId, type: root.dataset.type,
            severity: root.dataset.severity, occurred: root.dataset.occurred, description: root.dataset.description };
        closeDetail(); openForm(data);
    };

    document.getElementById('openCreateIncident')?.addEventListener('click', () => openForm());
    document.querySelectorAll('[data-close-modal]').forEach(button => button.addEventListener('click', closeForm));
    document.querySelectorAll('[data-close-detail]').forEach(button => button.addEventListener('click', closeDetail));
    document.addEventListener('click', event => {
        const detail = event.target.closest('.js-detail'); if (detail) openDetail(detail.dataset.id);
        const edit = event.target.closest('.js-edit'); if (edit) {
            openDetail(edit.dataset.id).then(() => editFromDetail());
        }
        if (event.target.closest('[data-close-detail]')) closeDetail();
        if (event.target.closest('.js-edit-from-detail')) editFromDetail();
    });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') { closeForm(); closeDetail(); } });

    form.addEventListener('submit', async event => {
        event.preventDefault(); errorBox.hidden = true;
        if (!form.checkValidity()) { form.reportValidity(); return; }
        const submit = document.getElementById('saveIncident'); submit.disabled = true;
        const payload = new FormData(form); if (booking.disabled) payload.set('BookingId', booking.value);
        const id = idInput.value; const url = id ? `/dieu-phoi/su-co/${id}` : '/dieu-phoi/su-co';
        try {
            const response = await fetch(url, { method: 'POST', body: payload, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) { errorBox.textContent = await readMessage(response); errorBox.hidden = false; return; }
            const result = await response.json(); closeForm(); notify(result.message || 'Đã lưu báo cáo sự cố.');
            setTimeout(() => window.location.reload(), 650);
        } catch { errorBox.textContent = 'Không thể kết nối đến hệ thống. Vui lòng thử lại.'; errorBox.hidden = false; }
        finally { submit.disabled = false; }
    });

    const requestedId = page.dataset.openIncident;
    if (requestedId && /^\d+$/.test(requestedId)) openDetail(requestedId);
    else if (page.dataset.bookingId && Array.from(booking.options).some(option => option.value === page.dataset.bookingId)) {
        openForm();
        booking.value = page.dataset.bookingId;
    }
})();
