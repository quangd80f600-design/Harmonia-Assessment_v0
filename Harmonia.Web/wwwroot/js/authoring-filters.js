(() => {
    const grade = document.getElementById('grade');
    const skill = document.querySelector('[data-subskill-filter]');
    if (!grade || !skill) return;
    grade.addEventListener('change', () => {
        skill.disabled = true;
        document.querySelector('[data-clear-subskill]').value = 'true';
        document.querySelector('[data-subskill-context]')?.setAttribute('hidden', '');
        document.getElementById('subskill-filter-notice').hidden = false;
    });
})();
