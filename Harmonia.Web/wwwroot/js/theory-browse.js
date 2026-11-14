// Clear dependent selections before the server renders the next filter level.
const theoryFilter = document.querySelector('[data-theory-filter]');
if (theoryFilter) {
    const names = ['Grade', 'UnitId', 'AreaId', 'SubskillId'];
    names.forEach((name, index) => theoryFilter.elements[name].addEventListener('change', () => {
        names.slice(index + 1).forEach(child => { theoryFilter.elements[child].value = ''; });
        theoryFilter.requestSubmit();
    }));
}
