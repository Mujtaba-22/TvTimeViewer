document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('allMangaFilterForm');
    if (!form) return;

    document.querySelectorAll('.manga-auto-submit').forEach(element => {
        element.addEventListener('change', function () {
            form.submit();
        });
    });
});