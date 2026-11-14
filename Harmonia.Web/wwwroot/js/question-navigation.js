(() => {
    'use strict';
    const root = document.querySelector('[data-question-navigation]');
    if (!root) return;
    const tree = document.getElementById('bank-taxonomy-tree'), results = document.getElementById('bank-skill-results');
    const panel = document.getElementById('question-knowledge'), toggle = root.querySelector('.knowledge-toggle');
    const grade = root.dataset.grade, current = { unit: root.dataset.unit, area: root.dataset.area, skill: root.dataset.skill };
    const mobile = matchMedia('(max-width: 1199.98px)');
    function displayPanel() { panel.hidden = mobile.matches && toggle.getAttribute('aria-expanded') !== 'true'; }
    toggle.addEventListener('click', () => { toggle.setAttribute('aria-expanded', String(toggle.getAttribute('aria-expanded') !== 'true')); displayPanel(); });
    mobile.addEventListener('change', displayPanel); displayPanel();
    const make = (tag, text, cls) => { const el = document.createElement(tag); if (text) el.textContent = text; if (cls) el.className = cls; return el; };
    function bankUrl(node) {
        const url = new URL(root.dataset.bankUrl, location.origin);
        url.searchParams.set('Grade', grade);
        if (root.dataset.term) url.searchParams.set('Term', root.dataset.term);
        const purpose = root.dataset.usagePurpose;
        if (purpose) url.searchParams.set('UsagePurpose', purpose);
        if (node?.unitId) url.searchParams.set('UnitId', node.unitId);
        if (node?.areaId) url.searchParams.set('AreaId', node.areaId);
        if (node?.level === 'skill') url.searchParams.set('SubskillId', node.id);
        return url;
    }
    document.getElementById('bank-grade').addEventListener('change', event => {
        const url = bankUrl(null); url.searchParams.set('Grade', event.target.value); location.assign(url);
    });
    async function get(endpoint, values, signal) {
        const url = new URL(endpoint, location.origin);
        for (const [key, value] of Object.entries(values)) if (value) url.searchParams.set(key, value);
        const response = await fetch(url, { signal, cache: 'no-store', headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error('Không tải được cây kiến thức.');
        return response.json();
    }
    function retry(container, action) {
        const message = make('p', 'Chưa tải được dữ liệu. ', 'workspace-empty'); message.setAttribute('role', 'alert');
        const button = make('button', 'Thử lại', 'btn btn-outline-secondary btn-sm'); button.type = 'button';
        button.addEventListener('click', () => { message.remove(); action(); }); message.append(button); container.append(message);
    }
    function scopeLink(node, text) {
        const link = make('a', text || node.label, 'tree-choice'); link.href = bankUrl(node);
        link.dataset.scopeLevel = node.level; link.dataset.scopeId = node.id;
        const selected = node.level === 'skill' ? current.skill === node.id : node.level === 'area' ? current.area === node.id && !current.skill : current.unit === node.id && !current.area;
        if (selected) link.setAttribute('aria-current', 'page');
        if (!text && node.code) link.append(make('small', node.code));
        return link;
    }
    function treeNode(node) {
        if (node.level === 'skill') return scopeLink(node);
        const details = make('details', null, 'tree-node'); details.dataset.level = node.level; details.dataset.nodeId = node.id;
        const ancestor = current[node.level] === node.id; details.dataset.ancestor = String(ancestor);
        const summary = make('summary', node.label); summary.append(make('small', `${node.count} ${node.level === 'unit' ? 'nhóm kiến thức' : 'kỹ năng'}`, 'd-block text-muted'));
        const children = make('div', null, 'tree-children'); details.append(summary, children);
        details.addEventListener('toggle', () => {
            if (!details.open || details.dataset.loaded) return;
            details.dataset.loaded = 'true';
            children.append(scopeLink(node, node.level === 'unit' ? 'Xem câu hỏi của bài này' : 'Xem câu hỏi của nhóm này'));
            const list = make('div'); children.append(list); loadNodes(list, node.level === 'unit' ? 'area' : 'skill', node.id);
        });
        if (ancestor) details.open = true;
        return details;
    }
    async function loadNodes(container, level = 'unit', parent = '', page = 1) {
        const loading = make('p', 'Đang tải…', 'workspace-note'); container.append(loading);
        try {
            const data = await get(root.dataset.treeUrl, { Grade: grade, Level: level, ParentId: parent, Page: page });
            loading.remove();
            data.items.forEach(node => container.append(treeNode(node)));
            if (!data.items.length && page === 1) container.append(make('p', 'Nhánh này chưa có dữ liệu đang hoạt động.', 'workspace-empty'));
            if (data.hasMore) {
                const more = make('button', 'Xem thêm', 'btn btn-link'); more.type = 'button';
                more.addEventListener('click', () => { more.remove(); loadNodes(container, level, parent, page + 1); }); container.append(more);
            }
        } catch { loading.remove(); retry(container, () => loadNodes(container, level, parent, page)); }
    }
    let searchId = 0, abort, timer;
    const search = document.getElementById('bank-skill-search');
    async function searchSkills(page = 1) {
        const term = search.value.trim(), request = ++searchId; abort?.abort();
        results.hidden = !term; tree.hidden = !!term;
        if (page === 1) results.replaceChildren();
        if (!term || !grade) return;
        abort = new AbortController();
        try {
            const data = await get(root.dataset.skillsUrl, { Grade: grade, Search: term, Page: page }, abort.signal);
            if (request !== searchId) return;
            data.items.forEach(node => { const link = scopeLink(node); link.append(make('small', node.path)); results.append(link); });
            if (!data.items.length && page === 1) results.append(make('p', 'Không tìm thấy kỹ năng phù hợp.', 'workspace-empty'));
            if (data.hasMore) {
                const more = make('button', 'Xem thêm kỹ năng', 'btn btn-link'); more.type = 'button';
                more.addEventListener('click', () => { more.remove(); searchSkills(page + 1); }); results.append(more);
            }
        } catch (error) { if (error.name !== 'AbortError' && request === searchId) retry(results, () => searchSkills(page)); }
    }
    search.addEventListener('input', () => { clearTimeout(timer); ++searchId; abort?.abort(); timer = setTimeout(searchSkills, 250); });
    tree.replaceChildren();
    if (grade) loadNodes(tree);
    else { tree.append(make('p', 'Chọn khối để mở cây kiến thức.', 'workspace-empty')); search.disabled = true; }
})();
