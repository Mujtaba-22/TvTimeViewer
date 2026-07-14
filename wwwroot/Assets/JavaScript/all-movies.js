document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('allMoviesFilterForm');
    if (!form) return;

    document.querySelectorAll('.movies-auto-submit').forEach(element => {
        element.addEventListener('change', function () {
            form.submit();
        });
    });
});