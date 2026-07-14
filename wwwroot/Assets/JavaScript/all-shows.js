document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('allShowsFilterForm');
    if (!form) return;

    document.querySelectorAll('.shows-auto-submit').forEach(element => {
        element.addEventListener('change', function () {
            form.submit();
        });
    });
});