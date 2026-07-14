document.addEventListener('DOMContentLoaded', function () {
    const searchInput = document.getElementById('liveSearchInput');
    const resultsContainer = document.getElementById('searchResultsContainer');
    const spinner = document.getElementById('liveSearchSpinner');
    const typeFilter = document.getElementById('typeFilter');
    const genreFilter = document.getElementById('genreFilter');
    const discoverGrid = document.getElementById('discoverGrid');
    const loadMoreBtn = document.getElementById('loadMoreBtn');
    const browseHeading = document.getElementById('browseHeading');
    const filterBar = document.getElementById('filterBar');

    let currentController = null;
    let currentPage = 1;
    let totalPages = 1;

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

    function renderSearchResults(items, query) {
        if (!items || items.length === 0) {
            resultsContainer.innerHTML = `
                <div class="text-center text-muted mt-5">
                    <h5>We're sorry!</h5>
                    <p>We couldn't find any matches for "${escapeHtml(query)}"</p>
                </div>`;
            return;
        }

        const cards = items.map(item => {
            const poster = item.poster && item.poster !== 'N/A'
                ? item.poster
                : '/images/placeholder.png';

            return `
                <div class="col-6 col-md-3 col-lg-2">
                    <a href="/Search/Details/${item.imdbID}" class="text-decoration-none text-dark">
                        <div class="card h-100 shadow-sm search-result-card">
                            <img src="${poster}" class="card-img-top" alt="${escapeHtml(item.title)}" />
                            <div class="card-body p-2">
                                <p class="card-text small fw-semibold mb-1">${escapeHtml(item.title)}</p>
                                <span class="badge bg-light text-dark border">${item.year}</span>
                                <span class="badge bg-info text-dark">${item.type}</span>
                            </div>
                        </div>
                    </a>
                </div>`;
        }).join('');

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
            const res = await fetch(`/Search/SearchJson?q=${encodeURIComponent(query.trim())}`, {
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

    document.getElementById('liveSearchForm').addEventListener('submit', function (e) {
        e.preventDefault();
        liveSearch(searchInput.value);
    });

    function discoverCardHtml(item) {
        const poster = item.poster || '/images/placeholder.png';
        const rating = item.rating ? item.rating.toFixed(1) : 'N/A';
        const year = item.releaseDate ? item.releaseDate.substring(0, 4) : '';

        return `
            <div class="col-6 col-md-3 col-lg-2">
                <div class="card h-100 shadow-sm discover-card">
                    <img src="${poster}" class="card-img-top" alt="${escapeHtml(item.title)}" />
                    <div class="card-body p-2">
                        <p class="card-text small fw-semibold mb-1" title="${escapeHtml(item.title)}">${escapeHtml(item.title)}</p>
                        <span class="badge bg-light text-dark border mb-2">★ ${rating} ${year ? '· ' + year : ''}</span>
                        <button type="button"
                                class="btn btn-sm btn-outline-primary w-100 trending-add-btn"
                                data-tmdb-id="${item.tmdbId}"
                                data-is-movie="${item.isMovie}">
                            + Add to List
                        </button>
                    </div>
                </div>
            </div>`;
    }

    async function loadDiscover(reset) {
        if (reset) {
            currentPage = 1;
            discoverGrid.innerHTML = '<p class="text-muted">Loading...</p>';
        }

        const type = typeFilter.value;
        const genreId = genreFilter.value;

        try {
            const res = await fetch(`/Trending/Discover?type=${type}&genreId=${genreId}&page=${currentPage}`);
            const data = await res.json();

            if (data.error) {
                discoverGrid.innerHTML = `<div class="alert alert-warning">${data.error}</div>`;
                loadMoreBtn.style.display = 'none';
                return;
            }

            totalPages = data.totalPages;
            const html = data.items.map(discoverCardHtml).join('');
            discoverGrid.innerHTML = reset ? html : discoverGrid.innerHTML + html;
            loadMoreBtn.style.display = currentPage < totalPages ? '' : 'none';
        } catch (err) {
            discoverGrid.innerHTML = `<div class="alert alert-warning">Could not load results.</div>`;
        }
    }

    async function loadGenres() {
        const type = typeFilter.value;

        try {
            const res = await fetch(`/Trending/Genres?type=${type}`);
            const genres = await res.json();

            genreFilter.innerHTML = '<option value="0">All Genres</option>' +
                genres.map(g => `<option value="${g.id}">${escapeHtml(g.name)}</option>`).join('');
        } catch (err) {
            genreFilter.innerHTML = '<option value="0">All Genres</option>';
        }
    }

    typeFilter.addEventListener('change', async function () {
        browseHeading.querySelector('h4').innerHTML =
            (typeFilter.value === 'movie' ? 'Browse Movies' : 'Browse Shows') +
            ' <span class="badge bg-secondary">Live from TMDb</span>';

        await loadGenres();
        await loadDiscover(true);
    });

    genreFilter.addEventListener('change', function () {
        loadDiscover(true);
    });

    loadMoreBtn.addEventListener('click', function () {
        currentPage++;
        loadDiscover(false);
    });

    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.trending-add-btn');
        if (!btn) return;

        const tmdbId = btn.dataset.tmdbId;
        const isMovie = btn.dataset.isMovie === 'true';
        const endpoint = isMovie ? '/Trending/AddMovie' : '/Trending/AddShow';

        btn.disabled = true;
        btn.textContent = 'Adding...';

        try {
            const res = await fetch(`${endpoint}?tmdbId=${tmdbId}`, { method: 'POST' });
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

    async function loadTopRated() {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 10000);

        try {
            const res = await fetch('/Trending/TopRated', { signal: controller.signal });
            clearTimeout(timeoutId);

            const data = await res.json();

            if (data.error) {
                document.getElementById('topRatedShowsRow').innerHTML = `<div class="alert alert-warning mb-0">Top Rated unavailable: ${data.error}</div>`;
                document.getElementById('topRatedMoviesRow').innerHTML = '';
                return;
            }

            renderTopRatedRow('topRatedShowsRow', data.shows);
            renderTopRatedRow('topRatedMoviesRow', data.movies);
        } catch (err) {
            clearTimeout(timeoutId);
            const message = err.name === 'AbortError' ? 'Request timed out.' : 'Could not reach TMDb.';
            document.getElementById('topRatedShowsRow').innerHTML = `<div class="alert alert-warning mb-0">${message}</div>`;
            document.getElementById('topRatedMoviesRow').innerHTML = '';
        }
    }

    function renderTopRatedRow(elementId, items) {
        const container = document.getElementById(elementId);

        if (!items || !items.length) {
            container.innerHTML = `<div class="alert alert-warning mb-0">No results returned.</div>`;
            return;
        }

        container.innerHTML = items.map(discoverCardHtml).join('');
    }

    (async function init() {
        await loadGenres();
        await loadDiscover(true);
        loadTopRated();
    })();
});