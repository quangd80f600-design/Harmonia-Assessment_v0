(() => {
    const form = document.querySelector('[data-observation-form]');
    let submitting = false;
    const selected = () => form ? [...form.querySelectorAll('.observation-save-row:checked:not(:disabled)')] : [];
    const update = () => {
        const status = form?.querySelector('[data-save-count]');
        if (status) status.textContent = `Đã chọn ${selected().length} dòng để lưu`;
    };
    form?.addEventListener('change', event => {
        const row = event.target.closest('tr[data-editable="true"]');
        if (row && event.target.matches('.observation-choice')) {
            row.querySelector('.observation-save-row').checked = [...row.querySelectorAll('.observation-choice')].some(s => s.value !== s.dataset.original);
        }
        update();
    });
    form?.addEventListener('submit', event => {
        if (!selected().length) { event.preventDefault(); update(); return; }
        submitting = true;
        const button = event.submitter;
        if (button) { button.disabled = true; button.textContent = 'Đang lưu…'; }
    });
    window.addEventListener('beforeunload', event => {
        if (!submitting && selected().length) { event.preventDefault(); event.returnValue = ''; }
    });
    document.querySelector('#observation-class')?.addEventListener('change', () => {
        const author = document.querySelector('#observation-author');
        if (author) author.value = '';
    });
    update();
})();
