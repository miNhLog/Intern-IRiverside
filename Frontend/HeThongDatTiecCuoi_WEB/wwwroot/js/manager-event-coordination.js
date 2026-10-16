(() => {
    const page = document.querySelector('[data-event-page]');
    if (!page) return;
    const drawer = document.getElementById('eventDrawer');
    const overlay = document.getElementById('eventDrawerOverlay');
    const drawerContent = document.getElementById('eventDrawerContent');
    const calendarContent = document.getElementById('calendarContent');
    const weekLabel = document.getElementById('weekLabel');
    let weekStart = mondayOf(new Date());

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
    document.addEventListener('click', event => {
        const target = event.target.closest('[data-booking-id]');
        if (target) openDrawer(target.dataset.bookingId);
    });
    overlay.addEventListener('click', closeDrawer);
    document.addEventListener('keydown', event => { if (event.key === 'Escape') closeDrawer(); });

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
