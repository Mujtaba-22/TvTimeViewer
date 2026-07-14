document.addEventListener('DOMContentLoaded', function () {
    const formatFilter = document.getElementById('formatFilter');
    const genreFilter = document.getElementById('genreFilter');
    const mangaGrid = document.getElementById('mangaGrid');
    const loadMoreBtn = document.getElementById('loadMoreBtn');

    let currentPage = 1;
    let hasNextPage = true;

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str || '';
        return div.innerHTML;
    }

    function cardHtml(item) {
        const cover = item.cover || '/images/placeholder.png';
        const rating = item.rating ? (item.rating / 10).toFixed(1) : 'N/A';

        return `
            <div class="col-6 col-md-3 col-lg-2">
                <div class="card h-100 shadow-sm">
                    <img src="${cover}" class="card-img-top" alt="${escapeHtml(item.title)}" />
                    <div class="card-body p-2">
                        <p class="card-text small fw-semibold mb-1" title="${escapeHtml(item.title)}">${escapeHtml(item.title)}</p>
                        <span class="badge bg-light text-dark border mb-1">★ ${rating} ${item.year ? '· ' + item.year : ''}</span>
                        <button type="button"
                                class="btn btn-sm btn-outline-primary w-100 manga-add-btn"
                                data-anilist-id="${item.aniListId}"
                                data-format="${item.format}">
                            + Add to List
                        </button>
                    </div>
                </div>
            </div>`;
    }

    async function loadGenres() {
        try {
            const res = await fetch('/Manga/Genres');
            const genres = await res.json();

            genreFilter.innerHTML =
                '<option value="">All Genres</option>' +
                genres.map(g => `<option value="${g}">${g}</option>`).join('');
        } catch (err) {
        }
    }

    async function loadManga(reset) {
        if (reset) {
            currentPage = 1;
            mangaGrid.innerHTML = '<p class="text-muted">Loading...</p>';
        }

        try {
            const res = await fetch(`/Manga/Discover?format=${formatFilter.value}&genre=${encodeURIComponent(genreFilter.value)}&page=${currentPage}`);
            const data = await res.json();

            if (data.error) {
                mangaGrid.innerHTML = `<div class="alert alert-warning">${data.error}</div>`;
                loadMoreBtn.style.display = 'none';
                return;
            }

            hasNextPage = data.hasNextPage;
            const html = data.items.map(cardHtml).join('');
            mangaGrid.innerHTML = reset ? html : mangaGrid.innerHTML + html;
            loadMoreBtn.style.display = hasNextPage ? '' : 'none';
        } catch (err) {
            mangaGrid.innerHTML = `<div class="alert alert-warning">Could not load results.</div>`;
        }
    }

    formatFilter.addEventListener('change', function () {
        loadManga(true);
    });

    genreFilter.addEventListener('change', function () {
        loadManga(true);
    });

    loadMoreBtn.addEventListener('click', function () {
        currentPage++;
        loadManga(false);
    });

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.manga-add-btn');
        if (!btn) return;

        const aniListId = btn.dataset.anilistId;
        const format = btn.dataset.format;

        btn.disabled = true;
        btn.textContent = 'Adding...';

        try {
            const res = await fetch(`/Manga/Add?aniListId=${aniListId}&format=${format}`, { method: 'POST' });
            const data = await res.json();

            if (data.success) {
                btn.textContent = '✓ Added';
                btn.classList.remove('btn-outline-primary');
                btn.classList.add('btn-success');
            } else {
                btn.disabled = false;
                btn.textContent = '+ Add to List';
                alert(data.message);
            }
        } catch (err) {
            btn.disabled = false;
            btn.textContent = '+ Add to List';
            alert('Could not add this title.');
        }
    });

    loadGenres();
    loadManga(true);
});