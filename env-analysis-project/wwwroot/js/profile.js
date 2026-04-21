'use strict';

(function () {
    const getAntiForgeryToken = () =>
        document.querySelector('#profileAntiForgeryForm input[name="__RequestVerificationToken"]')?.value || '';

    const changeButton = document.getElementById('changePasswordBtn');
    const newPasswordInput = document.getElementById('newPassword');
    const confirmPasswordInput = document.getElementById('confirmPassword');

    if (!changeButton || !newPasswordInput || !confirmPasswordInput) return;

    const setButtonBusy = (busy) => {
        changeButton.disabled = busy;
        changeButton.classList.toggle('opacity-60', busy);
        changeButton.classList.toggle('pointer-events-none', busy);
    };

    const changePassword = async () => {
        const newPassword = (newPasswordInput.value || '').trim();
        const confirmPassword = (confirmPasswordInput.value || '').trim();

        if (!newPassword || !confirmPassword) {
            alert('Please enter both password fields.');
            return;
        }

        if (newPassword !== confirmPassword) {
            alert('Confirm password does not match the new password.');
            return;
        }

        setButtonBusy(true);
        try {
            const token = getAntiForgeryToken();
            const res = await fetch('/Profile/ChangePassword', {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest',
                    ...(token ? { RequestVerificationToken: token } : {})
                },
                body: JSON.stringify({
                    newPassword,
                    confirmPassword
                })
            });

            const json = await res.json();
            if (!res.ok || json?.success === false) {
                const message = json?.message || 'Failed to update password.';
                const detail = Array.isArray(json?.errors) && json.errors.length > 0
                    ? `\n- ${json.errors.join('\n- ')}`
                    : '';
                throw new Error(`${message}${detail}`);
            }

            alert(json?.message || 'Password updated successfully.');
            newPasswordInput.value = '';
            confirmPasswordInput.value = '';
        } catch (error) {
            console.error(error);
            alert(error.message || 'Error while updating password.');
        } finally {
            setButtonBusy(false);
        }
    };

    changeButton.addEventListener('click', changePassword);
})();
