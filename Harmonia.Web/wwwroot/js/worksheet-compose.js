(() => {
    // Protect the current composition when filters/navigation would replace the form.
    const form = document.querySelector('#worksheet-form');
    let dirty = false;
    form?.addEventListener('input', () => { dirty = true; });
    form?.addEventListener('change', () => { dirty = true; });
    form?.addEventListener('submit', () => { dirty = false; });
    document.querySelector('[data-group-filter]')?.addEventListener('submit', event => {
        if (dirty && !window.confirm('Đổi bộ lọc sẽ bỏ lựa chọn chưa lưu. Bạn muốn tiếp tục?')) {
            event.preventDefault(); return;
        }
        dirty = false;
    });
    window.addEventListener('beforeunload', event => {
        if (dirty) { event.preventDefault(); event.returnValue = ''; }
    });
    const count = document.querySelector('#worksheet-count');
    document.querySelectorAll('[data-worksheet-pick]').forEach(row => {
        const box = row.querySelector('.worksheet-toggle');
        const update = () => {
            row.querySelectorAll('[data-pick-field]').forEach(x => x.disabled = !box.checked);
            if (count) count.textContent = 'Đã chọn ' + document.querySelectorAll('.worksheet-toggle:checked').length + ' mục.';
        };
        box.addEventListener('change', update); update();
    });
    const recipients = [...document.querySelectorAll('.group-recipient')];
    const updateRecipients = () => {
        const output = document.querySelector('#group-recipient-count');
        if (output) output.textContent = 'Đã chọn ' + recipients.filter(x => x.checked).length + ' học sinh.';
    };
    recipients.forEach(x => x.addEventListener('change', updateRecipients));
    document.querySelector('#group-select-all')?.addEventListener('click', () => { recipients.forEach(x => x.checked = true); dirty = true; updateRecipients(); });
    document.querySelector('#group-clear')?.addEventListener('click', () => { recipients.forEach(x => x.checked = false); dirty = true; updateRecipients(); });
    updateRecipients();
    const save = document.querySelector('#worksheet-save'); if (save) save.disabled = false;
})();
