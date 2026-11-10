(() => {
    const page = document.querySelector('[data-event-page]');
    if (!page) return;
    const drawer = document.getElementById('eventDrawer');
    const overlay = document.getElementById('eventDrawerOverlay');
    const drawerContent = document.getElementById('eventDrawerContent');
    const calendarContent = document.getElementById('calendarContent');
    const weekLabel = document.getElementById('weekLabel');
    let weekStart = mondayOf(new Date());

    // Reassign Modal Elements
    const reassignModal = document.getElementById('reassignModal');
    const reassignOverlay = document.getElementById('reassignModalOverlay');
    const reassignForm = document.getElementById('reassignForm');
    const reassignBookingId = document.getElementById('reassignBookingId');
    const reassignBookingCode = document.getElementById('reassignBookingCode');
    const reassignEventTime = document.getElementById('reassignEventTime');
    const reassignCurrentCoordinator = document.getElementById('reassignCurrentCoordinator');
    const reassignSubtitle = document.getElementById('reassignModalSubtitle');
    const reassignSelect = document.getElementById('reassignCoordinatorSelect');
    const reassignReason = document.getElementById('reassignReason');
    const btnSubmit = document.getElementById('btnSubmitReassign');

    function toast(message, isError = false) {
        if (window.RiversideToast) {
            isError ? window.RiversideToast.error(message) : window.RiversideToast.success(message);
        } else {
            alert(message);
        }
    }

    function mondayOf(value) {
        const result = new Date(value.getFullYear(), value.getMonth(), value.getDate());
        const day = result.getDay() || 7;
        result.setDate(result.getDate() - day + 1);
        return result;
    }
    function isoDate(value) {
        const year = value.getFullYear();
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }
    function formatDate(value) { return value.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' }); }

    document.querySelectorAll('[data-main-tab]').forEach(button => button.addEventListener('click', () => {
        document.querySelectorAll('[data-main-tab]').forEach(x => { x.classList.toggle('active', x === button); x.setAttribute('aria-selected', x === button ? 'true' : 'false'); });
        document.querySelectorAll('[data-main-panel]').forEach(panel => panel.classList.toggle('active', panel.dataset.mainPanel === button.dataset.mainTab));
        if (button.dataset.mainTab === 'calendar') loadCalendar();
    }));

    async function openDrawer(bookingId) {
        overlay.hidden = false; drawer.classList.add('open'); drawer.setAttribute('aria-hidden', 'false'); document.body.classList.add('drawer-open');
        drawerContent.innerHTML = '<div class="drawer-loading"><i class="fa-solid fa-spinner fa-spin"></i><span>Đang tải chi tiết...</span></div>';
        try {
            const response = await fetch(`/quan-ly/tiec-dieu-phoi/detail/${bookingId}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) throw new Error(await response.text());
            drawerContent.innerHTML = await response.text();
            bindDrawer();
        } catch {
            drawerContent.innerHTML = '<div class="drawer-empty tall"><i class="fa-solid fa-circle-exclamation"></i><strong>Không thể tải chi tiết tiệc.</strong></div>';
        }
    }
    function closeDrawer() { drawer.classList.remove('open'); drawer.setAttribute('aria-hidden', 'true'); overlay.hidden = true; document.body.classList.remove('drawer-open'); }
    function bindDrawer() {
        drawerContent.querySelector('[data-close-drawer]')?.addEventListener('click', closeDrawer);
        drawerContent.querySelectorAll('[data-drawer-tab]').forEach(button => button.addEventListener('click', () => {
            drawerContent.querySelectorAll('[data-drawer-tab]').forEach(x => x.classList.toggle('active', x === button));
            drawerContent.querySelectorAll('[data-drawer-panel]').forEach(panel => panel.classList.toggle('active', panel.dataset.drawerPanel === button.dataset.drawerTab));
        }));
    }

    async function openReassignModal(bookingId, bookingCode, eventInfo, currentCoordinator) {
        if (!reassignModal) return;
        reassignBookingId.value = bookingId;
        reassignBookingCode.textContent = bookingCode || `#${bookingId}`;
        reassignEventTime.textContent = eventInfo || '-';
        reassignCurrentCoordinator.textContent = currentCoordinator ? currentCoordinator : 'Chưa có điều phối';
        reassignSubtitle.textContent = `Tiệc ${bookingCode || ('#' + bookingId)} · ${eventInfo || ''}`;
        reassignReason.value = '';
        reassignSelect.innerHTML = '<option value="">-- Đang tải danh sách điều phối viên... --</option>';
        reassignSelect.disabled = true;
        if (btnSubmit) {
            btnSubmit.disabled = true;
            btnSubmit.innerHTML = '<i class="fa-solid fa-check"></i> <span>Xác nhận phân công</span>';
        }

        reassignOverlay.hidden = false;
        reassignModal.hidden = false;

        try {
            const response = await fetch(`/quan-ly/tiec-dieu-phoi/available-coordinators/${bookingId}`);
            if (!response.ok) throw new Error(await response.text());
            const list = await response.json();
            reassignSelect.innerHTML = '<option value="">-- Chọn một điều phối viên --</option>';
            if (!list || list.length === 0) {
                reassignSelect.innerHTML = '<option value="">Không có nhân viên điều phối nào trong hệ thống</option>';
            } else {
                list.forEach(item => {
                    const opt = document.createElement('option');
                    opt.value = item.employeeId;
                    let label = `${item.fullName} (${item.employeeCode})`;
                    if (item.isCurrent) {
                        label += ' — [Đang phụ trách tiệc này]';
                        opt.disabled = true;
                    } else if (item.isAvailable) {
                        label += ' — ✓ Sẵn sàng (Rảnh)';
                    } else {
                        label += ` — ⚠️ ${item.statusNote || 'Bận ca khác'}`;
                    }
                    opt.textContent = label;
                    reassignSelect.appendChild(opt);
                });
            }
            reassignSelect.disabled = false;
            if (btnSubmit) btnSubmit.disabled = false;
        } catch (err) {
            reassignSelect.innerHTML = '<option value="">Lỗi tải danh sách điều phối viên</option>';
            toast('Không thể tải danh sách điều phối viên.', true);
        }
    }

    function closeReassignModal() {
        if (!reassignModal) return;
        reassignOverlay.hidden = true;
        reassignModal.hidden = true;
    }

    if (reassignForm) {
        reassignForm.addEventListener('submit', async (e) => {
            e.preventDefault();
            const bId = reassignBookingId.value;
            const cId = parseInt(reassignSelect.value, 10);
            const reasonText = reassignReason.value.trim();

            if (!cId || isNaN(cId)) {
                toast('Vui lòng chọn điều phối viên.', true);
                return;
            }
            if (reasonText.length < 3) {
                toast('Vui lòng nhập lý do can thiệp (tối thiểu 3 ký tự).', true);
                return;
            }

            btnSubmit.disabled = true;
            btnSubmit.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> <span>Đang xử lý...</span>';

            try {
                const res = await fetch(`/quan-ly/tiec-dieu-phoi/assign/${bId}`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({ coordinatorEmployeeId: cId, reason: reasonText })
                });
                const data = await res.json().catch(() => ({}));
                if (!res.ok) {
                    throw new Error(data.message || 'Phân công điều phối viên thất bại.');
                }

                toast(data.message || 'Phân công điều phối viên thành công.');
                closeReassignModal();
                setTimeout(() => {
                    window.location.reload();
                }, 700);
            } catch (err) {
                toast(err.message, true);
                btnSubmit.disabled = false;
                btnSubmit.innerHTML = '<i class="fa-solid fa-check"></i> <span>Xác nhận phân công</span>';
            }
        });
    }

    document.addEventListener('click', event => {
        const assignBtn = event.target.closest('[data-emergency-assign]');
        if (assignBtn) {
            event.stopPropagation();
            const ds = assignBtn.dataset;
            openReassignModal(ds.emergencyAssign, ds.bookingCode, ds.eventInfo, ds.currentCoordinator);
            return;
        }

        const target = event.target.closest('[data-booking-id]');
        if (target && !event.target.closest('[data-emergency-assign]')) {
            openDrawer(target.dataset.bookingId);
            return;
        }

        if (event.target.closest('[data-close-reassign]')) {
            closeReassignModal();
        }
    });

    overlay.addEventListener('click', closeDrawer);
    if (reassignOverlay) reassignOverlay.addEventListener('click', closeReassignModal);
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            closeDrawer();
            closeReassignModal();
        }
    });

    async function loadCalendar() {
        const end = new Date(weekStart); end.setDate(end.getDate() + 6);
        weekLabel.textContent = `${formatDate(weekStart)} - ${formatDate(end)}`;
        const params = new URLSearchParams({ fromDate: isoDate(weekStart), toDate: isoDate(end) });
        const hall = document.getElementById('calendarHall').value;
        const coordinator = document.getElementById('calendarCoordinator').value;
        if (hall) params.set('hallId', hall);
        if (coordinator) params.set('coordinatorEmployeeId', coordinator);
        if (document.getElementById('calendarConflicts').checked) params.set('conflictsOnly', 'true');
        calendarContent.innerHTML = '<div class="event-loading"><i class="fa-solid fa-spinner fa-spin"></i> Đang tải lịch...</div>';
        try {
            const response = await fetch(`/quan-ly/tiec-dieu-phoi/calendar?${params}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) throw new Error(await response.text());
            calendarContent.innerHTML = await response.text();
        } catch {
            calendarContent.innerHTML = '<div class="calendar-empty"><i class="fa-solid fa-circle-exclamation"></i><strong>Không thể tải lịch điều phối.</strong></div>';
        }
    }
    document.querySelectorAll('[data-week]').forEach(button => button.addEventListener('click', () => {
        if (button.dataset.week === 'today') weekStart = mondayOf(new Date());
        else weekStart.setDate(weekStart.getDate() + (button.dataset.week === 'next' ? 7 : -7));
        loadCalendar();
    }));
    ['calendarHall', 'calendarCoordinator', 'calendarConflicts'].forEach(id => document.getElementById(id)?.addEventListener('change', loadCalendar));
})();
