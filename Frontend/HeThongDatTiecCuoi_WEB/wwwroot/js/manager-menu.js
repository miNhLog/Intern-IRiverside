(() => {
  const page = document.querySelector('.mm-page'); if (!page) return;
  const token = page.querySelector('input[name="__RequestVerificationToken"]').value;
  const esc = value => String(value ?? '').replace(/[&<>'"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));
  const money = value => new Intl.NumberFormat('vi-VN').format(value) + ' đ';
  const toast = (message, error=false) => { const el=document.createElement('div'); el.className='mm-toast'+(error?' error':''); el.textContent=message; document.body.append(el); setTimeout(()=>el.remove(),3000); };
  const open = id => document.getElementById(id).hidden=false;
  const close = modal => modal.hidden=true;
  document.querySelectorAll('[data-modal-open]').forEach(b=>b.onclick=()=>open(b.dataset.modalOpen));
  document.querySelectorAll('[data-modal-close]').forEach(b=>b.onclick=()=>close(b.closest('.mm-modal')));
  document.querySelectorAll('.mm-modal').forEach(m=>m.addEventListener('click',e=>{if(e.target===m) close(m)}));

  document.querySelectorAll('[data-edit]').forEach(button => button.onclick = () => {
    const card=button.closest('.mm-card'), form=document.querySelector('#editModal form');
    form.MenuId.value=card.dataset.menuId; form.MenuCode.value=card.dataset.code; form.MenuName.value=card.dataset.name;
    form.PricePerTable.value=card.dataset.price; form.Description.value=card.dataset.description || ''; open('editModal');
  });

  let selectedCard=null, currentDishes=[];
  document.querySelectorAll('[data-detail-toggle]').forEach(button => button.onclick=async()=>{
    const card=button.closest('.mm-card');
    if(selectedCard===card){ closeDetail(); return; }
    closeDetail(); selectedCard=card; card.classList.add('selected'); button.textContent='Thu gọn';
    const slot=card.closest('.mm-row').querySelector('.mm-detail-slot'); slot.hidden=false; slot.innerHTML='<p>Đang tải chi tiết...</p>';
    try { const response=await fetch(`${page.dataset.detailUrl}/${card.dataset.menuId}/chi-tiet`); if(!response.ok) throw new Error(await errorOf(response)); currentDishes=await response.json(); renderDetail(card,slot); }
    catch(e){ slot.innerHTML=`<div class="mm-alert error">${esc(e.message)}</div>`; }
  });
  function closeDetail(){ if(!selectedCard)return; selectedCard.classList.remove('selected'); selectedCard.querySelector('[data-detail-toggle]').textContent='Xem chi tiết'; selectedCard.closest('.mm-row').querySelector('.mm-detail-slot').hidden=true; selectedCard=null; currentDishes=[]; }
  function renderDetail(card,slot){
    const locked=card.dataset.inUse==='true', inactive=currentDishes.filter(d=>d.status==='INACTIVE').length;
    slot.innerHTML=`<div class="mm-detail-header"><div><h2>${esc(card.dataset.name)} <span class="mm-badge ${card.dataset.status==='ACTIVE'?'active':'inactive'}">${card.dataset.status==='ACTIVE'?'Đang áp dụng':'Tạm ngưng'}</span></h2><p>Mã thực đơn: <b>${esc(card.dataset.code)}</b> · Giá: <b>${money(card.dataset.price)} / bàn</b> · ${currentDishes.length} món</p><p>${esc(card.dataset.description || 'Chưa có mô tả.')}</p></div></div>${locked?'<div class="mm-warning"><i class="fa-solid fa-lock"></i> Thực đơn đã được sử dụng trong đặt tiệc. Thành phần món không thể chỉnh sửa.</div>':''}${inactive?`<div class="mm-warning"><i class="fa-solid fa-triangle-exclamation"></i> Thực đơn hiện có ${inactive} món đang tạm ngưng phục vụ.</div>`:''}${!locked?'<div class="mm-detail-tools"><button class="mm-btn gold" type="button" id="openDishPicker"><i class="fa-solid fa-plus"></i> Thêm món</button></div>':''}<div class="mm-table-wrap"><table class="mm-table"><thead><tr><th></th><th>STT</th><th>Tên món</th><th>Danh mục</th><th>Giá món</th><th>Trạng thái</th><th></th></tr></thead><tbody>${currentDishes.length?currentDishes.map((d,i)=>row(d,i,locked)).join(''):'<tr><td colspan="7">Thực đơn chưa có món ăn.</td></tr>'}</tbody></table></div>`;
    if(!locked){ slot.querySelector('#openDishPicker').onclick=()=>showDishPicker(card); enableDrag(slot,card); slot.querySelectorAll('.mm-remove').forEach(b=>b.onclick=()=>removeDish(card,b.dataset.id)); }
  }
  function row(d,i,locked){return `<tr draggable="${!locked}" data-dish-id="${d.dishId}"><td class="mm-drag">${locked?'':'⋮⋮'}</td><td>${i+1}</td><td><b>${esc(d.dishName)}</b><br><small>${esc(d.dishCode)}</small></td><td>${esc(d.category)}</td><td>${money(d.price)}</td><td><span class="mm-badge ${d.status==='ACTIVE'?'active':'inactive'}">${d.status==='ACTIVE'?'Đang phục vụ':'Tạm ngưng'}</span></td><td>${locked?'':`<button class="mm-remove" data-id="${d.dishId}">Bỏ khỏi thực đơn</button>`}</td></tr>`}
  async function showDishPicker(card){
    const response=await fetch(page.dataset.dishesUrl); if(!response.ok){toast(await errorOf(response),true);return} const all=await response.json(), ids=new Set(currentDishes.map(d=>d.dishId));
    const available=all.filter(d=>!ids.has(d.dishId)), box=document.getElementById('availableDishes'), form=document.getElementById('dishForm'); form.MenuId.value=card.dataset.menuId;
    const draw=()=>{const term=document.getElementById('dishSearch').value.toLowerCase(),cat=document.getElementById('dishCategory').value;box.innerHTML=available.filter(d=>(!term||d.dishName.toLowerCase().includes(term)||d.dishCode.toLowerCase().includes(term))&&(!cat||d.category===cat)).map(d=>`<label class="mm-dish-item"><input type="checkbox" name="DishIds" value="${d.dishId}">${d.imageUrl?`<img src="${esc(d.imageUrl)}" alt="">`:'<span class="mm-dish-image"><i class="fa-solid fa-bowl-food"></i></span>'}<span><b>${esc(d.dishName)}</b><small>${esc(d.category)} · ${money(d.price)}</small></span></label>`).join('')||'<p>Không còn món phù hợp để thêm.</p>'; updateCount();};
    document.getElementById('dishSearch').oninput=draw; document.getElementById('dishCategory').onchange=draw; box.onchange=updateCount; draw(); open('dishModal');
  }
  function updateCount(){document.getElementById('dishSelectedCount').textContent=`Đã chọn ${document.querySelectorAll('#availableDishes input:checked').length} món`;}
  document.getElementById('dishForm').onsubmit=async e=>{e.preventDefault();const form=e.currentTarget,ids=[...form.querySelectorAll('input[name="DishIds"]:checked')].map(x=>x.value);if(!ids.length){toast('Vui lòng chọn ít nhất một món.',true);return}const data=new FormData();data.append('__RequestVerificationToken',token);data.append('MenuId',form.MenuId.value);ids.forEach(id=>data.append('DishIds',id));const r=await fetch('/quan-ly/thuc-don/them-mon',{method:'POST',body:data});if(!r.ok){toast(await errorOf(r),true);return}const card=selectedCard;close(document.getElementById('dishModal'));toast('Đã thêm món vào thực đơn.');card.querySelector('[data-detail-toggle]').click();setTimeout(()=>card.querySelector('[data-detail-toggle]').click(),0);};
  async function removeDish(card,id){if(!confirm('Bỏ món này khỏi thực đơn?'))return;const data=new FormData();data.append('__RequestVerificationToken',token);const r=await fetch(`/quan-ly/thuc-don/${card.dataset.menuId}/bo-mon/${id}`,{method:'POST',body:data});if(!r.ok){toast(await errorOf(r),true);return}currentDishes=currentDishes.filter(d=>String(d.dishId)!==String(id));card.dataset.count=currentDishes.length;renderDetail(card,card.closest('.mm-row').querySelector('.mm-detail-slot'));toast('Đã bỏ món khỏi thực đơn.');}
  function enableDrag(slot,card){let dragged=null,original=[];const tbody=slot.querySelector('tbody');tbody.querySelectorAll('tr[data-dish-id]').forEach(tr=>{tr.ondragstart=()=>{dragged=tr;original=[...tbody.children]};tr.ondragover=e=>{e.preventDefault();if(dragged&&tr!==dragged)tbody.insertBefore(dragged,tr.getBoundingClientRect().top+tr.offsetHeight/2<e.clientY?tr.nextSibling:tr)};tr.ondragend=async()=>{if(!dragged)return;const ids=[...tbody.querySelectorAll('tr[data-dish-id]')].map(x=>x.dataset.dishId),data=new FormData();data.append('__RequestVerificationToken',token);ids.forEach(id=>data.append('dishIds',id));const r=await fetch(`/quan-ly/thuc-don/${card.dataset.menuId}/sap-xep`,{method:'POST',body:data});if(!r.ok){original.forEach(x=>tbody.append(x));toast(await errorOf(r),true)}else{currentDishes.sort((a,b)=>ids.indexOf(String(a.dishId))-ids.indexOf(String(b.dishId)));renderDetail(card,slot);toast('Đã lưu thứ tự món.')}dragged=null;};});}
  async function errorOf(response){try{const data=await response.json();return data.message||'Yêu cầu không thành công.'}catch{return 'Yêu cầu không thành công.'}}
})();
