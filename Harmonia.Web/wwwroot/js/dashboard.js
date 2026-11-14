(() => {
    const form = document.getElementById('dashboard-filter');
    const classes = document.getElementById('dashboard-class');
    const assessment = document.getElementById('dashboard-assessment');
    if (!form || !classes || !assessment) return;
    document.getElementById('dashboard-class-search')?.addEventListener('input', event => {
        const text = event.target.value.trim().toLocaleLowerCase('vi');
        Array.from(classes.options).forEach(option => {
            option.hidden = !!option.value && !option.selected && !option.text.toLocaleLowerCase('vi').includes(text);
        });
    });
    classes.addEventListener('change', () => {
        assessment.value = '';
        form.requestSubmit();
    });
})();
