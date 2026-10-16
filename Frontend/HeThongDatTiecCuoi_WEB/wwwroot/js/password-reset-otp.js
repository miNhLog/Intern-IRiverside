(() => {
    const loginForm = document.getElementById('loginForm');
    const flow = document.getElementById('passwordResetFlow');
    const openButton = document.getElementById('openForgotPassword');
    const backButton = document.getElementById('backToLogin');
    const sendForm = document.getElementById('sendResetOtpForm');
    const verifyForm = document.getElementById('verifyResetOtpForm');
    const completeForm = document.getElementById('completePasswordResetForm');
    const resendButton = document.getElementById('resendResetOtp');
    let cooldownTimer;

    if (!loginForm || !flow) return;

    const showFeedback = (message, success = false) => {
        if (!message) return;
        let region = document.querySelector('.auth-toast-region');
        if (!region) {
            region = document.createElement('div');
            region.className = 'auth-toast-region';
            region.setAttribute('aria-live', 'polite');
            region.setAttribute('aria-atomic', 'true');
            document.body.appendChild(region);
        }

        const toast = document.createElement('div');
        toast.className = `auth-toast ${success ? 'auth-toast-success' : 'auth-toast-error'}`;
        toast.setAttribute('role', success ? 'status' : 'alert');
        const icon = document.createElement('span');
        icon.className = 'material-symbols-outlined';
        icon.setAttribute('aria-hidden', 'true');
        icon.textContent = success ? 'check_circle' : 'error';
        const content = document.createElement('span');
        content.textContent = message;
        toast.append(icon, content);
        region.replaceChildren(toast);

        window.setTimeout(() => {
            toast.classList.add('is-leaving');
            window.setTimeout(() => toast.remove(), 220);
        }, 4500);
    };

    const showStep = step => {
        [sendForm, verifyForm, completeForm].forEach((form, index) => form.hidden = index + 1 !== step);
        document.querySelectorAll('[data-step-indicator]').forEach(indicator => {
            const value = Number(indicator.dataset.stepIndicator);
            indicator.classList.toggle('is-active', value === step);
            indicator.classList.toggle('is-complete', value < step);
        });
    };

    const postForm = async (url, form) => {
        const response = await fetch(url, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        let data;
        try { data = await response.json(); }
        catch { data = { message: 'Máy chủ trả về dữ liệu không hợp lệ.' }; }
        if (!response.ok || data.success === false) throw new Error(data.message || 'Không thể xử lý yêu cầu.');
        return data;
    };

    const startCooldown = seconds => {
        clearInterval(cooldownTimer);
        let remaining = Math.max(1, seconds || 60);
        resendButton.disabled = true;
        resendButton.textContent = `Gửi lại mã sau ${remaining} giây`;
        cooldownTimer = setInterval(() => {
            remaining -= 1;
            if (remaining <= 0) {
                clearInterval(cooldownTimer);
                resendButton.disabled = false;
                resendButton.textContent = 'Gửi lại mã OTP';
            } else {
                resendButton.textContent = `Gửi lại mã sau ${remaining} giây`;
            }
        }, 1000);
    };

    openButton?.addEventListener('click', () => {
        loginForm.hidden = true;
        flow.hidden = false;
        showStep(1);
        document.getElementById('resetEmail')?.focus();
    });

    backButton?.addEventListener('click', () => {
        clearInterval(cooldownTimer);
        flow.hidden = true;
        loginForm.hidden = false;
        sendForm.reset();
        verifyForm.reset();
        completeForm.reset();
        document.querySelector('.auth-toast-region')?.replaceChildren();
    });

    sendForm.addEventListener('submit', async event => {
        event.preventDefault();
        const button = document.getElementById('sendResetOtpButton');
        button.disabled = true;
        try {
            const data = await postForm('/login/password-reset/send-otp', sendForm);
            const email = document.getElementById('resetEmail').value.trim();
            document.getElementById('verifyResetEmail').value = email;
            document.getElementById('resetEmailDisplay').textContent = email;
            showStep(2);
            showFeedback(data.message, true);
            startCooldown(data.cooldownSeconds);
            document.getElementById('resetOtpCode').focus();
        } catch (error) { showFeedback(error.message); }
        finally { button.disabled = false; }
    });

    resendButton.addEventListener('click', async () => {
        resendButton.disabled = true;
        try {
            document.getElementById('resetEmail').value = document.getElementById('verifyResetEmail').value;
            const data = await postForm('/login/password-reset/send-otp', sendForm);
            showFeedback(data.message, true);
            startCooldown(data.cooldownSeconds);
        } catch (error) { showFeedback(error.message); resendButton.disabled = false; }
    });

    document.getElementById('resetOtpCode')?.addEventListener('input', event => {
        event.target.value = event.target.value.replace(/\D/g, '').slice(0, 6);
    });

    verifyForm.addEventListener('submit', async event => {
        event.preventDefault();
        try {
            const data = await postForm('/login/password-reset/verify-otp', verifyForm);
            document.getElementById('resetGrant').value = data.resetToken;
            showStep(3);
            showFeedback(data.message, true);
            document.getElementById('resetNewPassword').focus();
        } catch (error) { showFeedback(error.message); }
    });

    completeForm.addEventListener('submit', async event => {
        event.preventDefault();
        const password = document.getElementById('resetNewPassword').value;
        const confirmation = document.getElementById('resetConfirmPassword').value;
        if (password !== confirmation) {
            showFeedback('Mật khẩu xác nhận không khớp.');
            return;
        }
        try {
            const data = await postForm('/login/password-reset/complete', completeForm);
            clearInterval(cooldownTimer);
            flow.hidden = true;
            loginForm.hidden = false;
            loginForm.reset();
            showFeedback(data.message, true);
            document.querySelector('#loginForm input[name="Identifier"]')?.focus();
        } catch (error) { showFeedback(error.message); }
    });
})();
