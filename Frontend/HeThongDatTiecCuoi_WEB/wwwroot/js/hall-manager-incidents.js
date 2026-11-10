(() => {
    const page = document.querySelector('.hall-incident-page');
    if (!page) return;
    const formModal = document.getElementById('hmIncidentFormModal');
    const detailModal = document.getElementById('hmDetailModal');
    const form = document.getElementById('hmIncidentForm');
    const detailBody = document.getElementById('hmDetailBody');
    const errorBox = document.getElementById('hmFormError');
    const idInput = document.getElementById('hmIncidentId');
    const booking = document.getElementById('hmBooking');
    const type = document.getElementById('hmType');
    const severity = document.getElementById('hmSeverity');
    const occurred = document.getElementById('hmOccurred');
    const description = document.getElementById('hmDescription');
    const formTitle = document.getElementById('hmFormTitle');
    const focusableSelector = 'button:not([disabled]):not(.modal-backdrop), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    let returnFocus = null;

    const csrf = () => form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const setBodyState = () => document.body.classList.toggle('incident-modal-open', !formModal.hidden || !detailModal.hidden);
    const localNow = () => { const d=new Date(); d.setMinutes(d.getMinutes()-d.getTimezoneOffset()); return d.toISOString().slice(0,16); };
    const notify = (message, error=false) => error ? window.RiversideToast?.error(message) : window.RiversideToast?.success(message);
    const messageOf = async response => { try { const body=await response.json(); return body.message || 'Yêu cầu không thành công.'; } catch { return 'Không thể kết nối đến hệ thống.'; } };
    const rememberFocus = () => {
        if (!returnFocus && document.activeElement instanceof HTMLElement) returnFocus=document.activeElement;
    };
    const restoreFocus = () => {
        const target=returnFocus;
        returnFocus=null;
        if (target?.isConnected) setTimeout(()=>target.focus(),0);
    };
    const closeForm = (restore=true) => {
        if (formModal.hidden) return;
        formModal.hidden=true;
        setBodyState();
        if (restore) restoreFocus();
    };
    const closeDetail = (restore=true) => {
        if (detailModal.hidden) return;
        detailModal.hidden=true;
        detailModal.removeAttribute('aria-busy');
        setBodyState();
        if (restore) restoreFocus();
    };
    const showForm = data => {
        rememberFocus();
        form.reset(); idInput.value=''; booking.disabled=false; errorBox.hidden=true; occurred.value=localNow();
        formTitle.textContent='Ghi nhận sự cố'; document.querySelector('#hmSaveIncident span').textContent='Lưu báo cáo';
        if(data){ idInput.value=data.id; booking.value=data.bookingId; booking.disabled=true; type.value=data.type; severity.value=data.severity; occurred.value=data.occurred; description.value=data.description; formTitle.textContent='Chỉnh sửa báo cáo'; document.querySelector('#hmSaveIncident span').textContent='Lưu thay đổi'; }
        formModal.hidden=false; setBodyState(); setTimeout(()=>type.focus(),30);
    };
    const openDetail = async id => {
        rememberFocus();
        detailModal.hidden=false; detailModal.setAttribute('aria-busy','true'); setBodyState(); detailBody.className='detail-loading'; detailBody.innerHTML='<i class="fa-solid fa-spinner fa-spin"></i> Đang tải chi tiết...';
        detailModal.querySelector('.modal-card')?.focus();
        const response=await fetch(`/quan-ly-sanh/su-co/${id}`,{headers:{'X-Requested-With':'XMLHttpRequest'}});
        if(!response.ok){ detailBody.textContent=await messageOf(response); detailModal.removeAttribute('aria-busy'); return false; }
        detailBody.className=''; detailBody.innerHTML=await response.text(); detailModal.removeAttribute('aria-busy'); detailBody.querySelector('[data-hm-close-detail]')?.focus(); return true;
    };
    const editCurrent = () => { const root=detailBody.querySelector('.hall-incident-detail'); if(!root)return; closeDetail(false); showForm({id:root.dataset.id,bookingId:root.dataset.bookingId,type:root.dataset.type,severity:root.dataset.severity,occurred:root.dataset.occurred,description:root.dataset.description}); };
    const post = async (url, data) => {
        data.set('__RequestVerificationToken',csrf());
        const response=await fetch(url,{method:'POST',body:data,headers:{'X-Requested-With':'XMLHttpRequest'}});
        if(!response.ok) throw new Error(await messageOf(response));
        return response.json();
    };
    const refreshDetail = (id,message) => { notify(message); const url=new URL(location.href); url.searchParams.set('incidentId',id); setTimeout(()=>location.assign(url),550); };

    document.getElementById('hmOpenCreate')?.addEventListener('click',()=>showForm());
    document.querySelectorAll('[data-hm-close-form]').forEach(x=>x.addEventListener('click',closeForm));
    document.querySelectorAll('[data-hm-close-detail]').forEach(x=>x.addEventListener('click',closeDetail));
    document.addEventListener('keydown',e=>{
        const activeModal=!formModal.hidden?formModal:!detailModal.hidden?detailModal:null;
        if(!activeModal)return;
        if(e.key==='Escape'){
            e.preventDefault();
            if(!formModal.hidden)closeForm();else closeDetail();
            return;
        }
        if(e.key!=='Tab')return;
        const focusable=[...activeModal.querySelectorAll(focusableSelector)].filter(x=>x.offsetParent!==null);
        if(focusable.length===0){e.preventDefault();activeModal.querySelector('.modal-card')?.focus();return;}
        const first=focusable[0],last=focusable[focusable.length-1];
        if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}
        else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}
    });
    document.addEventListener('click',async event=>{
        const view=event.target.closest('.js-hm-detail'); if(view) await openDetail(view.dataset.id);
        const edit=event.target.closest('.js-hm-edit'); if(edit && await openDetail(edit.dataset.id)) editCurrent();
        if(event.target.closest('[data-hm-close-detail]')) closeDetail();
        if(event.target.closest('.js-hm-edit-detail')) editCurrent();
        const accept=event.target.closest('.js-hm-accept');
        if(accept){ if(!confirm('Tiếp nhận sự cố này và chuyển sang Đang xử lý?'))return; accept.disabled=true; try{const result=await post(`/quan-ly-sanh/su-co/${accept.dataset.id}/accept`,new FormData());refreshDetail(accept.dataset.id,result.message);}catch(error){notify(error.message,true);accept.disabled=false;} }
    });
    document.addEventListener('submit',async event=>{
        const resolutionForm=event.target.closest('.hm-resolution-form'); if(!resolutionForm)return;
        event.preventDefault(); const action=event.submitter?.dataset.action || 'save'; const value=resolutionForm.querySelector('textarea').value.trim(); const box=resolutionForm.querySelector('.resolution-error');
        if(value.length<3){box.textContent='Vui lòng nhập hướng giải quyết tối thiểu 3 ký tự.';box.hidden=false;return;}
        if(action==='resolve'&&!confirm('Xác nhận sự cố đã được xử lý hoàn tất?'))return;
        box.hidden=true; const buttons=resolutionForm.querySelectorAll('button'); buttons.forEach(x=>x.disabled=true);
        try{const result=await post(`/quan-ly-sanh/su-co/${resolutionForm.dataset.id}/${action==='resolve'?'resolve':'resolution'}`,new FormData(resolutionForm));refreshDetail(resolutionForm.dataset.id,result.message);}catch(error){box.textContent=error.message;box.hidden=false;buttons.forEach(x=>x.disabled=false);}
    });
    form.addEventListener('submit',async event=>{
        event.preventDefault(); errorBox.hidden=true; if(!form.checkValidity()){form.reportValidity();return;}
        const button=document.getElementById('hmSaveIncident');button.disabled=true;const data=new FormData(form);if(booking.disabled)data.set('BookingId',booking.value);const id=idInput.value;
        try{const result=await post(id?`/quan-ly-sanh/su-co/${id}/edit`:'/quan-ly-sanh/su-co',data);closeForm();notify(result.message);setTimeout(()=>location.reload(),550);}catch(error){errorBox.textContent=error.message;errorBox.hidden=false;}finally{button.disabled=false;}
    });
    const requested=page.dataset.openIncident;if(requested&&/^\d+$/.test(requested))openDetail(requested);
})();
