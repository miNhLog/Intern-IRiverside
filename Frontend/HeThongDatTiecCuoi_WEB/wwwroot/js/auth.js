document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".toggle-password").forEach(button => {
        button.addEventListener("click", () => {
            const input = button.parentElement?.querySelector("input");
            if (!input) return;
            input.type = input.type === "password" ? "text" : "password";
            button.setAttribute("aria-label", input.type === "password" ? "Hiện mật khẩu" : "Ẩn mật khẩu");
            const icon = button.querySelector(".material-symbols-outlined");
            if (icon) icon.textContent = input.type === "password" ? "visibility" : "visibility_off";
        });
    });

    const password = document.querySelector("#Password");
    const meter = document.querySelector(".password-meter");
    const strengthText = document.querySelector("#passwordStrength");
    if (password && meter && strengthText) {
        password.addEventListener("input", () => {
            const value = password.value;
            const checks = [value.length >= 8, /[a-z]/.test(value) && /[A-Z]/.test(value), /\d/.test(value), /[^A-Za-z0-9]/.test(value)];
            const score = checks.filter(Boolean).length;
            meter.dataset.score = String(score);
            strengthText.textContent = score === 4 ? "Độ mạnh: Tốt" : `Đã đạt ${score}/4 yêu cầu bảo mật`;
        });
    }

    const passwordForm = document.querySelector("[data-password-form]");
    if (passwordForm) {
        const otpInput = passwordForm.querySelector("#OtpCode");
        const otpButton = passwordForm.querySelector("#sendFirstPasswordOtp");
        const otpButtonText = otpButton?.querySelector("[data-otp-button-text]");
        const otpFeedback = passwordForm.querySelector("#otpFeedback");
        let otpTimer;
        otpInput?.addEventListener("input", () => { otpInput.value = otpInput.value.replace(/\D/g, "").slice(0, 6); });
        otpButton?.addEventListener("click", async () => {
            clearInterval(otpTimer);
            otpButton.disabled = true;
            if (otpButtonText) otpButtonText.textContent = "Đang gửi...";
            if (otpFeedback) { otpFeedback.textContent = ""; otpFeedback.classList.remove("is-error"); }
            try {
                const csrfToken = passwordForm.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
                const response = await fetch("/doi-mat-khau/gui-otp", { method: "POST", headers: { "RequestVerificationToken": csrfToken } });
                const result = await response.json();
                if (!result.success) throw new Error(result.message || "Không thể gửi mã OTP.");
                if (otpFeedback) otpFeedback.textContent = result.message;
                let remaining = Number(result.cooldownSeconds) || 60;
                if (otpButtonText) otpButtonText.textContent = `Gửi lại sau ${remaining}s`;
                otpTimer = setInterval(() => {
                    remaining -= 1;
                    if (remaining <= 0) {
                        clearInterval(otpTimer);
                        otpButton.disabled = false;
                        if (otpButtonText) otpButtonText.textContent = "Gửi lại mã";
                    } else if (otpButtonText) otpButtonText.textContent = `Gửi lại sau ${remaining}s`;
                }, 1000);
            } catch (error) {
                otpButton.disabled = false;
                if (otpButtonText) otpButtonText.textContent = "Gửi mã OTP";
                if (otpFeedback) { otpFeedback.textContent = error.message || "Không thể gửi mã OTP."; otpFeedback.classList.add("is-error"); }
            }
        });
        const newPassword = passwordForm.querySelector("#NewPassword");
        const confirmPassword = passwordForm.querySelector("#ConfirmPassword");
        const confirmError = passwordForm.querySelector(".client-confirm-error");
        const submitButton = passwordForm.querySelector(".first-password-submit");
        const requirements = { length: value => value.length >= 8, uppercase: value => /[A-Z]/.test(value), lowercase: value => /[a-z]/.test(value), number: value => /\d/.test(value), special: value => /[^A-Za-z0-9]/.test(value) };
        const updatePasswordState = () => {
            const value = newPassword?.value ?? "";
            Object.entries(requirements).forEach(([rule, passes]) => passwordForm.querySelector(`[data-rule="${rule}"]`)?.classList.toggle("met", passes(value)));
            if (confirmError && confirmPassword?.value) confirmError.textContent = confirmPassword.value === value ? "" : "Mật khẩu xác nhận không khớp.";
        };
        newPassword?.addEventListener("input", updatePasswordState);
        confirmPassword?.addEventListener("input", updatePasswordState);
        passwordForm.addEventListener("submit", event => {
            if (confirmPassword && newPassword && confirmPassword.value !== newPassword.value) { event.preventDefault(); if (confirmError) confirmError.textContent = "Mật khẩu xác nhận không khớp."; confirmPassword.focus(); return; }
            if (submitButton) { submitButton.disabled = true; submitButton.textContent = "Đang cập nhật..."; }
        });
    }
});
