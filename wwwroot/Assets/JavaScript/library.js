document.addEventListener('DOMContentLoaded', function () {
    async function checkShowUpdatesAuto() {
        const bar = document.getElementById('updatesNotificationBar');
        const dismissedKey = 'updatesDismissedAt';
        const lastDismissed = sessionStorage.getItem(dismissedKey);

        try {
            const res = await fetch('/Updates/Check?force=false');
            const data = await res.json();

            if (!data.updates || !data.updates.length) return;
            if (lastDismissed) return;

            const chips = data.updates.slice(0, 8).map(u => {
                const label = u.isNewSeason
                    ? `${u.title} · New Season ${u.latestSeason}`
                    : `${u.title} · ${u.newEpisodeCount} new ep${u.newEpisodeCount > 1 ? 's' : ''}`;
                return `<a href="/Show/Details/${u.showId}" class="updates-banner-chip">${label}</a>`;
            }).join('');

            bar.innerHTML = `
                <div class="updates-banner">
                    <div>
                        <p class="updates-banner-title">📺 ${data.updates.length} show${data.updates.length > 1 ? 's have' : ' has'} new episodes or seasons</p>
                        <div class="updates-banner-list">${chips}</div>
                    </div>
                    <button type="button" class="updates-banner-dismiss" id="dismissUpdatesBanner" title="Dismiss">✕</button>
                </div>
            `;

            document.getElementById('dismissUpdatesBanner').addEventListener('click', function () {
                sessionStorage.setItem(dismissedKey, '1');
                bar.innerHTML = '';
            });
        } catch (err) {
        }
    }

    function initLibrarySearch() {
        const searchInput = document.getElementById('librarySearch');
        const resultCountEl = document.getElementById('searchResultCount');
        const allSections = document.querySelectorAll('.library-section');

        function applyLibrarySearch() {
            if (!searchInput) return;

            const term = searchInput.value.trim().toLowerCase();
            let totalMatches = 0;
            const isSearching = term.length > 0;

            allSections.forEach(section => {
                const cards = section.querySelectorAll('.library-card');
                let sectionMatches = 0;

                cards.forEach(card => {
                    const title = card.dataset.title || '';
                    const matches = !isSearching || title.includes(term);

                    card.style.display = matches ? '' : 'none';

                    if (matches) sectionMatches++;
                });

                totalMatches += sectionMatches;

                const heading = section.previousElementSibling;
                if (isSearching && sectionMatches === 0) {
                    section.classList.add('section-no-match');
                    if (heading && heading.tagName !== 'DIV') heading.classList.add('section-no-match');
                } else {
                    section.classList.remove('section-no-match');
                    if (heading) heading.classList.remove('section-no-match');
                }
            });

            if (resultCountEl) {
                resultCountEl.textContent = isSearching
                    ? `${totalMatches} match${totalMatches === 1 ? '' : 'es'}`
                    : '';
            }
        }

        let searchDebounce;
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                clearTimeout(searchDebounce);
                searchDebounce = setTimeout(applyLibrarySearch, 120);
            });
        }
    }

    function showProgressModal() {
        Swal.fire({
            title: 'Starting...',
            html: `<div class="progress" style="height:24px;">
                     <div class="progress-bar bg-success" style="width:0%">0%</div>
                   </div>`,
            allowOutsideClick: false,
            allowEscapeKey: false,
            showConfirmButton: false
        });
    }

    function pollProgressPromise(jobId) {
        return new Promise((resolve, reject) => {
            const interval = setInterval(async () => {
                const res = await fetch('/Library/Progress?id=' + jobId);
                if (!res.ok) {
                    clearInterval(interval);
                    reject(new Error('Lost progress tracking.'));
                    return;
                }

                const state = await res.json();

                Swal.update({
                    title: state.label,
                    html: `<div class="progress" style="height:24px;">
                             <div class="progress-bar bg-success" style="width:${state.percent}%">${state.percent}%</div>
                           </div>`
                });

                if (state.completed) {
                    clearInterval(interval);
                    resolve(state.resultMessage);
                }
            }, 700);
        });
    }

    async function startAndAwait(url) {
        const res = await fetch(url, { method: 'POST' });
        const data = await res.json();
        if (data.error) throw new Error(data.error);
        return pollProgressPromise(data.jobId);
    }

    function initImportForm() {
        const importForm = document.getElementById('importForm');
        if (!importForm) return;

        importForm.addEventListener('submit', async function (e) {
            e.preventDefault();

            const fileInput = document.getElementById('zipFile');
            if (!fileInput.files.length) return;

            const formData = new FormData();
            formData.append('zipFile', fileInput.files[0]);

            showProgressModal();

            try {
                const importRes = await fetch('/Library/StartImportZip', {
                    method: 'POST',
                    body: formData
                });

                const importData = await importRes.json();
                if (importData.error) throw new Error(importData.error);

                await pollProgressPromise(importData.jobId);
                await startAndAwait('/Library/StartCleanDuplicates');
                await startAndAwait('/Library/StartRefreshPosters');
                await startAndAwait('/Library/StartRefreshGenres');

                Swal.fire({
                    icon: 'success',
                    title: 'All done!',
                    text: 'Your library was imported and fully enriched with posters and genres.',
                    confirmButtonText: 'OK'
                }).then(() => location.reload());
            } catch (err) {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: err.message || 'Something went wrong during import.'
                });
            }
        });
    }

    checkShowUpdatesAuto();
    initLibrarySearch();
    initImportForm();
});