(() => {
  'use strict';

  const page = document.querySelector('[data-coordination-page]');
  const urls = window.coordinationUrls;
  if (!page || !urls) return;

  const token = document.querySelector('#coordinationTokenForm input[name="__RequestVerificationToken"]')?.value || '';
  const assignmentModal = document.getElementById('assignmentModal');
  const cancelModal = document.getElementById('cancelModal');
  const assignmentForm = document.getElementById('assignmentForm');
  const cancelForm = document.getElementById('cancelForm');
  const assignmentSubmit = document.getElementById('assignmentSubmit');
  const cancelSubmit = document.getElementById('cancelSubmit');
  const bookingSelect = document.getElementById('assignmentBookingSelect');
  const bookingPicker = document.getElementById('bookingPickerGroup');
  const coordinatorList = document.getElementById('coordinatorList');
  const historyDrawer = document.getElementById('historyDrawer');
  const historyOverlay = document.getElementById('historyDrawerOverlay');
  let mode = 'assign';
  let activeTrigger = null;
  let currentCoordinatorId = null;
  let quickBookings = new Map();

  const escapeHtml = value => String(value ?? '')
    .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;').replaceAll("'", '&#039;');

  const initials = name => String(name || '').trim().split(/\s+/).filter(Boolean).slice(-2)
    .map(part => part.charAt(0).toUpperCase()).join('') || 'ĐP';

  const showToast = (message, isError = false) => {
    if (!message) return;
    if (isError) window.RiversideToast?.error(message);
    else window.RiversideToast?.success(message);
  };

  const flash = sessionStorage.getItem('coordinationFlash');
  if (flash) {
    sessionStorage.removeItem('coordinationFlash');
    try { const value = JSON.parse(flash); showToast(value.message, value.error); } catch { showToast(flash); }
  }

  const readMessage = async response => {
    try {
      const body = await response.json();
      return body.message || body.error || body.title || 'Yêu cầu không thành công.';
    } catch { return 'Không thể xử lý yêu cầu. Vui lòng thử lại.'; }
  };

  const fetchJson = async url => {
    const response = await fetch(url, { headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error(await readMessage(response));
    return response.json();
  };

  const postForm = (url, fields) => fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded', RequestVerificationToken: token, Accept: 'application/json' },
    body: new URLSearchParams(fields)
  });

  const setButtonLoading = (button, loading, loadingText, normalText, icon) => {
    button.disabled = loading;
    button.innerHTML = loading
      ? `<i class="fa-solid fa-spinner fa-spin"></i><span>${loadingText}</span>`
      : `<i class="fa-solid ${icon}"></i><span>${normalText}</span>`;
  };

  const openModal = (modal, trigger) => {
    activeTrigger = trigger || document.activeElement;
    modal.hidden = false;
    document.body.classList.add('coord-modal-open');
    window.setTimeout(() => modal.querySelector('select, textarea, button:not(.coord-modal-backdrop)')?.focus(), 30);
  };

  const closeModal = modal => {
    modal.hidden = true;
    if (!document.querySelector('.coord-modal:not([hidden])')) document.body.classList.remove('coord-modal-open');
    activeTrigger?.focus?.();
  };

  document.querySelectorAll('[data-close-modal]').forEach(button => {
    button.addEventListener('click', () => closeModal(button.closest('.coord-modal')));
  });

  const showFormError = (elementId, message) => {
    const element = document.getElementById(elementId);
    element.textContent = message || '';
    element.hidden = !message;
  };

  const resetAssignment = () => {
    assignmentForm.reset();
    document.getElementById('assignmentBookingId').value = '';
    document.getElementById('assignmentCoordinatorId').value = '';
    document.getElementById('assignmentBookingSummary').hidden = true;
    coordinatorList.innerHTML = '<div class="coord-people-empty"><i class="fa-regular fa-user"></i><span>Chọn một tiệc để xem lịch nhân sự.</span></div>';
    coordinatorList.dataset.loaded = 'false';
    currentCoordinatorId = null;
    showFormError('assignmentError', '');
    document.getElementById('reasonCount').textContent = '0';
    assignmentSubmit.disabled = true;
  };

  const setBookingSummary = booking => {
    document.getElementById('assignmentBookingId').value = booking.bookingId;
    document.getElementById('assignmentBooking').textContent = booking.bookingCode;
    document.getElementById('assignmentCustomer').textContent = booking.customerName || '';
    document.getElementById('assignmentHall').textContent = booking.hallName || '—';
    document.getElementById('assignmentDate').textContent = booking.eventDate || '—';
    document.getElementById('assignmentShift').textContent = booking.shift || '—';
    document.getElementById('assignmentBookingSummary').hidden = false;
  };

  const renderCoordinators = items => {
    coordinatorList.innerHTML = '';
    if (!items.length) {
      coordinatorList.innerHTML = '<div class="coord-people-empty"><i class="fa-solid fa-users-slash"></i><span>Không có điều phối viên hợp lệ.</span></div>';
      assignmentSubmit.disabled = true;
      return;
    }
    items.forEach(item => {
      const isCurrent = currentCoordinatorId !== null && Number(item.employeeId) === Number(currentCoordinatorId);
      const selectable = item.isAvailable && !isCurrent;
      const label = document.createElement('label');
      label.className = `coord-person-option${selectable ? '' : ' busy'}`;
      label.innerHTML = `<input type="radio" name="coordinator" value="${Number(item.employeeId)}" ${selectable ? '' : 'disabled'}>
        <span class="coord-person-avatar">${escapeHtml(initials(item.fullName))}</span>
        <span class="coord-person-info"><strong>${escapeHtml(item.fullName)}</strong><small>${escapeHtml(item.employeeCode)}</small></span>
        <span class="coord-availability${selectable ? '' : ' busy'}"><i class="fa-solid ${selectable ? 'fa-circle-check' : 'fa-ban'}"></i>${escapeHtml(isCurrent ? 'Đang phụ trách' : item.availabilityMessage)}</span>`;
      const radio = label.querySelector('input');
      radio.addEventListener('change', () => {
        coordinatorList.querySelectorAll('.coord-person-option').forEach(option => option.classList.remove('selected'));
        label.classList.add('selected');
        document.getElementById('assignmentCoordinatorId').value = radio.value;
        assignmentSubmit.disabled = false;
        showFormError('assignmentError', '');
      });
      coordinatorList.appendChild(label);
    });
  };

  const loadCoordinators = async bookingId => {
    document.getElementById('assignmentCoordinatorId').value = '';
    assignmentSubmit.disabled = true;
    coordinatorList.innerHTML = '<div class="coord-loading"><i class="fa-solid fa-spinner fa-spin"></i><span>Đang kiểm tra lịch nhân sự...</span></div>';
    try {
      const items = await fetchJson(`${urls.options}?bookingId=${encodeURIComponent(bookingId)}`);
      coordinatorList.dataset.loaded = 'true';
      renderCoordinators(items);
    } catch (error) {
      coordinatorList.innerHTML = '<div class="coord-people-empty"><i class="fa-solid fa-triangle-exclamation"></i><span>Không thể tải lịch nhân sự.</span></div>';
      showFormError('assignmentError', error.message);
    }
  };

  const openAssignmentForBooking = async (button, selectedMode) => {
    resetAssignment();
    mode = selectedMode;
    currentCoordinatorId = button.dataset.currentCoordinatorId || null;
    bookingPicker.hidden = true;
    document.getElementById('assignmentTitle').textContent = mode === 'change' ? 'Thay đổi điều phối viên' : 'Phân công điều phối viên';
    document.getElementById('changeReasonGroup').hidden = mode !== 'change';
    assignmentSubmit.querySelector('span').textContent = mode === 'change' ? 'Xác nhận thay đổi' : 'Xác nhận phân công';
    setBookingSummary({
      bookingId: button.dataset.bookingId, bookingCode: button.dataset.bookingCode,
      customerName: button.dataset.customer, hallName: button.dataset.hall,
      eventDate: button.dataset.date, shift: button.dataset.shift, tableCount: button.dataset.tables
    });
    openModal(assignmentModal, button);
    await loadCoordinators(button.dataset.bookingId);
  };

  document.querySelectorAll('.js-open-assignment').forEach(button => {
    button.addEventListener('click', () => openAssignmentForBooking(button, button.dataset.mode));
  });

  document.getElementById('quickAssignButton')?.addEventListener('click', async event => {
    resetAssignment();
    mode = 'assign';
    bookingPicker.hidden = false;
    document.getElementById('changeReasonGroup').hidden = true;
    document.getElementById('assignmentTitle').textContent = 'Phân công điều phối viên';
    assignmentSubmit.querySelector('span').textContent = 'Xác nhận phân công';
    bookingSelect.innerHTML = '<option value="">Đang tải danh sách tiệc...</option>';
    bookingSelect.disabled = true;
    openModal(assignmentModal, event.currentTarget);
    try {
      const items = await fetchJson(urls.unassigned);
      quickBookings = new Map(items.map(item => [String(item.bookingId), item]));
      bookingSelect.innerHTML = '<option value="">Chọn tiệc chưa phân công</option>';
      items.forEach(item => {
        const option = document.createElement('option');
        option.value = item.bookingId;
        option.textContent = `${item.bookingCode} · ${item.customerName} · ${item.hallName} · ${item.eventDate} (${item.shift})`;
        bookingSelect.appendChild(option);
      });
      if (!items.length) {
        bookingSelect.innerHTML = '<option value="">Không còn tiệc cần phân công</option>';
        coordinatorList.innerHTML = '<div class="coord-people-empty"><i class="fa-solid fa-circle-check"></i><span>Tất cả tiệc hợp lệ đã có điều phối viên.</span></div>';
      }
      bookingSelect.disabled = false;
    } catch (error) {
      bookingSelect.innerHTML = '<option value="">Không thể tải danh sách tiệc</option>';
      showFormError('assignmentError', error.message);
    }
  });

  bookingSelect.addEventListener('change', async () => {
    const booking = quickBookings.get(bookingSelect.value);
    if (!booking) { resetAssignment(); bookingPicker.hidden = false; return; }
    setBookingSummary(booking);
    await loadCoordinators(booking.bookingId);
  });

  document.getElementById('changeReason').addEventListener('input', event => {
    document.getElementById('reasonCount').textContent = event.target.value.length;
  });
  document.getElementById('cancelReason').addEventListener('input', event => {
    document.getElementById('cancelReasonCount').textContent = event.target.value.length;
  });

  assignmentForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (assignmentSubmit.disabled) return;
    const bookingId = document.getElementById('assignmentBookingId').value;
    const coordinatorEmployeeId = document.getElementById('assignmentCoordinatorId').value;
    const reason = document.getElementById('changeReason').value.trim();
    if (!bookingId) { showFormError('assignmentError', 'Vui lòng chọn tiệc cần phân công.'); return; }
    if (!coordinatorEmployeeId) { showFormError('assignmentError', 'Vui lòng chọn một điều phối viên đang rảnh.'); return; }
    if (mode === 'change' && reason.length < 3) { showFormError('assignmentError', 'Lý do thay đổi phải có ít nhất 3 ký tự.'); document.getElementById('changeReason').focus(); return; }
    setButtonLoading(assignmentSubmit, true, 'Đang lưu...', mode === 'change' ? 'Xác nhận thay đổi' : 'Xác nhận phân công', 'fa-check');
    try {
      const response = await postForm(mode === 'change' ? urls.change : urls.assign, { bookingId, coordinatorEmployeeId, reason });
      if (!response.ok) throw new Error(await readMessage(response));
      const body = await response.json();
      sessionStorage.setItem('coordinationFlash', JSON.stringify({ message: body.message || (mode === 'change' ? 'Đã thay đổi điều phối viên.' : 'Đã phân công điều phối viên.') }));
      window.location.reload();
    } catch (error) {
      showFormError('assignmentError', error.message);
      setButtonLoading(assignmentSubmit, false, '', mode === 'change' ? 'Xác nhận thay đổi' : 'Xác nhận phân công', 'fa-check');
      assignmentSubmit.disabled = false;
    }
  });

  document.querySelectorAll('.js-open-cancel').forEach(button => {
    button.addEventListener('click', () => {
      cancelForm.reset();
      document.getElementById('cancelBookingId').value = button.dataset.bookingId;
      document.getElementById('cancelBooking').textContent = `Tiệc ${button.dataset.bookingCode}`;
      document.getElementById('cancelCoordinator').textContent = button.dataset.coordinator || 'hiện tại';
      document.getElementById('cancelReasonCount').textContent = '0';
      showFormError('cancelError', '');
      setButtonLoading(cancelSubmit, false, '', 'Xác nhận hủy', 'fa-user-xmark');
      openModal(cancelModal, button);
    });
  });

  cancelForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (cancelSubmit.disabled) return;
    const reason = document.getElementById('cancelReason').value.trim();
    if (reason.length < 3) { showFormError('cancelError', 'Lý do hủy phải có ít nhất 3 ký tự.'); document.getElementById('cancelReason').focus(); return; }
    setButtonLoading(cancelSubmit, true, 'Đang hủy...', 'Xác nhận hủy', 'fa-user-xmark');
    try {
      const response = await postForm(urls.cancel, { bookingId: document.getElementById('cancelBookingId').value, reason });
      if (!response.ok) throw new Error(await readMessage(response));
      const body = await response.json();
      sessionStorage.setItem('coordinationFlash', JSON.stringify({ message: body.message || 'Đã hủy phân công.' }));
      window.location.reload();
    } catch (error) {
      showFormError('cancelError', error.message);
      setButtonLoading(cancelSubmit, false, '', 'Xác nhận hủy', 'fa-user-xmark');
    }
  });

  const closeDrawer = () => {
    historyDrawer.classList.remove('open');
    historyDrawer.setAttribute('aria-hidden', 'true');
    historyOverlay.hidden = true;
    document.body.classList.remove('coord-drawer-open');
    activeTrigger?.focus?.();
  };

  const renderDrawer = detail => {
    const booking = detail.booking;
    const stateLabel = ({CONFLICT:'Xung đột lịch',ASSIGNED:'Đã phân công',UNASSIGNED:'Chưa phân công',CANCELLED:'Đã hủy',COMPLETED:'Đã hoàn tất',INELIGIBLE:'Chưa đủ điều kiện'})[booking.coordinationState] || 'Chưa đủ điều kiện';
    const history = detail.history || [];
    const audit = detail.auditTrail || [];
    const historyHtml = history.length ? history.map(item => `<article><strong>${escapeHtml(item.coordinatorName)} <span class="coord-timeline-status">${escapeHtml(item.statusName)}</span></strong><p>${escapeHtml(item.coordinatorCode)} · Phân công bởi ${escapeHtml(item.assignedByName)}</p><small>${new Date(item.assignedAt).toLocaleString('vi-VN')}</small></article>`).join('') : '<div class="coord-history-empty">Chưa có bản ghi phân công.</div>';
    const auditHtml = audit.length ? audit.map(item => `<article><strong>${escapeHtml(actionLabel(item.action))}</strong><p>${escapeHtml(item.reason || 'Không có ghi chú')}</p><small>${escapeHtml(item.actorName)} · ${new Date(item.timestamp).toLocaleString('vi-VN')}</small></article>`).join('') : '<div class="coord-history-empty">Chưa có nhật ký thao tác.</div>';
    historyDrawer.innerHTML = `<header class="coord-drawer-header"><button type="button" class="coord-drawer-close" aria-label="Đóng"><i class="fa-solid fa-xmark"></i></button><span>CHI TIẾT ĐIỀU PHỐI</span><h2 id="historyTitle">${escapeHtml(booking.bookingCode)}</h2><p>${escapeHtml(booking.customerName)} · ${escapeHtml(booking.hallName)}</p><span class="coord-state ${escapeHtml(booking.coordinationState.toLowerCase())} coord-drawer-status">${escapeHtml(stateLabel)}</span></header>
      <div class="coord-drawer-body">${booking.ineligibilityReason ? `<p class="coord-ineligibility-notice">${escapeHtml(booking.ineligibilityReason)}</p>` : ''}<section class="coord-detail-card"><h3><i class="fa-regular fa-calendar"></i>Thông tin tiệc</h3><dl class="coord-detail-grid"><div><dt>Ngày tổ chức</dt><dd>${new Date(booking.eventDate).toLocaleDateString('vi-VN')}</dd></div><div><dt>Ca tổ chức</dt><dd>${escapeHtml(booking.shift)}</dd></div><div><dt>Sảnh</dt><dd>${escapeHtml(booking.hallName)}</dd></div><div><dt>Số bàn</dt><dd>${Number(booking.tableCount)}</dd></div><div><dt>Trạng thái tiệc</dt><dd>${escapeHtml(booking.bookingStatusName)}</dd></div><div><dt>Điều phối viên hiện tại</dt><dd>${escapeHtml(booking.coordinatorName || (booking.ineligibilityReason ? 'Không có phân công hiệu lực' : 'Chưa phân công'))}</dd></div></dl></section>
      <section class="coord-detail-card"><h3><i class="fa-solid fa-clock-rotate-left"></i>Lịch sử phân công</h3><div class="coord-timeline">${historyHtml}</div></section>
      <section class="coord-detail-card"><h3><i class="fa-solid fa-list-check"></i>Nhật ký thao tác</h3><div class="coord-timeline">${auditHtml}</div></section></div>`;
    historyDrawer.querySelector('.coord-drawer-close').addEventListener('click', closeDrawer);
  };

  const actionLabel = action => ({
    COORDINATION_ASSIGNED: 'Tạo phân công', COORDINATION_CHANGED: 'Thay đổi điều phối viên',
    COORDINATION_CANCELLED: 'Hủy phân công', COORDINATION_COMPLETED: 'Hoàn tất phân công'
  }[action] || action);

  document.querySelectorAll('.js-open-history').forEach(button => {
    button.addEventListener('click', async () => {
      activeTrigger = button;
      historyDrawer.innerHTML = '<div class="coord-drawer-loading"><i class="fa-solid fa-spinner fa-spin"></i><span>Đang tải chi tiết phân công...</span></div>';
      historyOverlay.hidden = false;
      historyDrawer.classList.add('open');
      historyDrawer.setAttribute('aria-hidden', 'false');
      document.body.classList.add('coord-drawer-open');
      try {
        const detail = await fetchJson(`${urls.history}?bookingId=${encodeURIComponent(button.dataset.bookingId)}`);
        renderDrawer(detail);
      } catch (error) {
        historyDrawer.innerHTML = `<div class="coord-drawer-loading"><i class="fa-solid fa-triangle-exclamation"></i><span>${escapeHtml(error.message)}</span><button type="button" class="coord-secondary">Đóng</button></div>`;
        historyDrawer.querySelector('button').addEventListener('click', closeDrawer);
      }
    });
  });
  historyOverlay.addEventListener('click', closeDrawer);

  document.addEventListener('keydown', event => {
    if (event.key !== 'Escape') return;
    const openModalElement = document.querySelector('.coord-modal:not([hidden])');
    if (openModalElement) closeModal(openModalElement);
    else if (historyDrawer.classList.contains('open')) closeDrawer();
  });

  document.getElementById('coordinationFilters')?.addEventListener('submit', event => {
    const button = event.currentTarget.querySelector('.coord-filter-submit');
    button.disabled = true;
    button.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i><span>Đang lọc...</span>';
  });
})();
