(() => {
    const page = document.querySelector('.manager-incident-page');
    if (!page) return;
    const formModal = document.getElementById('mgIncidentFormModal');
    const detailModal = document.getElementById('mgDetailModal');
    const form = document.getElementById('mgIncidentForm');
    const detailBody = document.getElementById('mgDetailBody');
    const errorBox = document.getElementById('mgFormError');
    const idInput = document.getElementById('mgIncidentId');
    const booking = document.getElementById('mgBooking');
    const type = document.getElementById('mgType');
    const severity = document.getElementById('mgSeverity');
    const occurred = document.getElementById('mgOccurred');
    const description = document.getElementById('mgDescription');
    const formTitle = document.getElementById('mgFormTitle');

    const csrf = () => form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const setBodyState = () => document.body.classList.toggle('incident-modal-open', !formModal.hidden || !detailModal.hidden);
    const localNow = () => { const date = new Date(); date.setMinutes(date.getMinutes() - date.getTimezoneOffset()); return date.toISOString().slice(0, 16); };
    const notify = (message, error = false) => error ? window.RiversideToast?.error(message) : window.RiversideToast?.success(message);
    const messageOf = async response => { try { const body = await response.json(); return body.message || 'Yêu cầu không thành công.'; } catch { return 'Không thể kết nối đến hệ thống.'; } };
    const closeForm = () => { formModal.hidden = true; setBodyState(); };
    const closeDetail = () => { detailModal.hidden = true; setBodyState(); };
    const showForm = data => {
        form.reset(); idInput.value = ''; booking.disabled = false; errorBox.hidden = true; occurred.value = localNow();
        formTitle.textContent = 'Tạo báo cáo sự cố'; document.querySelector('#mgSaveIncident span').textContent = 'Lưu báo cáo';
        if (data) {
            idInput.value = data.id; booking.value = data.bookingId; booking.disabled = true; type.value = data.type;
            severity.value = data.severity; occurred.value = data.occurred; description.value = data.description;
            formTitle.textContent = 'Chỉnh sửa báo cáo'; document.querySelector('#mgSaveIncident span').textContent = 'Lưu thay đổi';
        }
        formModal.hidden = false; setBodyState(); setTimeout(() => type.focus(), 30);
    };
    const openDetail = async id => {
        detailModal.hidden = false; setBodyState(); detailBody.className = 'detail-loading';
        detailBody.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Đang tải chi tiết...';
        const response = await fetch(`/quan-ly/su-co/${id}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!response.ok) { detailBody.textContent = await messageOf(response); return false; }
        detailBody.className = ''; detailBody.innerHTML = await response.text(); return true;
    };
    const editCurrent = () => {
        const root = detailBody.querySelector('.manager-incident-detail');
        if (!root) return;
        closeDetail();
        showForm({ id: root.dataset.id, bookingId: root.dataset.bookingId, type: root.dataset.type, severity: root.dataset.severity, occurred: root.dataset.occurred, description: root.dataset.description });
    };
    const post = async (url, data) => {
        data.set('__RequestVerificationToken', csrf());
        const response = await fetch(url, { method: 'POST', body: data, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!response.ok) throw new Error(await messageOf(response));
        return response.json();
    };
    const refreshDetail = (id, message) => { notify(message); const url = new URL(location.href); url.searchParams.set('incidentId', id); setTimeout(() => location.assign(url), 550); };

    document.getElementById('mgOpenCreate')?.addEventListener('click', () => showForm());
    document.querySelectorAll('[data-mg-close-form]').forEach(item => item.addEventListener('click', closeForm));
    document.querySelectorAll('[data-mg-close-detail]').forEach(item => item.addEventListener('click', closeDetail));
    document.addEventListener('keydown', event => { if (event.key === 'Escape') { closeForm(); closeDetail(); } });
    document.addEventListener('click', async event => {
        const view = event.target.closest('.js-mg-detail');
        if (view) await openDetail(view.dataset.id);
        const edit = event.target.closest('.js-mg-edit');
        if (edit && await openDetail(edit.dataset.id)) editCurrent();
        if (event.target.closest('[data-mg-close-detail]')) closeDetail();
        if (event.target.closest('.js-mg-edit-detail')) editCurrent();
        const accept = event.target.closest('.js-mg-accept');
        if (accept) {
            if (!confirm('Manager sẽ tiếp nhận và trực tiếp can thiệp sự cố này. Tiếp tục?')) return;
            accept.disabled = true;
            try { const result = await post(`/quan-ly/su-co/${accept.dataset.id}/accept`, new FormData()); refreshDetail(accept.dataset.id, result.message); }
            catch (error) { notify(error.message, true); accept.disabled = false; }
        }
    });
    document.addEventListener('submit', async event => {
        const resolutionForm = event.target.closest('.mg-resolution-form');
        if (!resolutionForm) return;
        event.preventDefault();
        const action = event.submitter?.dataset.action || 'save';
        const value = resolutionForm.querySelector('textarea').value.trim();
        const box = resolutionForm.querySelector('.resolution-error');
        if (value.length < 3) { box.textContent = 'Vui lòng nhập hướng giải quyết tối thiểu 3 ký tự.'; box.hidden = false; return; }
        if (action === 'resolve' && !confirm('Xác nhận sự cố đã được xử lý hoàn tất?')) return;
        box.hidden = true;
        const buttons = resolutionForm.querySelectorAll('button'); buttons.forEach(button => button.disabled = true);
        try { const result = await post(`/quan-ly/su-co/${resolutionForm.dataset.id}/${action === 'resolve' ? 'resolve' : 'resolution'}`, new FormData(resolutionForm)); refreshDetail(resolutionForm.dataset.id, result.message); }
        catch (error) { box.textContent = error.message; box.hidden = false; buttons.forEach(button => button.disabled = false); }
    });
    form.addEventListener('submit', async event => {
        event.preventDefault(); errorBox.hidden = true;
        if (!form.checkValidity()) { form.reportValidity(); return; }
        const button = document.getElementById('mgSaveIncident'); button.disabled = true;
        const data = new FormData(form); if (booking.disabled) data.set('BookingId', booking.value);
        const id = idInput.value;
        try { const result = await post(id ? `/quan-ly/su-co/${id}/edit` : '/quan-ly/su-co', data); closeForm(); notify(result.message); setTimeout(() => location.reload(), 550); }
        catch (error) { errorBox.textContent = error.message; errorBox.hidden = false; }
        finally { button.disabled = false; }
    });
    const requested = page.dataset.openIncident;
    if (requested && /^\d+$/.test(requested)) openDetail(requested);
})();
