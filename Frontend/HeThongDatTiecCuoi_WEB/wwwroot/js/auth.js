document.addEventListener("DOMContentLoaded", () => {
    const accountTypeInput = document.querySelector("#accountType");
    document.querySelectorAll(".tab-button").forEach(button => {
        button.addEventListener("click", () => {
            document.querySelectorAll(".tab-button").forEach(item => item.classList.remove("active"));
            button.classList.add("active");
            if (accountTypeInput) accountTypeInput.value = button.dataset.accountType;
        });
    });

    document.querySelectorAll(".toggle-password").forEach(button => {
        button.addEventListener("click", () => {
            const input = button.parentElement?.querySelector("input");
            if (!input) return;
            input.type = input.type === "password" ? "text" : "password";
            button.setAttribute("aria-label", input.type === "password" ? "Hiện mật khẩu" : "Ẩn mật khẩu");
        });
    });

    const password = document.querySelector("#MatKhau");
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
});
