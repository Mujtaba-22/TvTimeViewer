document.addEventListener('DOMContentLoaded', function () {
    const config = window.mangaDetailsConfig || {};
    const mangaId = config.mangaId;
    const totalChapters = config.totalChapters || 0;

    function updateProgressDisplay(readCount, total) {
        const percent = total > 0 ? Math.round((readCount / total) * 100) : 0;
        const progressBar = document.getElementById('mangaProgressBar');
        const progressText = document.getElementById('mangaProgressText');

        if (progressBar) {
            progressBar.style.width = percent + '%';
            progressBar.textContent = percent + '%';
        }

        if (progressText) {
            progressText.textContent = `${readCount} / ${total} chapters read`;
        }
    }

    const chapterGrid = document.getElementById('chapterGrid');
    if (chapterGrid) {
        chapterGrid.addEventListener('click', async function (e) {
            const btn = e.target.closest('.chapter-btn');
            if (!btn) return;

            const chapterId = btn.dataset.chapterId;
            btn.disabled = true;

            try {
                const res = await fetch(`/Manga/ToggleChapter?chapterId=${chapterId}`, { method: 'POST' });
                const data = await res.json();

                if (data.success) {
                    btn.classList.toggle('btn-success', data.read);
                    btn.classList.toggle('btn-outline-secondary', !data.read);

                    const readCount = document.querySelectorAll('.chapter-btn.btn-success').length;
                    updateProgressDisplay(readCount, totalChapters);
                }
            } finally {
                btn.disabled = false;
            }
        });
    }

    const addChapterBtn = document.getElementById('btnAddChapter');
    if (addChapterBtn) {
        addChapterBtn.addEventListener('click', async function () {
            const btn = this;
            btn.disabled = true;

            try {
                const res = await fetch(`/Manga/AddChapter?mangaId=${mangaId}`, { method: 'POST' });
                const data = await res.json();

                if (data.success) {
                    const grid = document.getElementById('chapterGrid');
                    const col = document.createElement('div');
                    col.className = 'col-4 col-md-2 col-lg-1';
                    col.innerHTML = `
                        <button type="button" class="btn btn-sm w-100 chapter-btn btn-outline-secondary"
                                data-chapter-id="${data.chapterId}" data-chapter-number="${data.chapterNumber}">
                            Ch. ${data.chapterNumber}
                        </button>`;

                    const upcomingCard = grid.querySelector('.chapter-btn-upcoming');
                    if (upcomingCard) {
                        const upcomingCol = upcomingCard.closest('.col-4, .col-md-2, .col-lg-1') || upcomingCard.parentElement;
                        grid.insertBefore(col, upcomingCol);
                    } else {
                        grid.appendChild(col);
                    }
                }
            } finally {
                btn.disabled = false;
            }
        });
    }
});