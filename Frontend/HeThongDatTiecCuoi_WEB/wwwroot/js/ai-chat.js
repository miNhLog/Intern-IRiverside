/* UC10 — Trợ lý ảo AI Riverside Palace. IIFE + null-guard, không đụng JS hiện tại. */
(function () {
    'use strict';

    var history = [];          // [{role, content}]
    var lastEntities = null;
    var sending = false;

    var QUICK_QS = [
        'Giá sảnh cho 30 bàn?',
        'Thực đơn tiệc cưới bao nhiêu một bàn?',
        'Chính sách đặt cọc và giữ sảnh 48h?'
    ];

    function $(id) { return document.getElementById(id); }

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    function addMsg(text, who) {
        var box = $('aiChatMsgs');
        if (!box) return null;
        var d = document.createElement('div');
        d.className = 'ai-msg ' + (who === 'user' ? 'user' : 'bot');
        d.textContent = text;
        box.appendChild(d);
        box.scrollTop = box.scrollHeight;
        return d;
    }

    function greeting() {
        addMsg('Dạ em chào Quý khách ạ! Em là trợ lý ảo Riverside Palace — hỏi em về sảnh tiệc, thực đơn, decor, chính sách đặt cọc/giữ chỗ 48h nhé! Hoặc gọi Hotline 0933 777 888 ạ!', 'bot');
        var box = $('aiChatMsgs');
        if (!box) return;
        var q = document.createElement('div');
        q.className = 'ai-quick';
        QUICK_QS.forEach(function (t) {
            var b = document.createElement('button');
            b.type = 'button'; b.textContent = t;
            b.onclick = function () { send(t); };
            q.appendChild(b);
        });
        box.appendChild(q);
        box.scrollTop = box.scrollHeight;
    }

    function setTyping(on) {
        var t = $('aiChatTyping');
        if (t) t.classList.toggle('show', !!on);
        var btn = $('aiChatSendBtn');
        if (btn) btn.disabled = !!on;
    }

    function postJson(url, payload) {
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        }).then(function (res) { return res.json(); });
    }

    function send(text) {
        var input = $('aiChatText');
        var msg = (text != null ? text : (input ? input.value : '') || '').trim();
        if (!msg || sending) return;
        sending = true;
        if (input) input.value = '';
        addMsg(msg, 'user');
        history.push({ role: 'user', content: msg });
        setTyping(true);
        postJson('/AiChat/Send', { message: msg, history: history.slice(-12) })
            .then(function (data) {
                setTyping(false); sending = false;
                var reply = (data && data.reply) || 'Dạ trợ lý AI đang bận, Quý khách gọi Hotline 0933 777 888 để được hỗ trợ ngay ạ!';
                addMsg(reply, 'bot');
                history.push({ role: 'assistant', content: reply });
                refreshExtract();
            })
            .catch(function () {
                setTyping(false); sending = false;
                var fb = 'Dạ kết nối trợ lý AI đang gián đoạn, Quý khách vui lòng thử lại hoặc gọi Hotline 0933 777 888 ạ!';
                addMsg(fb, 'bot');
                history.push({ role: 'assistant', content: fb });
            });
    }

    function countSlots(e) {
        if (!e) return 0;
        var n = 0;
        if (e.ngan_sach_tran) n++;
        if (e.so_ban_du_kien) n++;
        if (e.so_khach_du_kien) n++;
        if (e.ngay_to_chuc) n++;
        if (e.ca_to_chuc) n++;
        return n;
    }

    function fmtVnd(n) { return Number(n || 0).toLocaleString('vi-VN') + ' đ'; }

    function refreshExtract() {
        postJson('/AiChat/Extract', { message: '', history: history.slice(-12) })
            .then(function (data) {
                var box = $('aiExtractBox');
                if (!box) return;
                var e = data && data.entities;
                if (data && data.success && e && countSlots(e) >= 2) {
                    lastEntities = e;
                    var parts = [];
                    if (e.ngan_sach_tran) parts.push('Ngân sách ' + fmtVnd(e.ngan_sach_tran));
                    if (e.so_ban_du_kien) parts.push(e.so_ban_du_kien + ' bàn');
                    if (e.so_khach_du_kien) parts.push(e.so_khach_du_kien + ' khách');
                    if (e.ngay_to_chuc) parts.push('ngày ' + e.ngay_to_chuc);
                    if (e.ca_to_chuc) parts.push(e.ca_to_chuc);
                    if (e.phong_cach) parts.push('gu ' + e.phong_cach);
                    var sum = $('aiExtractSummary');
                    if (sum) sum.textContent = parts.join(' • ');
                    box.classList.add('show');
                } else {
                    box.classList.remove('show');
                }
            })
            .catch(function () { /* im lặng: extract là tính năng cộng thêm */ });
    }

    function mapStyle(v) {
        if (!v) return null;
        var s = String(v);
        var opts = ['Hiện đại / lãng mạn', 'Hoàng gia', 'Sân vườn / lãng mạn', 'Cổ tích / lãng mạn', 'Nhẹ nhàng'];
        if (opts.indexOf(s) >= 0) return s;
        if (/thiên nhiên|ngoài trời|ven sông/i.test(s)) return 'Sân vườn / lãng mạn';
        return 'Hiện đại / lãng mạn';
    }

    function fillSurvey(e) {
        if (!e) return 0;
        var filled = 0;
        var budget = $('expectedBudget');
        if (budget && e.ngan_sach_tran && e.ngan_sach_tran >= 100000000 && e.ngan_sach_tran <= 3000000000) {
            budget.value = e.ngan_sach_tran;
            budget.dispatchEvent(new Event('input', { bubbles: true }));
            filled++;
        }
        var guests = e.so_khach_du_kien || (e.so_ban_du_kien ? e.so_ban_du_kien * 10 : 0);
        var gInput = $('guestCount');
        if (gInput && guests && guests >= 150 && guests <= 1400) {
            gInput.value = guests; filled++;
            try { if (typeof syncGuestToTables === 'function') syncGuestToTables(); } catch (x) { }
        } else if (e.so_ban_du_kien) {
            var off = $('officialTableCount');
            if (off) { off.value = e.so_ban_du_kien; }
            if (gInput) {
                gInput.value = Math.min(1400, e.so_ban_du_kien * 10);
                try { if (typeof syncGuestToTables === 'function') syncGuestToTables(); } catch (x) { }
            }
            filled++;
        }
        var date = $('eventDate');
        if (date && e.ngay_to_chuc && /^\d{4}-\d{2}-\d{2}$/.test(e.ngay_to_chuc)) {
            date.value = e.ngay_to_chuc; filled++;
            try { if (typeof handleDateChanged === 'function') handleDateChanged(); } catch (x) { }
        }
        var shift = $('shift');
        if (shift && e.ca_to_chuc && /ca (trưa|tối)/i.test(e.ca_to_chuc)) {
            shift.value = /trưa/i.test(e.ca_to_chuc) ? 'Ca trưa' : 'Ca tối'; filled++;
        }
        var style = $('desiredStyle');
        var mapped = mapStyle(e.phong_cach);
        if (style && mapped) { style.value = mapped; }
        return filled;
    }

    function contactReady() {
        var c = $('coupleName'), p = $('phoneNumber'), m = $('email');
        if (!c || !p || !m) return false;
        return String(c.value || '').trim().length >= 2
            && /^0\d{9}$/.test(String(p.value || '').trim())
            && /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(String(m.value || '').trim());
    }

    function submitSurvey() {
        try {
            if (typeof handleAnalyzeCombos === 'function') {
                handleAnalyzeCombos(new Event('submit'));
                return true;
            }
        } catch (x) { /* rơi xuống submit form thường */ }
        var f = $('clientRecomForm');
        if (f) { f.requestSubmit(); return true; }
        return false;
    }

    function applyAndGo() {
        if (!lastEntities) return;
        var params = new URLSearchParams({ prefill: JSON.stringify(lastEntities) }).toString();
        var form = $('clientRecomForm');
        closePanel();
        if (form) {
            fillSurvey(lastEntities);
            form.scrollIntoView({ behavior: 'smooth', block: 'start' });
            setTimeout(submitSurvey, 650);
        } else {
            window.location.href = '/Recommendation/Index?' + params;
        }
    }

    function applyPrefillFromUrl() {
        try {
            var sp = new URLSearchParams(window.location.search);
            var raw = sp.get('prefill');
            if (!raw || !$('clientRecomForm')) return;
            var e = JSON.parse(raw);
            fillSurvey(e);
            lastEntities = e;
            setTimeout(function () {
                var f = $('clientRecomForm');
                if (f) f.scrollIntoView({ behavior: 'smooth', block: 'start' });
                if (contactReady()) {
                    submitSurvey();
                } else if (typeof showRecomAlert === 'function') {
                    showRecomAlert('Em đã điền sẵn ngân sách, số khách, ngày và ca từ đoạn chat. Hai bạn bổ sung tên/SĐT/email rồi bấm “Tính Toán & Trích Xuất Top 3 Combo” nhé!',
                        'Đã áp dụng thông tin từ trợ lý ảo', 'success');
                }
            }, 600);
            try {
                var u = new URL(window.location.href);
                u.searchParams.delete('prefill');
                window.history.replaceState(null, '', u.toString());
            } catch (x) { }
        } catch (x) { /* prefill lỗi: bỏ qua */ }
    }

    function openPanel() {
        var p = $('aiChatPanel');
        if (!p) return;
        p.classList.add('open');
        p.setAttribute('aria-hidden', 'false');
        if ($('aiChatMsgs') && $('aiChatMsgs').children.length === 0) greeting();
    }

    function closePanel() {
        var p = $('aiChatPanel');
        if (!p) return;
        p.classList.remove('open');
        p.setAttribute('aria-hidden', 'true');
    }

    function togglePanel() {
        var p = $('aiChatPanel');
        if (!p) return;
        if (p.classList.contains('open')) closePanel(); else openPanel();
    }

    function init() {
        var fab = $('aiChatFab');
        if (!fab || !$('aiChatPanel')) return;
        fab.addEventListener('click', function () {
            var p = $('aiChatPanel');
            if (p && p.classList.contains('open')) closePanel(); else openPanel();
        });
        var close = document.querySelector('#aiChatPanel .ai-close');
        if (close) close.addEventListener('click', closePanel);
        var sendBtn = $('aiChatSendBtn');
        if (sendBtn) sendBtn.addEventListener('click', function () { send(); });
        var input = $('aiChatText');
        if (input) input.addEventListener('keydown', function (ev) {
            if (ev.key === 'Enter' && !ev.shiftKey) { ev.preventDefault(); send(); }
        });
        var apply = $('aiApplySurveyBtn');
        if (apply) apply.addEventListener('click', applyAndGo);
        document.addEventListener('keydown', function (ev) { if (ev.key === 'Escape') closePanel(); });
        applyPrefillFromUrl();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // API công khai cho test/nhúng + nút mở widget từ mọi trang (Home/Halls/Menus/Recommendation).
    window.AiChatWidget = { open: openPanel, close: closePanel, toggle: togglePanel, send: send, applyPrefillFromUrl: applyPrefillFromUrl };
})();
