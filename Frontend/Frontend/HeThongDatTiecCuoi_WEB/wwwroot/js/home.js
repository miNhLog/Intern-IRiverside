(() => {
    const page = document.querySelector('.home-page');
    if (!page) return;

    const toast = page.querySelector('.home-toast');
    let toastTimer;
    let hideTimer;

    function showToast(message) {
        clearTimeout(toastTimer);
        clearTimeout(hideTimer);
        toast.textContent = message;
        toast.hidden = false;
        requestAnimationFrame(() => toast.classList.add('visible'));
        toastTimer = setTimeout(() => {
            toast.classList.remove('visible');
            hideTimer = setTimeout(() => { toast.hidden = true; }, 220);
        }, 2800);
    }

    page.querySelectorAll('[data-coming-soon]').forEach(element => {
        element.addEventListener('click', event => {
            event.preventDefault();
            showToast('Chức năng đang được phát triển.');
            closeMobileMenu();
        });
    });
    page.querySelector('[data-search]').addEventListener('click', () => {
        showToast('Chức năng tìm kiếm sảnh đang được phát triển.');
    });
    page.querySelectorAll('img[data-fallback]').forEach(image => {
        const useFallback = () => {
            const fallback = image.dataset.fallback;
            if (fallback && image.getAttribute('src') !== fallback) image.src = fallback;
        };
        image.addEventListener('error', useFallback);
        if (image.complete && image.naturalWidth === 0) useFallback();
    });

    const menuButton = page.querySelector('.mobile-menu-toggle');
    const mobileMenu = page.querySelector('.mobile-nav');
    const mobileBackdrop = page.querySelector('.mobile-nav-backdrop');
    function closeMobileMenu() {
        mobileMenu.hidden = true;
        mobileBackdrop.hidden = true;
        menuButton.setAttribute('aria-expanded', 'false');
        menuButton.setAttribute('aria-label', 'Mở menu');
        menuButton.querySelector('.material-symbols-outlined').textContent = 'menu';
    }
    menuButton.addEventListener('click', () => {
        const opening = mobileMenu.hidden;
        mobileMenu.hidden = !opening;
        mobileBackdrop.hidden = !opening;
        menuButton.setAttribute('aria-expanded', String(opening));
        menuButton.setAttribute('aria-label', opening ? 'Đóng menu' : 'Mở menu');
        menuButton.querySelector('.material-symbols-outlined').textContent = opening ? 'close' : 'menu';
    });
    document.addEventListener('click', event => {
        if (!mobileMenu.hidden && !page.querySelector('.home-header').contains(event.target)) closeMobileMenu();
    });
    mobileBackdrop.addEventListener('click', closeMobileMenu);
    window.matchMedia('(max-width: 1023px)').addEventListener('change', event => {
        if (!event.matches) closeMobileMenu();
    });

    const modal = page.querySelector('#aiAssistantModal');
    const dialog = modal.querySelector('.ai-dialog');
    const input = modal.querySelector('#aiChatInput');
    const messages = modal.querySelector('#aiMessages');
    let previousFocus = null;

    function openAi() {
        previousFocus = document.activeElement;
        modal.classList.add('open');
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('home-modal-open');
        closeMobileMenu();
        input.focus();
    }
    function closeAi() {
        modal.classList.remove('open');
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('home-modal-open');
        if (previousFocus && previousFocus.isConnected) previousFocus.focus();
    }
    page.querySelectorAll('[data-open-ai]').forEach(button => button.addEventListener('click', openAi));
    modal.querySelectorAll('[data-close-ai]').forEach(button => button.addEventListener('click', closeAi));
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            if (modal.classList.contains('open')) closeAi();
            else closeMobileMenu();
        }
        if (event.key === 'Tab' && modal.classList.contains('open')) {
            const focusable = [...dialog.querySelectorAll('button, input')].filter(item => !item.disabled);
            if (event.shiftKey && document.activeElement === focusable[0]) {
                event.preventDefault();
                focusable.at(-1).focus();
            } else if (!event.shiftKey && document.activeElement === focusable.at(-1)) {
                event.preventDefault();
                focusable[0].focus();
            }
        }
    });

    function addMessage(message, sender) {
        const row = document.createElement('div');
        row.className = `chat-row ${sender}`;
        if (sender === 'assistant') {
            const avatar = document.createElement('span');
            avatar.className = 'chat-avatar';
            avatar.setAttribute('aria-hidden', 'true');
            avatar.textContent = 'AI';
            row.appendChild(avatar);
        }
        const text = document.createElement('p');
        text.textContent = message;
        row.appendChild(text);
        messages.appendChild(row);
        messages.scrollTop = messages.scrollHeight;
    }
    function sendMessage() {
        const message = input.value.trim();
        if (!message) return;
        addMessage(message, 'user');
        input.value = '';
        addMessage('Tính năng tư vấn AI đang được phát triển.', 'assistant');
        input.focus();
    }
    modal.querySelector('#aiChatForm').addEventListener('submit', event => {
        event.preventDefault();
        sendMessage();
    });
    modal.querySelectorAll('[data-quick-prompt]').forEach(button => {
        button.addEventListener('click', () => {
            input.value = button.dataset.quickPrompt;
            sendMessage();
        });
    });

    const hallGrid = page.querySelector('#featuredHallGrid');
    if (hallGrid) {
        const cards = [...hallGrid.querySelectorAll('.hall-card')];
        let currentHall = 0;
        function moveHall(direction) {
            currentHall = (currentHall + direction + cards.length) % cards.length;
            cards.forEach(card => card.classList.remove('is-highlighted'));
            cards[currentHall].classList.add('is-highlighted');
            cards[currentHall].scrollIntoView({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });
        }
        page.querySelector('[data-hall-prev]').addEventListener('click', () => {
            moveHall(-1);
        });
        page.querySelector('[data-hall-next]').addEventListener('click', () => {
            moveHall(1);
        });
    } else {
        page.querySelectorAll('.hall-controls button').forEach(button => { button.disabled = true; });
    }
})();
