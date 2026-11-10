(() => {
    'use strict';
    const data = JSON.parse(document.getElementById('bannerData')?.textContent || '[]');
    const formDialog = document.getElementById('bannerFormDialog');
    const previewDialog = document.getElementById('bannerPreviewDialog');
    const form = document.getElementById('bannerForm');
    const field = name => form.elements.namedItem(name);
    const openForm = (banner, requestedPlacement = 'HOME') => {
        form.reset();
        document.getElementById('bannerFormTitle').textContent = banner ? 'Chỉnh sửa Banner' : 'Thêm Banner';
        ['BannerId','Version','Placement','Title','Subtitle','Description','NavigationLabel','ImageAlt','PrimaryActionType','PrimaryButtonLabel','PrimaryLink','SecondaryActionType','SecondaryButtonLabel','SecondaryLink','SortOrder'].forEach(name => {
            const key = name.charAt(0).toLowerCase() + name.slice(1);
            if (field(name)) {
                if (name === 'BannerId') {
                    field(name).value = banner?.bannerId ?? '0';
                } else if (name === 'PrimaryActionType' || name === 'SecondaryActionType') {
                    field(name).value = banner?.[key] || 'NONE';
                } else if (name === 'SortOrder') {
                    const placement = banner?.placement || requestedPlacement;
                    field(name).value = banner?.[key] ?? (data.filter(x => x.placement === placement).length + 1);
                } else if (name === 'Placement') {
                    field(name).value = banner?.placement || requestedPlacement;
                } else {
                    field(name).value = banner?.[key] ?? '';
                }
            }
        });
        field('IsVisible').checked = banner?.isVisible ?? false;
        const preview = form.querySelector('.banner-image-preview');
        preview.innerHTML = banner?.imageUrl ? `<img src="${banner.imageUrl}" alt="">` : '<i class="fa-regular fa-image"></i>';
        formDialog.showModal();
    };
    document.querySelectorAll('[data-banner-create]').forEach(button => button.addEventListener('click', () => openForm(null, button.dataset.placement || 'HOME')));
    document.querySelectorAll('[data-banner-edit]').forEach(button => button.addEventListener('click', () => openForm(data.find(x => x.bannerId === Number(button.dataset.bannerEdit)))));
    document.querySelectorAll('[data-banner-preview]').forEach(button => button.addEventListener('click', () => {
        const banner = data.find(x => x.bannerId === Number(button.dataset.bannerPreview));
        previewDialog.querySelector('img').src = banner.imageUrl; previewDialog.querySelector('img').alt = banner.imageAlt;
        previewDialog.querySelector('h2').textContent = banner.title; previewDialog.querySelector('h3').textContent = banner.subtitle; previewDialog.querySelector('p').textContent = banner.description;
        previewDialog.showModal();
    }));
    document.querySelectorAll('[data-dialog-close]').forEach(button => button.addEventListener('click', () => button.closest('dialog').close()));
    document.querySelector('[data-banner-image]')?.addEventListener('change', event => {
        const file = event.target.files?.[0]; if (!file) return;
        if (file.size > 5 * 1024 * 1024) { window.RiversideToast?.warning('Ảnh không được vượt quá 5 MB.'); event.target.value = ''; return; }
        const image = new Image(); image.onload = () => { if (image.width < 1280 || image.height < 720) window.RiversideToast?.warning('Ảnh có độ phân giải thấp. Khuyến nghị tối thiểu 1920×1080.'); URL.revokeObjectURL(image.src); };
        image.src = URL.createObjectURL(file); form.querySelector('.banner-image-preview').innerHTML = `<img src="${image.src}" alt="Ảnh xem trước">`;
    });
    document.querySelectorAll('[data-delete-banner]').forEach(form => form.addEventListener('submit', event => { if (!confirm('Bạn có chắc muốn xóa banner này? Thao tác không thể hoàn tác.')) event.preventDefault(); }));
})();
