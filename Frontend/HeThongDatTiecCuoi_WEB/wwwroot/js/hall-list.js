(() => {
    const page = document.querySelector('.hall-list-page, .hall-detail-page, .menu-list-page, .menu-detail-page');
    if (!page) return;

    page.querySelector('[data-sort-form] select')?.addEventListener('change', event => {
        event.currentTarget.form.submit();
    });
})();
