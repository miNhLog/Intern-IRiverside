(() => {
    const root = document.querySelector('[data-page-root]');
    if (!root) return;

    const base = '/quan-ly/hop-dong-thanh-toan';
    const token = document.querySelector('#cpAntiforgery input[name="__RequestVerificationToken"]')?.value || '';
    const state = { contractPage: 1, paymentPage: 1, paymentsLoaded: false, selectedBooking: null, currentContract: null, confirmCallback: null };
    const byId = id => document.getElementById(id);
    const money = value => `${Number(value || 0).toLocaleString('vi-VN')} đ`;
    const date = value => value ? new Date(value).toLocaleDateString('vi-VN') : '—';
    const percentage = value => Number(value || 0).toLocaleString('vi-VN', { maximumFractionDigits: 1 });
    const createNode = (tag, className, text) => {
        const element = document.createElement(tag);
        if (className) element.className = className;
        if (text !== undefined && text !== null) element.textContent = text;
        return element;
    };

    async function request(url, options = {}) {
        const headers = { ...(options.headers || {}) };
        if (options.body !== undefined) headers['Content-Type'] = 'application/json';
        if (options.method && options.method !== 'GET') headers.RequestVerificationToken = token;
        const response = await fetch(url, { ...options, headers });
        let payload = null;
        try { payload = await response.json(); } catch { /* PDF and empty bodies are not used here. */ }
        if (!response.ok || payload?.success === false) throw new Error(payload?.message || 'Yêu cầu không thành công.');
        return payload?.data ?? payload;
    }

    function showToast(message, isError = false) {
        const toast = byId('cpToast');
        toast.textContent = message;
        toast.classList.toggle('error', isError);
        toast.classList.add('open');
        clearTimeout(showToast.timer);
        showToast.timer = setTimeout(() => toast.classList.remove('open'), 3500);
    }

    function statusBadge(code, name) {
        const normalized = String(code || '').toLowerCase();
        const css = ['draft', 'active', 'completed', 'cancelled', 'pending', 'success', 'failed'].includes(normalized) ? normalized : 'draft';
        return createNode('span', `cp-badge ${css}`, name || code || '—');
    }

    function queryString(values) {
        const params = new URLSearchParams();
        Object.entries(values).forEach(([key, value]) => {
            if (value !== null && value !== undefined && String(value).trim() !== '' && value !== 'ALL') params.set(key, value);
        });
        return params.toString();
    }

    async function loadContracts(page = 1) {
        state.contractPage = page;
        try {
            const data = await request(`${base}/contracts?${queryString({
                keyword: byId('contractKeyword').value,
                status: byId('contractStatus').value,
                fromDate: byId('contractFromDate').value,
                toDate: byId('contractToDate').value,
                page
            })}`);
            renderContractRows(data.items || []);
            byId('kpiTotal').textContent = data.statistics.totalContracts;
            byId('kpiActive').textContent = data.statistics.activeContracts;
            byId('kpiAwaiting').textContent = data.statistics.awaitingPaymentContracts;
            byId('kpiRevenue').textContent = money(data.statistics.totalCollectedRevenue);
            byId('contractPageInfo').textContent = `Trang ${Math.max(1, data.page)} / ${Math.max(1, data.totalPages)} · ${data.totalItems} hợp đồng`;
            renderPages(byId('contractPages'), data.page, data.totalPages, loadContracts);
        } catch (error) { showToast(error.message, true); }
    }

    function renderContractRows(items) {
        const body = byId('contractRows');
        body.replaceChildren();
        if (!items.length) {
            const row = body.insertRow();
            const cell = row.insertCell();
            cell.colSpan = 8; cell.className = 'cp-empty'; cell.textContent = 'Chưa có hợp đồng phù hợp.';
            return;
        }
        items.forEach(item => {
            const row = body.insertRow();
            row.dataset.contractId = item.contractId;
            row.addEventListener('click', () => openDrawer(item.contractId));
            const codeCell = row.insertCell(); codeCell.append(createNode('span', 'cp-code', item.contractCode));
            const customer = row.insertCell(); customer.className = 'cp-customer'; customer.append(createNode('strong', '', item.customerName), createNode('small', '', item.customerPhone));
            const booking = row.insertCell(); booking.append(createNode('span', 'cp-code', item.bookingCode));
            row.insertCell().textContent = date(item.createdDate);
            const total = row.insertCell(); total.className = 'cp-money'; total.textContent = money(item.totalValue);
            row.insertCell().append(statusBadge(item.statusCode, item.statusName));
            const progressCell = row.insertCell();
            const progress = createNode('div', 'cp-progress');
            const track = createNode('div', 'cp-progress-track');
            const bar = createNode('div', 'cp-progress-bar');
            bar.style.width = `${Math.max(0, Math.min(100, Number(item.paymentPercentage || 0)))}%`;
            track.append(bar);
            const meta = createNode('div', 'cp-progress-meta');
            meta.append(createNode('span', '', `Đã thu ${money(item.paidAmount)}`), createNode('b', '', `${percentage(item.paymentPercentage)}%`));
            progress.append(track, meta);
            if (item.hasPaymentInconsistency) progress.append(createNode('div', 'cp-warning', 'Thanh toán vượt giá trị'));
            progressCell.append(progress);
            const actions = row.insertCell();
            const view = createNode('button', 'cp-icon-btn'); view.type = 'button'; view.title = 'Xem chi tiết';
            const icon = createNode('i', 'fa-regular fa-eye'); view.append(icon);
            view.addEventListener('click', event => { event.stopPropagation(); openDrawer(item.contractId); });
            actions.append(view);
        });
    }

    async function loadPayments(page = 1) {
        state.paymentPage = page;
        try {
            const data = await request(`${base}/payments?${queryString({
                keyword: byId('paymentKeyword').value,
                type: byId('paymentType').value,
                status: byId('paymentStatus').value,
                fromDate: byId('paymentFromDate').value,
                toDate: byId('paymentToDate').value,
                page
            })}`);
            state.paymentsLoaded = true;
            renderPaymentRows(data.items || []);
            byId('paymentPageInfo').textContent = `Trang ${Math.max(1, data.page)} / ${Math.max(1, data.totalPages)} · ${data.totalItems} giao dịch · Chỉ đọc`;
            renderPages(byId('paymentPages'), data.page, data.totalPages, loadPayments);
        } catch (error) { showToast(error.message, true); }
    }

    function renderPaymentRows(items) {
        const body = byId('paymentRows'); body.replaceChildren();
        if (!items.length) {
            const row = body.insertRow(); const cell = row.insertCell(); cell.colSpan = 9; cell.className = 'cp-empty'; cell.textContent = 'Chưa có giao dịch phù hợp.'; return;
        }
        items.forEach(item => {
            const row = body.insertRow(); row.addEventListener('click', () => openDrawer(item.contractId, 'payments'));
            row.insertCell().append(createNode('span', 'cp-code', item.transactionCode || `#${item.paymentId}`));
            row.insertCell().append(createNode('span', 'cp-code', item.contractCode));
            row.insertCell().append(createNode('span', 'cp-code', item.bookingCode));
            const customer = row.insertCell(); customer.className = 'cp-customer'; customer.append(createNode('strong', '', item.customerName), createNode('small', '', item.customerPhone));
            row.insertCell().textContent = item.paymentType;
            const amount = row.insertCell(); amount.className = 'cp-money'; amount.textContent = money(item.amount);
            row.insertCell().textContent = date(item.paymentDate);
            row.insertCell().textContent = item.paymentMethod || '—';
            row.insertCell().append(statusBadge(item.statusCode, item.statusName));
        });
    }

    function renderPages(container, current, total, callback) {
        container.replaceChildren(); total = Math.max(1, total || 1); current = Math.max(1, current || 1);
        const add = (label, page, disabled, active = false) => {
            const button = createNode('button', `cp-page-btn${active ? ' active' : ''}`, label);
            button.type = 'button'; button.disabled = disabled; if (!disabled) button.addEventListener('click', () => callback(page)); container.append(button);
        };
        add('‹', current - 1, current === 1);
        const first = Math.max(1, Math.min(current - 2, total - 4));
        const last = Math.min(total, first + 4);
        for (let page = first; page <= last; page++) add(String(page), page, false, page === current);
        add('›', current + 1, current === total);
    }

    async function openDrawer(contractId, targetTab = 'general') {
        byId('drawerOverlay').classList.add('open'); byId('contractDrawer').classList.add('open');
        byId('drawerLoading').classList.remove('cp-hidden');
        ['drawerGeneral', 'drawerBooking', 'drawerPayments', 'drawerHistory'].forEach(id => byId(id).replaceChildren());
        try {
            const detail = await request(`${base}/contracts/${contractId}`);
            state.currentContract = detail;
            renderDrawer(detail);
            switchDrawerTab(targetTab);
        } catch (error) { showToast(error.message, true); closeDrawer(); }
    }

    function renderDrawer(detail) {
        byId('drawerLoading').classList.add('cp-hidden');
        byId('drawerCode').textContent = detail.contractCode;
        byId('drawerMeta').textContent = `Ngày lập ${date(detail.createdDate)}`;
        const status = byId('drawerStatus'); status.replaceChildren(statusBadge(detail.statusCode, detail.statusName));

        const general = byId('drawerGeneral');
        general.append(detailCard('Thông tin chung', [
            ['Mã hợp đồng', detail.contractCode], ['Mã booking', detail.booking.bookingCode], ['Khách hàng', detail.booking.customerName],
            ['Số điện thoại', detail.booking.customerPhone], ['Email', detail.booking.customerEmail || 'Chưa cung cấp'],
            ['Ngày tạo', date(detail.createdDate)], ['Tổng giá trị', money(detail.totalValue)], ['Trạng thái', detail.statusName]
        ]));
        general.append(textCard('Nội dung hợp đồng', detail.contractContent), textCard('Điều khoản thanh toán', detail.paymentTerms));

        const booking = byId('drawerBooking');
        booking.append(detailCard('Booking liên quan', [
            ['Mã booking', detail.booking.bookingCode], ['Ngày / ca', `${date(detail.booking.eventDate)} · ${detail.booking.shift}`],
            ['Sảnh', detail.booking.hallName], ['Số khách', String(detail.booking.guestCount)], ['Thực đơn', detail.booking.menuName || 'Chưa chọn'],
            ['Trang trí', detail.booking.decorationPackageName || 'Chưa chọn'], ['Dịch vụ', detail.booking.services?.length ? detail.booking.services.join(', ') : 'Không có'],
            ['Dự toán', money(detail.booking.estimatedTotal)], ['Trạng thái booking', detail.booking.statusName]
        ]));
        const bookingLink = createNode('a', 'cp-secondary', 'Xem Booking'); bookingLink.href = '/AdminBooking'; bookingLink.style.marginTop = '4px'; booking.append(bookingLink);

        renderDrawerPayments(detail);
        renderHistory(detail.history || []);
        renderDrawerActions(detail);
    }

    function detailCard(title, pairs) {
        const card = createNode('article', 'cp-detail-card'); card.append(createNode('h3', '', title));
        const list = createNode('dl', 'cp-detail-grid');
        pairs.forEach(([label, value]) => { list.append(createNode('dt', '', label), createNode('dd', '', value ?? '—')); });
        card.append(list); return card;
    }

    function textCard(title, text) {
        const card = createNode('article', 'cp-detail-card'); card.append(createNode('h3', '', title), createNode('p', 'cp-pre', text || 'Chưa có nội dung.')); return card;
    }

    function renderDrawerPayments(detail) {
        const section = byId('drawerPayments');
        const summary = createNode('article', 'cp-detail-card'); summary.append(createNode('h3', '', 'Tiến độ thanh toán'));
        const track = createNode('div', 'cp-progress-track'); const bar = createNode('div', 'cp-progress-bar');
        bar.style.width = `${Math.max(0, Math.min(100, Number(detail.paymentPercentage || 0)))}%`; track.append(bar); summary.append(track);
        const values = createNode('div', 'cp-payment-summary');
        [['Đã thanh toán', money(detail.paidAmount)], ['Còn lại', money(detail.remainingBalance)], ['Tổng giá trị', money(detail.totalValue)]].forEach(([label, value]) => {
            const item = createNode('div'); item.append(createNode('span', '', label), createNode('strong', '', value)); values.append(item);
        }); summary.append(values);
        if (detail.hasPaymentInconsistency) summary.append(createNode('p', 'cp-warning', 'Tổng thanh toán vượt giá trị hợp đồng. Vui lòng kiểm tra dữ liệu.'));
        else if (detail.statusCode === 'ACTIVE' && detail.remainingBalance > 0) summary.append(createNode('p', 'cp-warning', `Còn ${money(detail.remainingBalance)} chưa thanh toán.`));
        section.append(summary);
        const history = createNode('article', 'cp-detail-card'); history.append(createNode('h3', '', 'Lịch sử giao dịch (chỉ đọc)'));
        if (!detail.payments?.length) history.append(createNode('p', 'cp-pre', 'Chưa phát sinh giao dịch thanh toán.'));
        else detail.payments.forEach(payment => history.append(detailCard(`${payment.paymentType} · ${money(payment.amount)}`, [
            ['Mã giao dịch', payment.transactionCode || `#${payment.paymentId}`], ['Ngày', date(payment.paymentDate)],
            ['Phương thức', payment.paymentMethod || '—'], ['Trạng thái', payment.statusName]
        ])));
        section.append(history);
    }

    function renderHistory(items) {
        const section = byId('drawerHistory'); const card = createNode('article', 'cp-detail-card'); card.append(createNode('h3', '', 'Lịch sử nghiệp vụ'));
        const timeline = createNode('div', 'cp-timeline');
        if (!items.length) timeline.append(createNode('p', 'cp-pre', 'Chưa có lịch sử.'));
        const names = { CONTRACT_CREATED: 'Tạo hợp đồng', CONTRACT_UPDATED: 'Cập nhật bản nháp', CONTRACT_ACTIVATED: 'Kích hoạt hợp đồng', CONTRACT_COMPLETED: 'Hoàn tất hợp đồng', CONTRACT_CANCELLED: 'Hủy hợp đồng' };
        items.forEach(item => { const event = createNode('div', 'cp-timeline-item'); event.append(createNode('strong', '', names[item.action] || item.action), createNode('span', '', `${new Date(item.timestamp).toLocaleString('vi-VN')} · ${item.notes || ''}`)); timeline.append(event); });
        card.append(timeline); section.append(card);
    }

    function renderDrawerActions(detail) {
        const actions = byId('drawerActions'); actions.replaceChildren();
        const pdf = createNode('a', 'cp-secondary', 'In / Tải PDF'); pdf.href = `${base}/contracts/${detail.contractId}/pdf`; pdf.target = '_blank';
        const pdfIcon = createNode('i', 'fa-solid fa-print'); pdf.prepend(pdfIcon); actions.append(pdf);
        if (detail.statusCode === 'DRAFT') {
            actions.append(actionButton('Sửa hợp đồng', 'cp-secondary', () => openEdit(detail)), actionButton('Kích hoạt', 'cp-primary', () => confirmActivate(detail)), actionButton('Hủy hợp đồng', 'cp-danger', () => confirmCancel(detail)));
        } else if (detail.statusCode === 'ACTIVE') {
            if (!detail.hasPaymentInconsistency && Number(detail.paidAmount) === Number(detail.totalValue)) actions.append(actionButton('Hoàn tất hợp đồng', 'cp-primary', () => confirmComplete(detail)));
            actions.append(actionButton('Hủy hợp đồng', 'cp-danger', () => confirmCancel(detail)));
        }
    }

    function actionButton(label, className, handler) { const button = createNode('button', className, label); button.type = 'button'; button.addEventListener('click', handler); return button; }
    function closeDrawer() { byId('drawerOverlay').classList.remove('open'); byId('contractDrawer').classList.remove('open'); state.currentContract = null; }
    function switchDrawerTab(tab) {
        document.querySelectorAll('[data-drawer-tab]').forEach(button => button.classList.toggle('active', button.dataset.drawerTab === tab));
        const map = { general: 'drawerGeneral', booking: 'drawerBooking', payments: 'drawerPayments', history: 'drawerHistory' };
        Object.entries(map).forEach(([key, id]) => byId(id).classList.toggle('cp-hidden', key !== tab));
    }

    async function openCreateModal() { openModal('createModal'); await loadEligibleBookings(); }
    async function loadEligibleBookings(keyword = '') {
        const container = byId('eligibleBookings'); container.replaceChildren(createNode('div', 'cp-empty', 'Đang tải booking đủ điều kiện...'));
        try {
            const bookings = await request(`${base}/eligible-bookings?${queryString({ keyword })}`);
            container.replaceChildren();
            if (!bookings.length) { container.append(createNode('div', 'cp-empty', 'Không có booking đã xác nhận chưa có hợp đồng.')); return; }
            bookings.forEach(booking => {
                const button = createNode('button', 'cp-booking-option'); button.type = 'button'; button.dataset.bookingId = booking.bookingId;
                button.append(createNode('strong', '', `${booking.bookingCode} · ${booking.customerName}`), createNode('span', '', `${booking.customerPhone} · ${date(booking.eventDate)} · ${booking.hallName}`));
                button.addEventListener('click', () => selectBooking(booking, button)); container.append(button);
            });
            const initialId = Number(root.dataset.initialBookingId || 0);
            if (initialId) { const found = bookings.find(item => item.bookingId === initialId); const button = container.querySelector(`[data-booking-id="${initialId}"]`); if (found && button) selectBooking(found, button); }
        } catch (error) { container.replaceChildren(createNode('div', 'cp-empty', error.message)); }
    }

    function selectBooking(booking, button) {
        state.selectedBooking = booking;
        document.querySelectorAll('.cp-booking-option').forEach(item => item.classList.toggle('selected', item === button));
        const summary = byId('selectedBookingSummary'); summary.classList.remove('cp-hidden'); summary.replaceChildren();
        summary.append(createNode('strong', '', `${booking.bookingCode} · ${booking.customerName}`), document.createElement('br'),
            document.createTextNode(`${booking.customerPhone} · ${booking.customerEmail || 'Chưa có email'}`), document.createElement('br'),
            document.createTextNode(`${date(booking.eventDate)} · ${booking.shift} · ${booking.hallName} · ${booking.guestCount} khách`), document.createElement('br'),
            document.createTextNode(`Thực đơn: ${booking.menuName || 'Chưa chọn'} · Decor: ${booking.decorationPackageName || 'Chưa chọn'}`));
        byId('createTotalValue').value = booking.estimatedTotal ?? 0;
        byId('createContent').value = booking.defaultContractContent || '';
        byId('createTerms').value = booking.defaultPaymentTerms || '';
    }

    async function saveDraft() {
        if (!state.selectedBooking) return showToast('Vui lòng chọn booking.', true);
        try {
            const result = await request(`${base}/contracts`, { method: 'POST', body: JSON.stringify({ bookingId: state.selectedBooking.bookingId, totalValue: Number(byId('createTotalValue').value || 0), contractContent: byId('createContent').value, paymentTerms: byId('createTerms').value }) });
            closeModal('createModal'); showToast(result.message || 'Đã lưu hợp đồng bản nháp.'); state.selectedBooking = null; root.dataset.initialBookingId = ''; await loadContracts(1); await openDrawer(result.contractId);
        } catch (error) { showToast(error.message, true); }
    }

    function openEdit(detail) { byId('editTotalValue').value = detail.totalValue; byId('editContent').value = detail.contractContent; byId('editTerms').value = detail.paymentTerms; openModal('editModal'); }
    async function saveEdit() {
        if (!state.currentContract) return;
        try {
            const result = await request(`${base}/contracts/${state.currentContract.contractId}`, { method: 'PUT', body: JSON.stringify({ totalValue: Number(byId('editTotalValue').value || 0), contractContent: byId('editContent').value, paymentTerms: byId('editTerms').value }) });
            closeModal('editModal'); showToast(result.message); await loadContracts(state.contractPage); await openDrawer(state.currentContract.contractId);
        } catch (error) { showToast(error.message, true); }
    }

    function showConfirm(title, message, callback, danger = false) { byId('confirmTitle').textContent = title; byId('confirmMessage').textContent = message; state.confirmCallback = callback; byId('confirmAction').className = danger ? 'cp-danger' : 'cp-primary'; openModal('confirmModal'); }
    function confirmActivate(detail) { showConfirm('Kích hoạt hợp đồng', 'Khi kích hoạt, hợp đồng sẽ không thể chỉnh sửa nội dung hoặc tổng giá trị. Bạn có chắc chắn muốn tiếp tục?', () => runCommand(detail.contractId, 'activate')); }
    function confirmComplete(detail) { showConfirm('Hoàn tất hợp đồng', `Khách hàng đã thanh toán đủ ${money(detail.totalValue)}. Xác nhận hoàn tất hợp đồng?`, () => runCommand(detail.contractId, 'complete')); }
    function confirmCancel(detail) { const warning = detail.paidAmount > 0 ? ' Hợp đồng đã phát sinh thanh toán thành công. Việc hủy không xóa hoặc hoàn tiền các giao dịch đã ghi nhận.' : ''; showConfirm('Hủy hợp đồng', `Bạn có chắc chắn muốn hủy ${detail.contractCode}? Booking không bị hủy.${warning}`, () => runCommand(detail.contractId, 'cancel'), true); }
    async function runCommand(id, command) { closeModal('confirmModal'); try { const result = await request(`${base}/contracts/${id}/${command}`, { method: 'POST' }); showToast(result.message); await loadContracts(state.contractPage); await openDrawer(id); } catch (error) { showToast(error.message, true); } }

    function openModal(id) { byId(id).classList.add('open'); }
    function closeModal(id) { byId(id).classList.remove('open'); if (id === 'confirmModal') state.confirmCallback = null; }
    function debounce(handler, delay = 350) { let timer; return (...args) => { clearTimeout(timer); timer = setTimeout(() => handler(...args), delay); }; }

    document.querySelectorAll('[data-main-tab]').forEach(button => button.addEventListener('click', () => {
        document.querySelectorAll('[data-main-tab]').forEach(item => item.classList.toggle('active', item === button));
        const payments = button.dataset.mainTab === 'payments'; byId('contractTab').classList.toggle('cp-hidden', payments); byId('paymentTab').classList.toggle('cp-hidden', !payments); if (payments && !state.paymentsLoaded) loadPayments(1);
    }));
    document.querySelectorAll('[data-drawer-tab]').forEach(button => button.addEventListener('click', () => switchDrawerTab(button.dataset.drawerTab)));
    document.querySelectorAll('[data-close-modal]').forEach(button => button.addEventListener('click', () => closeModal(button.dataset.closeModal)));
    [byId('contractStatus'), byId('contractFromDate'), byId('contractToDate')].forEach(element => element.addEventListener('change', () => loadContracts(1)));
    byId('contractKeyword').addEventListener('input', debounce(() => loadContracts(1)));
    [byId('paymentType'), byId('paymentStatus'), byId('paymentFromDate'), byId('paymentToDate')].forEach(element => element.addEventListener('change', () => loadPayments(1)));
    byId('paymentKeyword').addEventListener('input', debounce(() => loadPayments(1)));
    byId('eligibleKeyword').addEventListener('input', debounce(event => loadEligibleBookings(event.target.value)));
    byId('openCreateContract').addEventListener('click', openCreateModal); byId('saveDraft').addEventListener('click', saveDraft); byId('saveEdit').addEventListener('click', saveEdit);
    byId('closeDrawer').addEventListener('click', closeDrawer); byId('drawerOverlay').addEventListener('click', closeDrawer);
    byId('confirmAction').addEventListener('click', () => { const callback = state.confirmCallback; if (callback) callback(); });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') { closeDrawer(); document.querySelectorAll('.cp-modal.open').forEach(modal => closeModal(modal.id)); } });

    loadContracts(1);
    if (root.dataset.openCreate === 'true' || root.dataset.initialBookingId) openCreateModal();
    else if (Number(root.dataset.initialContractId || 0)) openDrawer(Number(root.dataset.initialContractId));
})();
