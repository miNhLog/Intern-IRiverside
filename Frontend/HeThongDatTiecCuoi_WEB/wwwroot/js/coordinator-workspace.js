(() => {
    'use strict';
    const config = JSON.parse(document.getElementById('coordinatorWorkspaceData').textContent);
    const $ = id => document.getElementById(id);
    const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    const date = value => new Date(String(value).slice(0, 10) + 'T12:00:00Z');
    const key = value => value.toISOString().slice(0, 10);
    const add = (value, days) => { const result = new Date(value); result.setUTCDate(result.getUTCDate() + days); return result; };
    const fmt = value => { const d=date(value); return `${String(d.getUTCDate()).padStart(2,'0')}/${String(d.getUTCMonth()+1).padStart(2,'0')}`; };
    const shiftName = shift => {
        const value = String(shift ?? '').trim().replace(/^(?:ca\s+)+/i, '').toLocaleLowerCase('vi');
        return ({'trưa':'Ca trưa','tối':'Ca tối'}[value] ?? String(shift ?? '').trim());
    };
    const shiftDisplay = shift => { const name = shiftName(shift); const hours = {'Ca trưa':'10:30–14:00','Ca tối':'17:30–21:30'}[name]; return hours ? `${name} (${hours})` : name; };
    const shiftOrder = shift => ({'Ca trưa':0,'Ca tối':1}[shiftName(shift)] ?? 2);
    const today = date(config.today);
    const monday = value => add(value, -((value.getUTCDay() + 6) % 7));
    const queryParams = new URLSearchParams(location.search);
    let week = monday(today), demo = config.standalone === true, mode = 'week', scope = queryParams.get('scope') === 'all' ? 'all' : 'week', page = config.page, selected = null;
    const requestedBookingId = Number(queryParams.get('bookingId')) || null;
    const work = new Map();
    const sample = [
        {bookingId:-1, bookingCode:'MH-261001', customerName:'Minh Anh & Hoàng Nam', eventDate:key(today), shift:'Trưa', hallName:'Sảnh Riverside', tableCount:35, menuName:'Thực đơn Hạnh Phúc', decorationName:'Elegant Gold', services:['Âm thanh, ánh sáng','MC tiệc cưới'], specialRequests:'Chuẩn bị 2 bàn chay và kiểm tra micro trước khi đón khách.', bookingStatusName:'Đã xác nhận'},
        {bookingId:-2, bookingCode:'MH-261002', customerName:'Ngọc Linh & Tuấn Kiệt', eventDate:key(today), shift:'Tối', hallName:'Sảnh Palace', tableCount:42, menuName:'Thực đơn Thịnh Vượng', decorationName:'Classic White', services:['Ban nhạc','Màn hình LED'], specialRequests:'Kiểm tra video trình chiếu trước giờ khai tiệc.', bookingStatusName:'Đã xác nhận'},
        {bookingId:-3, bookingCode:'MH-261003', customerName:'Thanh Hà & Đức Huy', eventDate:key(add(today,2)), shift:'Tối', hallName:'Sảnh Riverside', tableCount:28, menuName:'Thực đơn Hạnh Phúc', decorationName:'Garden Romance', services:['MC tiệc cưới'], specialRequests:'Bố trí lối đi thuận tiện cho người lớn tuổi.', bookingStatusName:'Đã xác nhận'},
        {bookingId:-4, bookingCode:'MH-261004', customerName:'Bảo Trân & Quang Minh', eventDate:key(add(today,5)), shift:'Trưa', hallName:'Sảnh Grand', tableCount:50, menuName:'Thực đơn Kim Cương', decorationName:'Royal Gold', services:['Âm thanh, ánh sáng'], specialRequests:null, bookingStatusName:'Đã xác nhận'}
    ];
    function items() { return (demo ? sample : config.parties).map(p => ({...p, shift:shiftName(p.shift)})); }
    function tasks(party) {
        if (!demo) return [];
        if (!work.has(party.bookingId)) work.set(party.bookingId, [
            ['Trước tiệc','Kiểm tra bố trí bàn và sơ đồ sảnh','Trước giờ đón khách',true],
            ['Trước tiệc','Xác nhận thực đơn và số bàn với bếp','Trước giờ đón khách',true],
            ['Trước tiệc','Kiểm tra trang trí, âm thanh và ánh sáng','Trước giờ đón khách',party.bookingId === -1],
            ['Trước tiệc','Đối chiếu yêu cầu đặc biệt của khách','Trước giờ đón khách',false],
            ['Trong tiệc','Kiểm tra đón khách và phục vụ món','Trong ca tiệc',false],
            ['Trong tiệc','Theo dõi phát sinh và báo cáo sự cố','Trong ca tiệc',false],
            ['Sau tiệc','Kiểm tra bàn giao sảnh và thiết bị','Sau khi kết thúc tiệc',false],
            ['Sau tiệc','Ghi nhận tổng kết ca điều phối','Sau khi kết thúc tiệc',false]
        ].map((t,i) => ({id:i,group:t[0],name:t[1],due:t[2],done:party.bookingId < -2 ? false : t[3]})));
        return work.get(party.bookingId);
    }
    const percent = party => { const list = tasks(party); return list.length ? Math.round(list.filter(t=>t.done).length/list.length*100) : null; };
    const stat = (icon,label,value,note,tone='') => `<article class="cw-stat ${tone}"><span class="cw-stat-icon"><i class="fa-solid ${icon}"></i></span><div><small>${label}</small><strong>${value}</strong><em>${note}</em></div></article>`;
    const empty = (title,body) => `<div class="cw-empty"><i class="fa-regular fa-calendar-check"></i><strong>${title}</strong>${body}</div>`;
    const statusClass = value => String(value || 'unknown').toLowerCase().replaceAll('_', '-');
    function setPage(next) {
        page = next;
        $('cwSchedule').hidden = page !== 'schedule'; $('cwProgress').hidden = page !== 'progress';
        const title = page === 'schedule' ? 'Lịch làm việc' : 'Theo dõi tiến độ';
        $('cwTitle').textContent = title;
        $('cwSubtitle').textContent = page === 'schedule' ? 'Sắp xếp lịch tiệc, chủ động chuẩn bị từng ca làm việc.' : 'Theo dõi từng đầu việc để mỗi buổi tiệc diễn ra trọn vẹn.';
        document.querySelectorAll('[data-cw-page]').forEach(el => { el.classList.toggle('active', el.dataset.cwPage === page); });
        if (config.standalone) history.replaceState(null, '', '#' + page);
        render();
    }
    function filters() {
        for (const [id, field, label] of [['cwHall','hallName','Tất cả sảnh'],['cwShift','shift','Tất cả ca']]) {
            const values = [...new Set(items().map(p=>p[field]))];
            if (field === 'shift') values.sort((a,b)=>shiftOrder(a)-shiftOrder(b));
            $(id).innerHTML = `<option value="">${label}</option>` + values.map(v=>`<option value="${esc(v)}">${esc(v)}</option>`).join('');
        }
    }
    function render() {
        if (config.standalone && $('cwDemo')) $('cwDemo').textContent = 'Đặt lại bản minh họa';
        $('cwNotice').hidden = !demo;
        $('cwNotice').textContent = 'BẢN XEM TRƯỚC · Toàn bộ tiệc và checklist bên dưới là dữ liệu minh họa. Thao tác chỉ có hiệu lực trong phiên xem này.';
        $('cwError').hidden = demo || !config.error;
        if (config.error && !demo) { $('cwError').textContent = config.error + ' '; const retry = document.createElement('button'); retry.className='cw-button'; retry.textContent='Thử lại'; retry.onclick=()=>location.reload(); $('cwError').append(retry); }
        if (page === 'schedule') renderSchedule(); else renderProgress();
    }
    function renderSchedule() {
        const all = items(), end = add(week,7), inWeek = all.filter(p=>p.eventDate.slice(0,10)>=key(week) && p.eventDate.slice(0,10)<key(end)), base = scope === 'all' ? all : inWeek;
        const query = $('cwSearch').value.trim().toLocaleLowerCase('vi');
        const selectedDate = $('cwDate').value;
        const visible = base.filter(p=>(!selectedDate || p.eventDate.slice(0,10)===selectedDate) && (!$('cwHall').value || p.hallName===$('cwHall').value) && (!$('cwShift').value || p.shift===$('cwShift').value) && `${p.bookingCode} ${p.customerName} ${p.hallName}`.toLocaleLowerCase('vi').includes(query));
        $('cwClearDate').hidden = !selectedDate;
        const failed = !demo && config.error;
        $('cwScheduleStats').innerHTML = stat(scope === 'all' ? 'fa-list' : 'fa-calendar-week',scope === 'all' ? 'Tổng tiệc được giao' : 'Tiệc trong tuần',failed?'—':base.length,scope === 'all' ? 'Toàn bộ phân công còn hiệu lực' : 'Theo tuần đang xem') + stat('fa-calendar-day','Tiệc hôm nay',failed?'—':all.filter(p=>p.eventDate.slice(0,10)===key(today)).length,fmt(key(today))+' · Được phân công') + stat('fa-champagne-glasses','Tiệc sắp tới',failed?'—':all.filter(p=>p.eventDate.slice(0,10)>key(today)).length,'Từ ngày mai','blue') + stat('fa-building',scope === 'all' ? 'Tổng sảnh' : 'Sảnh trong tuần',failed?'—':new Set(base.map(p=>p.hallName)).size,'Có tiệc được giao','green');
        $('cwDateRange').textContent = `${fmt(key(week))} – ${fmt(key(add(week,6)))} / ${week.getUTCFullYear()}`;
        $('cwResult').textContent = failed ? 'Chưa tải được dữ liệu' : `${visible.length} tiệc phù hợp`;
        $('cwToolbar').hidden = scope === 'all';
        $('cwScopeWeek').classList.toggle('active', scope === 'week');
        $('cwScopeAll').classList.toggle('active', scope === 'all');
        $('cwScopeWeek').setAttribute('aria-pressed', String(scope === 'week'));
        $('cwScopeAll').setAttribute('aria-pressed', String(scope === 'all'));
        const shifts = ['Ca trưa','Ca tối'];
        let html = '<div class="cw-week"><div class="cw-day-heading">CA TIỆC</div>';
        for(let i=0;i<7;i++) { const day=add(week,i); html+=`<div class="cw-day-heading ${key(day)===key(today)?'today':''}">${['Thứ hai','Thứ ba','Thứ tư','Thứ năm','Thứ sáu','Thứ bảy','Chủ nhật'][i]}<strong>${day.getUTCDate()}</strong></div>`; }
        for(const shift of shifts) { html+=`<div class="cw-shift-label">${esc(shift)}</div>`; for(let i=0;i<7;i++) { const d=key(add(week,i)), events=visible.filter(p=>p.eventDate.slice(0,10)===d&&p.shift===shift); html+=`<div class="cw-slot ${d===key(today)?'today':''}">${events.length ? events.map(p=>`<button class="cw-event" type="button" data-detail="${p.bookingId}"><strong>${esc(p.bookingCode)}</strong><span>${esc(p.customerName)}</span><small><i class="fa-solid fa-location-dot"></i> ${esc(p.hallName)} · ${p.tableCount} bàn</small></button>`).join('') : '<span class="cw-slot-empty">—</span>'}</div>`; }}
        $('cwCalendar').innerHTML = failed ? empty('Không tải được lịch','Vui lòng thử lại ở thông báo phía trên.') : html+'</div>';
        $('cwCalendar').hidden = scope === 'all' || mode !== 'week'; $('cwScheduleList').hidden = scope === 'week' && mode !== 'list';
        $('cwScheduleList').innerHTML = failed ? empty('Không tải được lịch','Vui lòng thử lại.') : !visible.length ? empty(scope === 'all' ? 'Chưa có tiệc được phân công' : 'Không có tiệc phù hợp trong tuần này',scope === 'all' ? 'Các tiệc được giao cho bạn sẽ xuất hiện tại đây.' : 'Thử chọn tuần khác hoặc thay đổi bộ lọc.') : `<div class="cw-table-wrap"><table class="cw-table"><thead><tr><th>Ngày / Ca</th><th>Tiệc / Khách hàng</th><th>Sảnh</th><th>Số bàn</th><th>Trạng thái</th><th></th></tr></thead><tbody>${visible.sort((a,b)=>a.eventDate.localeCompare(b.eventDate)||shiftOrder(a.shift)-shiftOrder(b.shift)).map(p=>`<tr><td>${fmt(p.eventDate)} · ${esc(shiftDisplay(p.shift))}</td><td><strong>${esc(p.bookingCode)}</strong><br>${esc(p.customerName)}</td><td>${esc(p.hallName)}</td><td>${p.tableCount}</td><td><span class="cw-status ${statusClass(p.bookingStatusCode)}">${esc(p.bookingStatusName)}</span></td><td><button type="button" data-detail="${p.bookingId}">Chi tiết →</button></td></tr>`).join('')}</tbody></table></div>`;
        const next = all.filter(p=>p.eventDate.slice(0,10)>=key(today)).sort((a,b)=>a.eventDate.localeCompare(b.eventDate)||shiftOrder(a.shift)-shiftOrder(b.shift))[0];
        $('cwNextParty').innerHTML = failed ? '<p>Chưa tải được dữ liệu tiệc.</p>' : next ? `<h3>${esc(next.customerName)}</h3><p>${esc(next.bookingCode)} · ${fmt(next.eventDate)} · ${esc(shiftDisplay(next.shift))}<br>${esc(next.hallName)} · ${next.tableCount} bàn</p><button class="cw-button" type="button" data-detail="${next.bookingId}">Xem thông tin tiệc →</button>` : '<p>Chưa có tiệc sắp tới được phân công cho bạn.</p>';
    }
    function renderProgress() {
        const all=items(), allTasks=all.flatMap(tasks), done=allTasks.filter(t=>t.done).length;
        const noData = !demo;
        $('cwProgressStats').innerHTML=stat('fa-champagne-glasses','Tiệc đang theo dõi',config.error&&!demo?'—':all.length,'Phân công còn hiệu lực')+stat('fa-list-check','Tổng đầu việc',noData?'—':allTasks.length,noData?'Chưa có checklist':'Checklist minh họa','blue')+stat('fa-circle-check','Đã hoàn thành',noData?'—':done,noData?'Chưa có checklist':'Đã đánh dấu trong phiên xem','green')+stat('fa-hourglass-half','Chưa hoàn thành',noData?'—':allTasks.length-done,noData?'Chưa có checklist':'Cần tiếp tục thực hiện','red');
        const query=$('cwProgressSearch').value.trim().toLocaleLowerCase('vi'), visible=all.filter(p=>`${p.bookingCode} ${p.customerName}`.toLocaleLowerCase('vi').includes(query));
        if(!visible.some(p=>p.bookingId===selected)) selected=visible[0]?.bookingId ?? null;
        $('cwPartyCount').textContent=visible.length;
        $('cwPartyList').innerHTML=visible.length ? visible.map(p=>{ const n=percent(p), list=tasks(p); return `<button class="cw-party ${p.bookingId===selected?'selected':''}" aria-pressed="${p.bookingId===selected}" type="button" data-party="${p.bookingId}"><strong>${esc(p.bookingCode)}</strong><p>${esc(p.customerName)}</p><small>${fmt(p.eventDate)} · ${esc(shiftDisplay(p.shift))} · ${esc(p.hallName)}</small>${n===null?'<p>Chưa có checklist</p>':`<div class="cw-bar"><span style="width:${n}%"></span></div><div class="cw-party-progress"><span>${list.filter(t=>t.done).length}/${list.length} công việc</span><strong>${n}%</strong></div>`}</button>`; }).join('') : empty(config.error&&!demo?'Không tải được danh sách':'Chưa có tiệc phù hợp',demo?'Thử thay đổi từ khóa.':'Chọn “Xem dữ liệu minh họa” để xem thử bố cục.');
        const party=all.find(p=>p.bookingId===selected);
        if(!party) { $('cwChecklist').innerHTML=empty('Chọn tiệc để xem tiến độ','Checklist theo từng giai đoạn sẽ hiển thị tại đây.'); return; }
        const list=tasks(party), n=percent(party);
        $('cwChecklist').innerHTML=`<div class="cw-checklist-head"><span class="cw-eyebrow">${esc(party.bookingCode)} · ${demo?'CHECKLIST MINH HỌA':'TIỆC ĐƯỢC PHÂN CÔNG'}</span><h2>${esc(party.customerName)}</h2><p>${fmt(party.eventDate)} · ${esc(shiftDisplay(party.shift))} · ${esc(party.hallName)} · ${party.tableCount} bàn</p>${n===null?'':`<div class="cw-progress-total"><span>Tiến độ chuẩn bị &amp; vận hành</span><strong>${n}% hoàn thành</strong></div><div class="cw-bar"><span style="width:${n}%"></span></div>`}</div>` + (n===null ? empty('Tiệc này chưa có checklist công việc','Bật dữ liệu minh họa để trải nghiệm đánh dấu tiến độ trước khi triển khai chức năng lưu.') : `<div class="cw-checklist-body">${['Trước tiệc','Trong tiệc','Sau tiệc'].map((group,i)=>{ const rows=list.filter(t=>t.group===group); return `<section class="cw-task-group"><h3>${i+1}. ${group}<span>${rows.filter(t=>t.done).length}/${rows.length} hoàn thành</span></h3>${rows.map(t=>`<label class="cw-task ${t.done?'done':''}"><input type="checkbox" data-task="${t.id}" ${t.done?'checked':''}/><span class="cw-task-copy"><span class="cw-task-name">${esc(t.name)}</span><small>${esc(t.due)}</small></span><span class="cw-pill ${t.done?'done':'pending'}">${t.done?'Hoàn thành':'Chưa làm'}</span></label>`).join('')}</section>`; }).join('')}</div><footer class="cw-checklist-footer"><i class="fa-solid fa-circle-info"></i> Bản xem trước · Đánh dấu để thử thay đổi tiến độ. Dữ liệu không được lưu vào hệ thống.</footer>`);
    }
    function canIssueReviewQr(party) { return !demo && party && party.bookingId > 0 && String(party.bookingStatusCode||'').toUpperCase()==='PREPARING'; }
    function reviewQrSection(party) {
        if (!canIssueReviewQr(party)) return '';
        return `<section class="cw-review-qr" data-review-qr="${party.bookingId}"><h3><i class="fa-solid fa-qrcode"></i> Mã QR đánh giá</h3><p id="cwReviewQrNote-${party.bookingId}" class="cw-review-qr-note">Phát hành hoặc lấy lại mã QR đánh giá cho tiệc đang chuẩn bị.</p><div class="cw-dialog-actions"><button type="button" class="cw-button cw-primary" data-review-qr-issue="${party.bookingId}"><i class="fa-solid fa-qrcode"></i> Phát hành / Lấy mã QR</button></div><div id="cwReviewQrResult-${party.bookingId}" class="cw-review-qr-result" aria-live="polite"></div></section>`;
    }
    function detail(id) {
        const p=items().find(p=>p.bookingId===id); if(!p) return;
        $('cwDetailBody').innerHTML=`<h2>${esc(p.bookingCode)}</h2><p>${esc(p.customerName)}</p><dl><dt>Ngày / Ca</dt><dd>${fmt(p.eventDate)} · ${esc(shiftDisplay(p.shift))}</dd><dt>Sảnh / Quy mô</dt><dd>${esc(p.hallName)} · ${p.tableCount} bàn</dd><dt>Thực đơn</dt><dd>${esc(p.menuName||'Chưa chọn')}</dd><dt>Trang trí</dt><dd>${esc(p.decorationName||'Chưa chọn')}</dd><dt>Dịch vụ</dt><dd>${esc(p.services?.join(', ')||'Chưa có')}</dd><dt>Yêu cầu đặc biệt</dt><dd>${esc(p.specialRequests||'Không có yêu cầu đặc biệt')}</dd><dt>Trạng thái</dt><dd>${esc(p.bookingStatusName)}</dd></dl>${demo?'<p>Dữ liệu minh họa · Không phải thông tin tiệc thực tế.</p>':`<div class="cw-dialog-actions"><a class="cw-button" href="/dieu-phoi/checklist?bookingId=${p.bookingId}"><i class="fa-solid fa-list-check"></i> Theo dõi tiến độ</a><a class="cw-button cw-primary" href="/dieu-phoi/su-co?bookingId=${p.bookingId}"><i class="fa-solid fa-triangle-exclamation"></i> Báo cáo sự cố</a></div>${reviewQrSection(p)}`}`;
        $('cwDetail').showModal();
    }
    $('cwClose').onclick=()=>$('cwDetail').close();
    async function issueReviewQr(bookingId, button) {
        const note=$(`cwReviewQrNote-${bookingId}`), result=$(`cwReviewQrResult-${bookingId}`);
        const token=document.querySelector('#coordinatorWorkspace input[name="__RequestVerificationToken"]')?.value;
        if (!token) { if(note) note.textContent='Không tìm thấy mã xác thực. Vui lòng tải lại trang.'; return; }
        if (button) { button.disabled=true; button.setAttribute('aria-busy','true'); }
        if (note) note.textContent='Đang xử lý mã QR đánh giá...';
        if (result) result.innerHTML='';
        try {
            const response = await fetch(`/danh-gia/dieu-phoi/${bookingId}`, { method:'POST', headers:{ 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' } });
            const payload = await response.json().catch(()=>({}));
            if (!response.ok || !payload.success) {
                if (note) note.textContent=payload.message || 'Không thể phát hành hoặc lấy mã QR đánh giá.';
                return;
            }
            if (note) note.textContent=payload.created ? 'Đã phát hành mã QR đánh giá.' : 'Đã lấy mã QR đánh giá hiện có.';
            const qrSrc=`/danh-gia/dieu-phoi/${bookingId}/qr.png?v=${Date.now()}`;
            const reviewUrl=payload.reviewUrl ? `<p><a href="${esc(payload.reviewUrl)}" target="_blank" rel="noopener">Mở liên kết đánh giá khách mời</a></p>` : '';
            if (result) result.innerHTML=`${reviewUrl}<img src="${qrSrc}" alt="Mã QR đánh giá cho tiệc ${esc(payload.bookingCode||bookingId)}" class="cw-review-qr-image" />`;
        } catch {
            if (note) note.textContent='Không thể kết nối máy chủ. Vui lòng thử lại.';
        } finally {
            if (button) { button.disabled=false; button.removeAttribute('aria-busy'); }
        }
    }
    $('coordinatorWorkspace').addEventListener('click',event=>{
        const issue=event.target.closest('[data-review-qr-issue]');
        if (issue) { issueReviewQr(Number(issue.dataset.reviewQrIssue), issue); return; }
        const btn=event.target.closest('[data-detail],[data-party]'); if(!btn) return; if(btn.dataset.detail) detail(Number(btn.dataset.detail)); else { selected=Number(btn.dataset.party); renderProgress(); }
    });
    $('cwChecklist').addEventListener('change',event=>{if(!event.target.matches('[data-task]')||!demo)return;const party=items().find(p=>p.bookingId===selected);const t=tasks(party).find(t=>t.id===Number(event.target.dataset.task));t.done=event.target.checked;renderProgress(); const input=$('cwChecklist').querySelector(`[data-task="${t.id}"]`); input?.focus();});
    if (config.standalone && $('cwDemo')) $('cwDemo').onclick=()=>{selected=null;week=monday(today);$('cwDate').value='';work.clear();filters();render();};
    $('cwPrev').onclick=()=>{week=add(week,-7);$('cwDate').value='';renderSchedule();};$('cwNext').onclick=()=>{week=add(week,7);$('cwDate').value='';renderSchedule();};$('cwToday').onclick=()=>{week=monday(today);$('cwDate').value='';renderSchedule();};
    $('cwDate').addEventListener('change',()=>{if ($('cwDate').value) week=monday(date($('cwDate').value));renderSchedule();});
    $('cwClearDate').onclick=()=>{$('cwDate').value='';renderSchedule();};
    $('cwScopeWeek').onclick=()=>{scope='week';if ($('cwDate').value) week=monday(date($('cwDate').value));renderSchedule();};
    $('cwScopeAll').onclick=()=>{scope='all';renderSchedule();};
    for(const id of ['cwWeek','cwList']) $(id).onclick=()=>{mode=id==='cwWeek'?'week':'list';$('cwWeek').classList.toggle('active',mode==='week');$('cwList').classList.toggle('active',mode==='list');$('cwWeek').setAttribute('aria-pressed',String(mode==='week'));$('cwList').setAttribute('aria-pressed',String(mode==='list'));renderSchedule();};
    for(const id of ['cwSearch','cwHall','cwShift']) $(id).addEventListener(id==='cwSearch'?'input':'change',renderSchedule);
    $('cwProgressSearch').oninput=renderProgress;
    document.querySelectorAll('[data-cw-page]').forEach(el=>el.addEventListener('click',event=>{event.preventDefault();setPage(el.dataset.cwPage);}));
    filters();setPage(config.standalone && location.hash==='#progress'?'progress':page);
    if (requestedBookingId && items().some(p=>p.bookingId===requestedBookingId)) detail(requestedBookingId);
})();
