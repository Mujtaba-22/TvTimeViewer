document.addEventListener('DOMContentLoaded', function () {
    const allGamesSearch = document.getElementById('allGamesSearch');
    if (!allGamesSearch) return;

    allGamesSearch.addEventListener('input', function () {
        const term = allGamesSearch.value.trim().toLowerCase();

        document.querySelectorAll('.game-card').forEach(card => {
            const title = card.dataset.title || '';
            const matches = !term || title.includes(term);

            card.classList.toggle('hidden-by-search', !matches);
        });
    });
});