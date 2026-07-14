document.addEventListener('DOMContentLoaded', function () {
    const searchInput = document.getElementById('liveSearchInput');
    const resultsContainer = document.getElementById('searchResultsContainer');
    const spinner = document.getElementById('liveSearchSpinner');
    const genreFilter = document.getElementById('genreFilter');
    const discoverGrid = document.getElementById('discoverGrid');
    const loadMoreBtn = document.getElementById('loadMoreBtn');
    const browseHeading = document.getElementById('browseHeading');
    const topRatedGamesRow = document.getElementById('topRatedGamesRow');
    const liveSearchForm = document.getElementById('liveSearchForm');

    let currentController = null;
    let currentPage = 1;

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str || '';
        return div.innerHTML;
    }

    function toggleBrowseVisibility(show) {
        discoverGrid.style.display = show ? '' : 'none';
        loadMoreBtn.style.display = show ? '' : 'none';
        browseHeading.style.display = show ? '' : 'none';
    }

    function addButtonHtml(igdbId) {
        return `<button type="button" class="btn btn-sm btn-outline-primary w-100 game-add-btn" data-igdb-id="${igdbId}">+ Add to Library</button>`;
    }

    function renderSearchResults(items, query) {
        if (!items || items.length === 0) {
            resultsContainer.innerHTML = `
                <div class="text-center text-muted mt-5">
                    <h5>We're sorry!</h5>
                    <p>We couldn't find any matches for "${escapeHtml(query)}"</p>
                </div>`;
            return;
        }

        const cards = items.map(item => `
            <div class="col-6 col-md-3 col-lg-2">
                <div class="card h-100 shadow-sm">
                    <img src="${item.poster || '/images/placeholder.png'}" class="card-img-top" alt="${escapeHtml(item.title)}" />
                    <div class="card-body p-2">
                        <p class="card-text small fw-semibold mb-1">${escapeHtml(item.title)}</p>
                        <span class="badge bg-light text-dark border">${item.year || ''}</span>
                        ${item.genre ? `<span class="badge bg-info text-dark mb-2">${escapeHtml(item.genre)}</span>` : ''}
                        ${addButtonHtml(item.igdbId)}
                    </div>
                </div>
            </div>
        `).join('');

        resultsContainer.innerHTML = `<div class="row g-3">${cards}</div>`;
    }

    async function liveSearch(query) {
        if (!query || query.trim().length < 2) {
            resultsContainer.innerHTML = '';
            toggleBrowseVisibility(true);
            return;
        }

        toggleBrowseVisibility(false);

        if (currentController) currentController.abort();
        currentController = new AbortController();

        spinner.style.display = 'inline-block';

        try {
            const res = await fetch(`/Games/SearchJson?q=${encodeURIComponent(query.trim())}`, {
                signal: currentController.signal
            });
            const data = await res.json();
            renderSearchResults(data, query.trim());
        } catch (err) {
            if (err.name !== 'AbortError') {
                resultsContainer.innerHTML = `<div class="alert alert-warning">Search failed. Try again.</div>`;
            }
        } finally {
            spinner.style.display = 'none';
        }
    }

    function discoverCardHtml(item) {
        const poster = item.cover || item.poster || '/images/placeholder.png';
        const rating = item.rating ? item.rating.toFixed(0) : 'N/A';

        return `
            <div class="col-6 col-md-3 col-lg-2">
                <div class="card h-100 shadow-sm">
                    <img src="${poster}" class="card-img-top" alt="${escapeHtml(item.title)}" />
                    <div class="card-body p-2">
                        <p class="card-text small fw-semibold mb-1" title="${escapeHtml(item.title)}">${escapeHtml(item.title)}</p>
                        <span class="badge bg-light text-dark border mb-2">★ ${rating} ${item.year ? '· ' + item.year : ''}</span>
                        ${addButtonHtml(item.igdbId)}
                    </div>
                </div>
            </div>`;
    }

    async function loadDiscover(reset) {
        if (reset) {
            currentPage = 1;
            discoverGrid.innerHTML = '<p class="text-muted">Loading...</p>';
        }

        const genreId = genreFilter.value;

        try {
            const res = await fetch(`/Games/Discover?genreId=${genreId}&page=${currentPage}`);
            const data = await res.json();

            if (data.error) {
                discoverGrid.innerHTML = `<div class="alert alert-warning">${data.error}</div>`;
                loadMoreBtn.style.display = 'none';
                return;
            }

            const html = data.items.map(discoverCardHtml).join('');
            discoverGrid.innerHTML = reset ? html : discoverGrid.innerHTML + html;
            loadMoreBtn.style.display = data.hasNextPage ? '' : 'none';
        } catch (err) {
            discoverGrid.innerHTML = `<div class="alert alert-warning">Could not load results.</div>`;
        }
    }

    async function loadGenres() {
        try {
            const res = await fetch('/Games/Genres');
            const genres = await res.json();

            genreFilter.innerHTML =
                '<option value="0">All Genres</option>' +
                genres.map(g => `<option value="${g.id}">${escapeHtml(g.name)}</option>`).join('');
        } catch (err) {
            genreFilter.innerHTML = '<option value="0">All Genres</option>';
        }
    }

    async function loadTopRated() {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 10000);

        try {
            const res = await fetch('/Games/TopRated', { signal: controller.signal });
            clearTimeout(timeoutId);

            const data = await res.json();

            if (data.error) {
                topRatedGamesRow.innerHTML = `<div class="alert alert-warning mb-0">Top Rated unavailable: ${data.error}</div>`;
                return;
            }

            topRatedGamesRow.innerHTML = data.games && data.games.length
                ? data.games.map(discoverCardHtml).join('')
                : `<div class="alert alert-warning mb-0">No results returned.</div>`;
        } catch (err) {
            clearTimeout(timeoutId);
            const message = err.name === 'AbortError' ? 'Request timed out.' : 'Could not reach IGDB.';
            topRatedGamesRow.innerHTML = `<div class="alert alert-warning mb-0">${message}</div>`;
        }
    }

    liveSearchForm.addEventListener('submit', function (e) {
        e.preventDefault();
        liveSearch(searchInput.value);
    });

    genreFilter.addEventListener('change', function () {
        loadDiscover(true);
    });

    loadMoreBtn.addEventListener('click', function () {
        currentPage++;
        loadDiscover(false);
    });

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.game-add-btn');
        if (!btn) return;

        const igdbId = btn.dataset.igdbId;
        btn.disabled = true;
        btn.textContent = 'Adding...';

        try {
            const res = await fetch(`/Games/Add?igdbId=${igdbId}`, { method: 'POST' });
            const data = await res.json();

            if (data.success) {
                btn.textContent = '✓ Added';
                btn.classList.remove('btn-outline-primary');
                btn.classList.add('btn-success');
            } else {
                btn.disabled = false;
                btn.textContent = '+ Add to Library';
                alert(data.message);
            }
        } catch (err) {
            btn.disabled = false;
            btn.textContent = '+ Add to Library';
            alert('Could not add this title.');
        }
    });

    (async function init() {
        await loadGenres();
        await loadDiscover(true);
        loadTopRated();
    })();
});