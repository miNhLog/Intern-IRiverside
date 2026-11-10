(() => {
 'use strict';
 const root=document.getElementById('checklistApp'); if(!root)return;
 const $=id=>root.querySelector('#'+id);
 const e=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const format=value=>value.split('-').reverse().join('/');
 const queryParams=new URLSearchParams(location.search),requestedBookingId=Number(queryParams.get('bookingId'))||null;
 let role='coordinator',today='',selected=requestedBookingId,tab=queryParams.get('tab')==='history'?'history':'tasks',parties=[],busy=false,saveDialog;
 const stateNames={missing:'Chưa tạo checklist',unassigned:'Chưa phân công',active:'Đang thực hiện',review:'Chờ kiểm tra',approved:'Đã bàn giao',cancelled:'Đã hủy',closed:'Tiệc đã kết thúc'};
 const taskNames={todo:'Chưa làm',doing:'Đang thực hiện',done:'Hoàn thành'};
 const shiftLabel=p=>(/^ca\s/i.test(p.shift)?p.shift:'Ca '+p.shift)+(p.shiftHours?' ('+p.shiftHours+')':'');
 const party=()=>parties.find(p=>p.id===selected);
 const visibleParties=()=>parties;
 const isDone=t=>t.status==='done'&&t.completionValid;
 const completed=p=>p.tasks.filter(isDone).length;
 const progress=p=>p.tasks.length?Math.round(completed(p)/p.tasks.length*100):0;
 const overdue=t=>!isDone(t)&&t.due<today;
 const canEdit=p=>p.canEdit;
 const hasMissing=p=>p.tasks.some(t=>t.required&&!isDone(t));
 const badge=p=>`<span class="pill ${p.state}">${stateNames[p.state]||e(p.state)}</span>`;
 function toast(text,type='success'){window.RiversideToast?.[type]?.(text);}
 function modal(title,body,label,callback){$('dialogTitle').textContent=title;$('dialogBody').innerHTML=body;$('dialogSubmit').textContent=label;saveDialog=callback;$('modal').showModal();}
 const field=(label,name,value='',type='text',extra='')=>`<label>${label}<input name="${name}" type="${type}" value="${e(value)}" ${extra}></label>`;
 const reason=label=>`<label>${label}<textarea name="reason" required minlength="3" maxlength="500" placeholder="Nhập nội dung cụ thể..."></textarea></label>`;
 const empty=(title,text)=>`<div class="empty"><strong>${title}</strong>${text}</div>`;
 async function responseData(response){
  if(response.status===401 || (response.redirected && new URL(response.url,location.href).pathname==='/login'))
   throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
  const type=response.headers.get('content-type')||'';
  if(!type.includes('json')){
   if(response.status===404)throw new Error('Không tìm thấy đường dẫn xử lý checklist. Vui lòng tải lại trang rồi thử lại.');
   if(response.status===400)throw new Error('Yêu cầu hoặc mã xác thực trang không còn hợp lệ. Vui lòng tải lại trang rồi thử lại.');
   if(response.status===403)throw new Error('Bạn không có quyền thực hiện thao tác checklist này.');
   if(response.status>=500)throw new Error('Máy chủ gặp lỗi khi xử lý checklist. Vui lòng thử lại sau.');
   throw new Error('Máy chủ trả về phản hồi checklist không hợp lệ. Vui lòng tải lại trang.');
  }
  let data;
  try{data=await response.json();}catch{throw new Error('Không đọc được dữ liệu checklist từ máy chủ. Vui lòng thử lại.');}
  if(!response.ok)throw new Error(data.message||data.title||'Không thể lưu thay đổi. Vui lòng tải lại.');
  return data;
 }
 async function load(){
  const data=await responseData(await fetch(root.dataset.url,{headers:{Accept:'application/json'},cache:'no-store'}));
  parties=data.parties;role=data.role;today=data.today;
  for(const p of parties)for(const h of p.history)h.time=new Date(/(Z|[+-]\d{2}:\d{2})$/i.test(h.time)?h.time:h.time+'+07:00').toLocaleString('vi-VN',{timeZone:'Asia/Ho_Chi_Minh'});
  $('loadError').hidden=true;render();
 }
 async function command(action,body={}){
  if(busy)throw new Error('Đang lưu thay đổi. Vui lòng chờ.');
  const p=party();busy=true;
  const control=root.querySelector('.detail-panel');control.inert=true;
  try{
   const data=await responseData(await fetch(`${root.dataset.commandUrl}/${p.id}/${action}`,{method:'POST',headers:{'Content-Type':'application/json','RequestVerificationToken':$('checklistToken').querySelector('input').value},body:JSON.stringify({version:p.version,...body})}));
   await load();toast(data.message||'Đã lưu thay đổi.');
  }catch(error){
   try{await load();}catch{ $('loadError').textContent='Không tải được dữ liệu mới. Vui lòng chọn Làm mới.';$('loadError').hidden=false; }
   throw error;
  }finally{busy=false;control.inert=false;}
 }
  function render(){
  const all=visibleParties(), query=$('search').value.trim().toLocaleLowerCase('vi'), filtered=all.filter(p=>`${p.code} ${p.customer}`.toLocaleLowerCase('vi').includes(query));
  if(!filtered.some(p=>p.id===selected)&&!(requestedBookingId&&selected===requestedBookingId&&!all.some(p=>p.id===requestedBookingId)))selected=filtered[0]?.id??null;
  $('listTitle').textContent=role==='manager'?'Tiệc thuộc sảnh quản lý':'Tiệc của tôi';$('partyCount').textContent=filtered.length;
  $('roleTip').textContent=role==='manager'?'Quản lý công việc theo sảnh phụ trách. Kiểm tra checklist và sự cố trước khi xác nhận bàn giao.':'Hoàn thành các công việc bắt buộc rồi gửi quản lý kiểm tra bàn giao.';
  const active=all.filter(p=>p.state==='active'||p.needsTimingRepair&&!['closed','cancelled','unassigned'].includes(p.state)), tasks=active.flatMap(p=>p.tasks), done=tasks.filter(isDone).length;
  const stat=(icon,label,n,sub,tone='')=>`<article class="stat ${tone}"><span class="stat-icon">${icon}</span><div><small>${label}</small><strong>${n}</strong><em>${sub}</em></div></article>`;
  $('stats').innerHTML=stat('☷','Công việc chưa hoàn thành',tasks.length-done,'Đếm từng đầu việc checklist')+stat('◷','Công việc quá hạn',tasks.filter(overdue).length,'Theo ngày Việt Nam','red')+stat('⇧','Tiệc chờ kiểm tra',all.filter(p=>p.state==='review').length,'Đã gửi bàn giao','blue')+stat('✓','Tiệc đã bàn giao',all.filter(p=>p.state==='approved').length,'Quản lý đã xác nhận','green');
  $('partyList').innerHTML=filtered.length?filtered.map(p=>`<button type="button" class="party ${p.id===selected?'selected':''}" data-party="${p.id}" aria-pressed="${p.id===selected}"><strong>${e(p.code)}</strong><p>${e(p.customer)}</p><small>${format(p.date)} · ${e(shiftLabel(p))}<br>${e(p.hall)} · ${p.tables} bàn</small>${p.tasks.length?`<div class="bar"><span style="width:${progress(p)}%"></span></div>`:''}<div class="party-bottom">${badge(p)}<span>${p.tasks.length?`${completed(p)}/${p.tasks.length} việc · ${progress(p)}%`:'Chưa tạo checklist'}</span></div></button>`).join(''):empty('Không có tiệc phù hợp','Thử đổi từ khóa hoặc kiểm tra phân công của bạn.');
  const p=party();if(!p){$('detail').innerHTML=requestedBookingId&&selected===requestedBookingId?empty('Không thể mở tiệc từ thông báo','Tiệc không còn trong phạm vi truy cập của bạn hoặc không còn tồn tại. Chọn tiệc khác trong danh sách để tiếp tục.'):empty('Chọn một tiệc','Thông tin và checklist sẽ hiển thị tại đây.');return;}
  renderDetail(p);
 }
 function renderDetail(p){
  const editing=canEdit(p), manager=p.canManage, locked=['approved','cancelled','closed'].includes(p.state);
  let actions='';
  if(manager&&p.state==='missing')actions='<button class="btn primary" data-action="initialize">Tạo checklist tiêu chuẩn</button>'; else if(manager&&p.state==='unassigned')actions='<button class="btn primary" data-action="assign">＋ Phân công điều phối viên</button>';
  else if(manager&&!locked){actions='<button class="btn" data-action="add">＋ Thêm công việc</button><button class="btn" data-action="reassign">Đổi điều phối viên</button><button class="btn" data-action="reschedule">Yêu cầu đổi lịch</button>';}
  if(p.canRepairTiming)actions+='<button class="btn" data-action="repair-timing">Mở lại checklist sai thời điểm</button>';
  if(editing)actions='<button class="btn" data-action="issue">⚑ Báo cáo sự cố</button>';
  let notice=p.state==='closed'?'<div class="callout">Tiệc đã kết thúc; checklist và lịch sử chỉ được xem lại.</div>':'';
  if(p.state==='review')notice='<div class="callout">Checklist đã gửi bàn giao và tạm khóa cập nhật. Quản lý sảnh kiểm tra rồi xác nhận hoặc yêu cầu làm lại.</div>';
  if(p.state==='approved')notice='<div class="callout">✓ Quản lý đã xác nhận bàn giao. Checklist được khóa và giữ lịch sử. Trạng thái tiệc vẫn là “'+e(p.bookingState)+'”.</div>';
  if(p.state==='cancelled')notice='<div class="callout gold">Tiệc đã hủy. Ngừng thực hiện checklist, giữ toàn bộ tiến độ và lịch sử để tra cứu.</div>';
  if(p.returnReason&&p.state==='active')notice=`<div class="callout gold"><b>Yêu cầu làm lại từ quản lý:</b> ${e(p.returnReason)}<br>Công việc liên quan đã được mở lại. Hoàn thành và gửi bàn giao lần nữa.</div>`;
  $('detail').innerHTML=`<div class="detail-head"><div class="detail-top"><span class="eyebrow">${p.code} · ĐIỀU PHỐI TIỆC</span>${badge(p)}</div><h2>${e(p.customer)}</h2><div class="detail-meta">${format(p.date)} · ${e(shiftLabel(p))} · ${e(p.hall)} · ${p.tables} bàn<br>Phụ trách: <b>${e(p.coordinator||'Chưa phân công')}</b> &nbsp;·&nbsp; Trạng thái tiệc: <b>${e(p.bookingState)}</b></div><div class="detail-actions">${actions}</div>${p.tasks.length?`<div class="progress-caption"><span>${completed(p)}/${p.tasks.length} công việc hoàn thành</span><strong>${progress(p)}%</strong></div><div class="bar"><span style="width:${progress(p)}%"></span></div>`:''}</div><div class="tabs"><button class="tab ${tab==='tasks'?'active':''}" data-tab="tasks">Checklist <span>${p.tasks.length}</span></button><button class="tab ${tab==='issues'?'active':''}" data-tab="issues">Sự cố <span>${p.issues.filter(i=>!i.resolved).length}</span></button><button class="tab ${tab==='history'?'active':''}" data-tab="history">Lịch sử <span>${p.history.length}</span></button></div>${notice}${p.timingMessage?`<div class="callout gold">${e(p.timingMessage)}</div>`:''}<div id="tabContent"></div>`;
  if(tab==='history')$('tabContent').innerHTML=`<div class="timeline">${p.history.map(h=>`<article class="timeline-item"><strong>${e(h.text)}</strong><p>${e(h.by)}</p><small>${e(h.time)}</small></article>`).join('')}</div>`;
  else if(tab==='issues')$('tabContent').innerHTML=p.issues.length?p.issues.map(i=>`<article class="issue-card"><div class="issue-top"><span class="pill ${i.resolved?'approved':'cancelled'}">${i.resolved?'Đã xử lý':'Đang mở'}</span><small>${e(i.code)}</small></div><h3>${e(i.title)}</h3><p>${e(i.description)}</p><small>${e(i.by)}</small>${manager&&!i.resolved&&!locked?`<div style="margin-top:12px"><a class="btn small" href="/quan-ly-sanh/su-co">Mở trang xử lý sự cố</a></div>`:''}</article>`).join(''):empty('Chưa có sự cố được ghi nhận','Báo cáo sự cố liên kết trực tiếp với tiệc, độc lập với trạng thái công việc.');
  else if(!p.tasks.length)$('tabContent').innerHTML=empty('Checklist chưa được tạo',manager?'Chọn “Phân công điều phối viên” để bắt đầu từ mẫu tiêu chuẩn.':'Quản lý sảnh sẽ phân công và tạo checklist cho tiệc.');
  else{
   $('tabContent').innerHTML=`<div class="checklist">${['Trước tiệc','Trong tiệc','Sau tiệc'].map((group,n)=>{const rows=p.tasks.filter(t=>t.group===group);return `<section class="task-group"><h3>${n+1}. ${group}<span>${rows.filter(isDone).length}/${rows.length} hoàn thành</span></h3>${rows.map(t=>`<article class="task ${isDone(t)?'done':''}"><span class="task-mark">${isDone(t)?'✓':t.status==='doing'?'◐':'○'}</span><div class="task-copy"><span class="task-name">${e(t.name)}</span>${t.required?'<span class="required" title="Bắt buộc hoàn thành trước khi bàn giao">*</span>':''}<div class="task-meta"><span class="${overdue(t)?'late':''}">${overdue(t)?'Quá hạn · ':''}Hạn ${format(t.due)}</span><span>${t.required?'Bắt buộc':'Không bắt buộc'}</span>${t.by?`<span>Cập nhật: ${e(t.by)}</span>`:''}</div>${t.timingMessage?`<div class="task-note">${e(t.timingMessage)}</div>`:''}${t.note?`<div class="task-note">${e(t.note)}</div>`:''}</div><div class="task-buttons">${editing?`<select data-task="${t.id}" aria-label="Trạng thái: ${e(t.name)}">${Object.entries(taskNames).map(([v,label])=>`<option value="${v}" ${t.status===v?'selected':''} ${(v==='done'&&!t.canComplete||v==='doing'&&!t.canStart)?'disabled':''}>${label}</option>`).join('')}</select><button class="icon-btn" data-note="${t.id}" title="Ghi chú công việc" aria-label="Ghi chú: ${e(t.name)}">✎</button>`:`<span class="pill ${isDone(t)?'approved':t.status==='doing'?'active':''}">${t.status==='done'&&!t.completionValid?'Cần làm lại':taskNames[t.status]}</span>`}${manager&&['active','review'].includes(p.state)?`<button type="button" class="btn small danger" data-delete="${t.id}" aria-label="Xóa công việc: ${e(t.name)}">Xóa</button>`:''}</div></article>`).join('')}</section>`;}).join('')}</div>`;
   if(editing){const missing=p.tasks.filter(t=>t.required&&!isDone(t)).length;$('tabContent').innerHTML+=`<footer class="handover"><p><strong>${missing?`Còn ${missing} công việc bắt buộc chưa hoàn thành`:'Đã hoàn thành các công việc bắt buộc'}</strong>${missing?'Hoàn thành các mục có dấu * để gửi bàn giao.':(p.canHandover?'Sẵn sàng gửi quản lý kiểm tra bàn giao.':e(p.timingMessage||'Chưa đủ điều kiện bàn giao.'))}${p.issues.some(i=>!i.resolved)?'<br>Tiệc còn sự cố đang mở. Quản lý sẽ thấy khi kiểm tra.':''}</p><button class="btn primary" data-action="submit" ${missing||!p.canHandover?'disabled':''}>Gửi bàn giao →</button></footer>`;}
   if(manager&&p.state==='review')$('tabContent').innerHTML+=`<footer class="handover"><p><strong>Kiểm tra trước khi xác nhận</strong>Đối chiếu checklist và ${p.issues.filter(i=>!i.resolved).length} sự cố đang mở.<br>Xác nhận bàn giao không tự hoàn tất tiệc.</p><div class="detail-actions"><button class="btn" data-action="return">Yêu cầu làm lại</button><button class="btn primary" data-action="approve" ${!p.canHandover||hasMissing(p)?'disabled':''}>Xác nhận bàn giao</button></div></footer>`;
  }
 }

 function action(name){
  const p=party();if(!p||busy)return;
  if(name==='assign'){location.href='/Home/CoordinationAssignments?search='+encodeURIComponent(p.code);return;}
  if(name==='initialize')modal('Tạo checklist tiêu chuẩn',`<p>Tạo 8 công việc cho tiệc ${e(p.code)}, gắn với điều phối viên đang được giao.</p>`,'Tạo checklist',()=>command('initialize'));
  if(name==='add'&&p.canManage)modal('Thêm công việc riêng cho tiệc',`${field('Tên công việc','name','','text','required maxlength="180"')}<label>Giai đoạn<select name="group"><option>Trước tiệc</option><option>Trong tiệc</option><option>Sau tiệc</option></select></label>${field('Hạn hoàn thành','due',p.date,'date','required')}<label class="checkbox-label"><input type="checkbox" name="required" checked> Bắt buộc hoàn thành trước khi gửi bàn giao</label>`,'Thêm công việc',f=>command('add',{name:f.get('name').trim(),group:f.get('group'),dueDate:f.get('due'),required:f.has('required')}));
  if(name==='reassign'){location.href='/Home/CoordinationAssignments?search='+encodeURIComponent(p.code);return;}
  if(name==='reschedule'){location.href='/HallManagerScheduleChange';return;}
  if(name==='repair-timing'&&p.canRepairTiming)modal('Mở lại checklist sai thời điểm',`<p>Chỉ mở lại các việc hoàn thành sai thời điểm hoặc thiếu thời điểm xác minh. Giữ nguyên lịch sử và các việc hợp lệ.</p>${reason('Lý do mở lại')}`,'Mở lại checklist',f=>command('repair-timing',{note:f.get('reason').trim()}));
  if(name==='submit'&&p.canEdit&&p.canHandover&&!hasMissing(p))modal('Gửi bàn giao cho quản lý',`<p>Các công việc bắt buộc đã hoàn thành. Checklist sẽ tạm khóa chờ quản lý kiểm tra.</p>${p.issues.some(i=>!i.resolved)?'<p>⚑ Tiệc còn sự cố đang mở; thông tin được giữ kèm bàn giao.</p>':''}<label>Ghi chú bàn giao<textarea name="note" maxlength="500"></textarea></label>`,'Gửi bàn giao',f=>command('submit',{note:f.get('note')}));
  if(name==='approve'&&p.canManage&&p.canHandover&&!hasMissing(p)&&p.state==='review')modal('Xác nhận bàn giao checklist',`<p>Checklist sẽ khóa cập nhật. Trạng thái tiệc được giữ nguyên.</p>${p.issues.some(i=>!i.resolved)?'<p>⚑ Tiệc còn sự cố đang mở. Vui lòng kiểm tra trước khi xác nhận.</p>':''}<label>Ghi chú kiểm tra<textarea name="note" maxlength="500"></textarea></label>`,'Xác nhận bàn giao',f=>command('approve',{note:f.get('note')}));
  if(name==='return'&&p.canManage&&p.state==='review')modal('Yêu cầu thực hiện lại',`<label>Công việc cần làm lại<select name="task">${p.tasks.map(t=>`<option value="${t.id}">${e(t.name)}</option>`).join('')}</select></label>${reason('Nội dung cần bổ sung / làm lại')}`,'Gửi yêu cầu làm lại',f=>command('return',{taskId:Number(f.get('task')),note:f.get('reason').trim()}));
  if(name==='issue'&&p.canEdit){location.href='/dieu-phoi/su-co?bookingId='+p.id;}
 }
 $('refresh').onclick=async()=>{if(busy)return;try{await load();toast('Đã tải dữ liệu mới nhất.');}catch(error){$('loadError').textContent=error.message;$('loadError').hidden=false;}};
 $('search').oninput=()=>{if(!busy)render();};
 $('partyList').onclick=event=>{if(busy)return;const button=event.target.closest('[data-party]');if(button){selected=Number(button.dataset.party);tab='tasks';render();}};
 $('detail').onclick=event=>{const btn=event.target.closest('button');if(!btn||busy)return;
  if(btn.dataset.tab){tab=btn.dataset.tab;renderDetail(party());return;}
  if(btn.dataset.action){action(btn.dataset.action);return;}
  const p=party();
  if(btn.dataset.delete&&p.canManage&&['active','review'].includes(p.state)){
   const t=p.tasks.find(t=>t.id===Number(btn.dataset.delete));if(!t)return;
   modal('Xóa công việc',`<p>Xóa công việc <strong>${e(t.name)}</strong> khỏi checklist tiệc ${e(p.code)}?</p><p>Tiến độ sẽ được tính lại. Lịch sử công việc được giữ lại; checklist chờ kiểm tra sẽ được mở lại để gửi bàn giao lần nữa.</p>${reason('Lý do xóa công việc')}`,'Xóa công việc',f=>command('delete',{taskId:t.id,note:f.get('reason').trim()}));
   return;
  }
  if(btn.dataset.note&&p.canEdit){const t=p.tasks.find(t=>t.id===Number(btn.dataset.note));modal('Ghi chú công việc',`<p>${e(t.name)}</p><label>Ghi chú<textarea name="note" maxlength="500">${e(t.note)}</textarea></label>`,'Lưu ghi chú',f=>command('note',{taskId:t.id,note:f.get('note')}));}
 };
 $('detail').onchange=async event=>{const select=event.target.closest('[data-task]'),p=party();if(!select||!p?.canEdit||busy)return;try{await command('task',{taskId:Number(select.dataset.task),status:select.value});}catch(error){toast(error.message,'error');}};
 $('dialogForm').onsubmit=async event=>{event.preventDefault();if(busy)return;const form=event.target;const f=new FormData(form);for(const name of ['name','reason'])if(f.has(name)&&!String(f.get(name)).trim()){toast('Vui lòng nhập nội dung hợp lệ.','warning');return;}$('dialogSubmit').disabled=true;try{await saveDialog(f);$('modal').close();}catch(error){toast(error.message,'error');}finally{$('dialogSubmit').disabled=false;}};
 $('close').onclick=$('back').onclick=()=>{if(!busy)$('modal').close();};
 $('detail').innerHTML=empty('Đang tải checklist...','Vui lòng chờ trong giây lát.');
 load().catch(error=>{$('loadError').textContent=error.message;$('loadError').hidden=false;$('detail').innerHTML=empty('Không tải được checklist','Chọn Làm mới để thử lại.');});
})();
