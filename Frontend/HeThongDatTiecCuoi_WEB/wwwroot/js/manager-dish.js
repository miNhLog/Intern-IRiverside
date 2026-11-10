(() => {
    const page = document.querySelector('.md-page');
    if (!page) return;
    const token = page.querySelector('input[name="__RequestVerificationToken"]').value;
    const detail = document.getElementById('dishDetail');
    let selected = null;
    let current = null;
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[c]));
    const money = v => new Intl.NumberFormat('vi-VN').format(v) + ' đ';
    const toast = (m, e = false) => e ? window.RiversideToast?.error(m) : window.RiversideToast?.success(m);
    const open = id => document.getElementById(id).hidden = false;
    const close = m => m.hidden = true;

    document.querySelectorAll('[data-open]').forEach(b => b.onclick = () => open(b.dataset.open));
    document.querySelectorAll('[data-close]').forEach(b => b.onclick = () => close(b.closest('.md-modal')));
    document.querySelectorAll('.md-modal').forEach(m => m.onclick = e => { if (e.target === m) close(m); });

    document.querySelectorAll('[data-preview]').forEach(input => input.onchange = () => {
        const file = input.files[0];
        const box = input.closest('.md-upload').querySelector('.md-preview');
        if (!file) return;
        if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size > 5 * 1024 * 1024) {
            input.value = '';
            toast('Ảnh phải là JPG, PNG hoặc WEBP và không quá 5 MB.', true);
            return;
        }
        const url = URL.createObjectURL(file);
        box.innerHTML = `<img src="${url}" alt="Xem trước ảnh">`;
    });

    document.querySelectorAll('.md-card').forEach(card => {
        card.addEventListener('click', e => {
            if (e.target.closest('details,button')) return;
            load(card);
        });
        card.addEventListener('keydown', e => {
            if (e.key === 'Enter') load(card);
        });
        card.querySelector('[data-edit]').onclick = () => edit(card);
        card.querySelector('[data-status]').onclick = () => status(card);
    });

    async function load(card) {
        selected?.classList.remove('selected');
        selected = card;
        card.classList.add('selected');
        detail.innerHTML = '<div class="md-detail-empty"><p>Đang tải chi tiết...</p></div>';
        try {
            const r = await fetch(`${page.dataset.detailBase}/${card.dataset.dishId}/chi-tiet`);
            if (!r.ok) throw new Error(await errorOf(r));
            current = await r.json();
            render();
        } catch (e) {
            detail.innerHTML = `<div class="md-alert error">${esc(e.message)}</div>`;
        }
    }

    function render() {
        const d = current;
        const img = d.imageUrl ? `<img src="${esc(d.imageUrl)}" alt="${esc(d.dishName)}">` : '<div class="md-detail-hero"><i class="bi bi-cup-hot" aria-hidden="true"></i></div>';
        const menus = d.menus?.length ? d.menus.map(m => `<div class="md-menu-usage"><span><b>${esc(m.menuName)}</b><small>${esc(m.menuCode)} · ${m.status === 'ACTIVE' ? 'Đang áp dụng' : 'Tạm ngưng'}</small></span><span class="md-menu-type">${m.menuType === 'STANDARD' ? 'Tiêu chuẩn' : 'Tùy chỉnh'}</span></div>`).join('') : '<p>Món ăn chưa được sử dụng trong thực đơn nào.</p>';
        detail.innerHTML = `<div class="md-detail-content">${img}<div class="md-detail-body"><small>${esc(d.dishCode)}</small><h2>${esc(d.dishName)}</h2><p><span class="md-category">${esc(d.category)}</span> · <span class="md-status ${d.status === 'ACTIVE' ? 'active' : 'inactive'}">${d.status === 'ACTIVE' ? 'Đang phục vụ' : 'Tạm ngưng'}</span></p><div class="md-detail-price">${money(d.price)}</div><div class="md-detail-actions"><button class="md-btn light" id="detailEdit">Chỉnh sửa</button><button class="md-btn navy" id="detailStatus">${d.status === 'ACTIVE' ? 'Tạm ngưng' : 'Áp dụng lại'}</button></div><section class="md-usage"><h3>Đang được sử dụng trong thực đơn</h3><p>Món ăn đang được sử dụng trong ${d.usageCount} thực đơn.</p>${menus}</section></div></div>`;
        detail.querySelector('#detailEdit').onclick = () => edit(selected);
        detail.querySelector('#detailStatus').onclick = () => status(selected);
    }

    function edit(card) {
        const f = document.querySelector('#editDishModal form');
        f.DishId.value = card.dataset.dishId;
        f.DishCode.value = card.dataset.code;
        f.DishName.value = card.dataset.name;
        f.Category.value = card.dataset.category;
        f.Price.value = card.dataset.price;
        f.DeleteImage.checked = false;
        f.NewImage.value = '';
        const box = f.querySelector('.md-preview');
        box.innerHTML = card.dataset.image ? `<img src="${esc(card.dataset.image)}" alt="Ảnh hiện tại">` : '<i class="bi bi-image" aria-hidden="true"></i>';
        open('editDishModal');
    }

    async function status(card) {
        const inactive = card.dataset.status === 'ACTIVE';
        const form = document.getElementById('statusForm');
        const alertBanner = document.getElementById('statusAlertBanner');
        const menuContainer = document.getElementById('statusAffectedMenusContainer');
        const menuList = document.getElementById('statusAffectedMenusList');
        const statusTitle = document.getElementById('statusTitle');
        const statusMessage = document.getElementById('statusMessage');
        const statusConfirm = document.getElementById('statusConfirm');

        form.dataset.id = card.dataset.dishId;
        form.dataset.status = inactive ? 'INACTIVE' : 'ACTIVE';

        if (inactive) {
            statusTitle.textContent = 'Cảnh báo: Tạm ngưng món ăn';
            statusConfirm.textContent = 'Xác nhận tạm ngưng';
            statusMessage.textContent = 'Đang kiểm tra các thực đơn chứa món ăn...';
            if (alertBanner) { alertBanner.hidden = true; alertBanner.style.display = 'none'; }
            if (menuContainer) { menuContainer.hidden = true; menuContainer.style.display = 'none'; }
            open('statusModal');

            try {
                const r = await fetch(`${page.dataset.detailBase}/${card.dataset.dishId}/chi-tiet`);
                if (r.ok) {
                    const data = await r.json();
                    if (data.menus && data.menus.length > 0) {
                        if (alertBanner) { alertBanner.hidden = false; alertBanner.style.display = 'flex'; }
                        if (menuContainer) { menuContainer.hidden = false; menuContainer.style.display = 'block'; }
                        statusMessage.textContent = `Món ăn này hiện đang được sử dụng trong ${data.menus.length} thực đơn tiệc cưới. Khi tạm ngưng, món ăn sẽ không thể được thêm vào các thực đơn tiệc mới:`;
                        menuList.innerHTML = data.menus.map(m => {
                            const typeLabel = m.menuType === 'STANDARD' ? 'Tiêu chuẩn' : 'Tùy chỉnh';
                            const typeClass = m.menuType === 'STANDARD' ? 'standard' : 'custom';
                            return `
                                <div class="md-affected-item">
                                    <div class="md-affected-item-info">
                                        <span class="md-affected-code">${esc(m.menuCode)}</span>
                                        <span class="md-affected-name">${esc(m.menuName)}</span>
                                    </div>
                                    <span class="md-affected-type ${typeClass}">${typeLabel}</span>
                                </div>
                            `;
                        }).join('');
                    } else {
                        if (alertBanner) { alertBanner.hidden = true; alertBanner.style.display = 'none'; }
                        if (menuContainer) { menuContainer.hidden = true; menuContainer.style.display = 'none'; }
                        statusMessage.textContent = 'Món ăn này chưa được sử dụng trong thực đơn nào. Bạn có chắc chắn muốn tạm ngưng phục vụ món ăn này không?';
                    }
                } else {
                    statusMessage.textContent = 'Bạn có chắc chắn muốn tạm ngưng phục vụ món ăn này không?';
                }
            } catch {
                statusMessage.textContent = 'Bạn có chắc chắn muốn tạm ngưng phục vụ món ăn này không?';
            }
        } else {
            statusTitle.textContent = 'Xác nhận áp dụng lại';
            statusConfirm.textContent = 'Áp dụng lại';
            if (alertBanner) { alertBanner.hidden = true; alertBanner.style.display = 'none'; }
            if (menuContainer) { menuContainer.hidden = true; menuContainer.style.display = 'none'; }
            statusMessage.textContent = `Bạn có chắc muốn kích hoạt và phục vụ lại món ăn "${esc(card.dataset.name)}" không?`;
            open('statusModal');
        }
    }

    document.getElementById('statusForm').onsubmit = async e => {
        e.preventDefault();
        const f = e.currentTarget;
        const data = new FormData();
        data.append('__RequestVerificationToken', token);
        data.append('status', f.dataset.status);
        const r = await fetch(`/quan-ly/mon-an/${f.dataset.id}/trang-thai`, { method: 'POST', body: data });
        if (!r.ok) {
            toast(await errorOf(r), true);
            return;
        }
        close(document.getElementById('statusModal'));
        toast('Cập nhật trạng thái món ăn thành công.');
        location.reload();
    };

    async function errorOf(r) {
        try {
            const d = await r.json();
            return d.message || 'Yêu cầu không thành công.';
        } catch {
            return 'Yêu cầu không thành công.';
        }
    }
})();
