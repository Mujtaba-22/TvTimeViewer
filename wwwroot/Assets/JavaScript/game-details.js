document.addEventListener('DOMContentLoaded', function () {
    const hoursForm = document.getElementById('hoursForm');
    if (!hoursForm) return;

    hoursForm.addEventListener('submit', function () {
        const submitButton = hoursForm.querySelector('button[type="submit"]');
        if (!submitButton) return;

        submitButton.disabled = true;
        submitButton.textContent = 'Saving...';
    });
});