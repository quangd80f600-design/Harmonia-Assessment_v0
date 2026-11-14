(() => {
    'use strict';
    const root = document.querySelector('[data-test-workspace]');
    if (!root) return;
    const initial = JSON.parse(document.getElementById('workspace-initial').textContent);
    const byId = id => document.getElementById(id);
    const make = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };
    const choice = (text, action, className = 'btn btn-sm btn-outline-secondary') => {
        const button = make('button', className, text); button.type = 'button'; button.addEventListener('click', action); return button;
    };
    const selected = new Map(initial.selected.map(q => [q.id, q]));
    const retainedIds = new Set(initial.retainedIds);
    const originalSnapshots = new Map(initial.selected.filter(q => retainedIds.has(q.id)).map(q => [q.id, q]));
    let order = [...selected.keys()], bankItems = [], page = 1;
    let grade = Number(initial.grade), unitId = null, areaId = null, subskillId = null;
    let bankRequest = 0, bankAbort, searchAbort, searchRequest = 0, searchTimer, questionTimer;
    let treeGeneration = 0;
    let activeTab = initial.hasErrors ? 'selected' : 'bank';
    let saving = false;
    const editor = byId('test-editor');
    const editorState = () => JSON.stringify({
        order, grade, name: byId('Input_TestName').value, description: byId('Input_Description').value
    });
    const savedState = editorState();
    const hasUnsavedChanges = () => !saving
        && (initial.editing || order.length > 0 || byId('Input_TestName').value.trim() || byId('Input_Description').value.trim())
        && (initial.hasErrors || editorState() !== savedState);
    window.addEventListener('beforeunload', event => {
        if (!hasUnsavedChanges()) return;
        event.preventDefault();
        event.returnValue = '';
    });
    // A validation failure renders a new page with hasErrors; it will warn again if left unsaved.
    window.addEventListener('pageshow', () => { saving = false; });
    const tablet = matchMedia('(max-width: 1199.98px)');
    const announce = text => { byId('workspace-announcement').textContent = text; };
    function tabs(focus = false) {
        root.querySelectorAll('[data-panel]').forEach(button => {
            const active = button.dataset.panel === activeTab;
            button.setAttribute('aria-selected', String(active)); button.tabIndex = active ? 0 : -1;
            if (focus && active) button.focus();
        });
        root.querySelectorAll('[data-panel-name]').forEach(panel => {
            panel.hidden = tablet.matches && panel.dataset.panelName !== activeTab;
            panel.setAttribute('role', tablet.matches ? 'tabpanel' : 'region');
        });
    }
    root.querySelectorAll('[data-panel]').forEach(button => {
        button.addEventListener('click', () => { activeTab = button.dataset.panel; tabs(); });
        button.addEventListener('keydown', event => {
            if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
            event.preventDefault();
            const names = ['taxonomy', 'bank', 'selected']; let index = names.indexOf(activeTab);
            index = event.key === 'Home' ? 0 : event.key === 'End' ? 2 : (index + (event.key === 'ArrowRight' ? 1 : 2)) % 3;
            activeTab = names[index]; tabs(true);
        });
    });
    tablet.addEventListener('change', () => tabs());
    async function get(url, values, signal) {
        const target = new URL(url, location.origin);
        for (const [key, value] of Object.entries(values)) if (value !== null && value !== undefined && value !== '') target.searchParams.set(key, value);
        const response = await fetch(target, { signal, headers: { 'Accept': 'application/json' }, cache: 'no-store' });
        if (!response.ok) throw new Error('Không tải được dữ liệu. Hãy thử lại hoặc chọn lại nhánh kiến thức. Các câu đã chọn vẫn được giữ.');
        return response.json();
    }
    function failure(container, retry) {
        const message = make('div', 'workspace-empty', 'Chưa tải được dữ liệu. Các câu đã chọn vẫn được giữ. ');
        message.setAttribute('role', 'alert'); message.append(choice('Thử lại', retry)); container.replaceChildren(message);
    }
    function field(name, value) {
        const input = make('input'); input.type = 'hidden'; input.name = name; input.value = value; return input;
    }
    function syncFields() {
        const fields = document.createDocumentFragment();
        order.forEach((id, i) => fields.append(field(`Input.Picks[${i}].QuestionId`, id), field(`Input.Picks[${i}].Number`, i + 1)));
        byId('selected-fields').replaceChildren(fields);
    }
    function renderSelected(focusId, command) {
        const list = byId('selected-questions'); list.replaceChildren();
        order.forEach((id, index) => {
            const q = selected.get(id), item = make('li'); item.dataset.selectedId = id;
            const content = make('div'); content.append(make('div', 'selected-item-code', q.code), make('p', 'selected-item-text', q.text),
                make('div', 'selected-item-skill', `${q.subskillName} · Khối ${q.grade}`));
            const actions = make('div', 'selected-actions');
            for (const [label, direction] of [['Lên', -1], ['Xuống', 1]]) {
                const button = choice(label, () => {
                    const next = index + direction;
                    if (next < 0 || next >= order.length) return;
                    [order[index], order[next]] = [order[next], order[index]];
                    renderSelected(id, direction < 0 ? 'up' : 'down');
                    announce(`${q.code} đã chuyển tới câu ${next + 1}.`);
                }, '');
                button.dataset.command = direction < 0 ? 'up' : 'down';
                button.disabled = direction < 0 ? index === 0 : index === order.length - 1;
                button.setAttribute('aria-label', `${label}: ${q.code}`); actions.append(button);
            }
            const remove = choice('Bỏ câu', () => removeQuestion(id), 'remove-question'); remove.dataset.command = 'remove';
            remove.setAttribute('aria-label', `Bỏ câu ${q.code}`); actions.append(remove); content.append(actions); item.append(content); list.append(item);
        });
        if (!order.length) list.append(make('p', 'workspace-empty', 'Đề chưa có câu hỏi. Tìm trong ngân hàng và bấm “Thêm vào đề” để bắt đầu.'));
        byId('selected-heading').textContent = `Đã chọn ${order.length} câu`;
        root.querySelectorAll('[data-selected-count]').forEach(span => { span.textContent = order.length; });
        byId('save-draft').disabled = order.length === 0;
        byId('selection-summary').textContent = order.length ? `${order.length} câu · Khối ${grade}. Lựa chọn được giữ khi đổi bộ lọc.` : 'Chọn ít nhất một câu trước khi lưu đề nháp.';
        const mixed = order.filter(id => Number(selected.get(id).grade) !== grade).length;
        byId('grade-warning').hidden = mixed === 0;
        byId('grade-warning').textContent = `${mixed} câu đã chọn không thuộc Khối ${grade}. Hãy đổi lại khối hoặc bỏ các câu đó trước khi lưu.`;
        syncFields(); updateBankButtons();
        if (focusId) {
            const item = [...list.querySelectorAll('[data-selected-id]')].find(node => node.dataset.selectedId === focusId);
            const button = item?.querySelector(`[data-command="${command}"]:not(:disabled)`);
            (button || item?.querySelector('button:not(:disabled)'))?.focus();
        }
    }
    function addQuestion(q) {
        if (selected.has(q.id)) return;
        if (order.length >= 200) { announce('Đề đã đủ 200 câu. Hãy bỏ một câu trước khi thêm.'); return; }
        // Do not refresh retained snapshots from a later bank response.
        selected.set(q.id, originalSnapshots.get(q.id) || q); order.push(q.id); renderSelected(); announce(`Đã thêm ${q.code}. Đề có ${order.length} câu.`);
    }
    function removeQuestion(id) {
        const index = order.indexOf(id), q = selected.get(id); if (!q) return;
        selected.delete(id); order = order.filter(value => value !== id);
        renderSelected(order[Math.min(index, order.length - 1)], 'remove'); announce(`Đã bỏ ${q.code}. Đề còn ${order.length} câu.`);
    }
    function updateBankButtons() {
        byId('question-bank').querySelectorAll('[data-question-id]').forEach(card => {
            const picked = selected.has(card.dataset.questionId), button = card.querySelector('[data-pick]');
            card.dataset.picked = String(picked);
            if (button) { button.textContent = picked ? 'Đã chọn · Bỏ khỏi đề' : 'Thêm vào đề'; button.setAttribute('aria-pressed', String(picked)); }
            const badge = card.querySelector('.picked-badge'); if (badge) badge.hidden = !picked;
        });
    }
    function renderBank(data) {
        bankItems = data.items; const body = byId('question-bank'); body.replaceChildren();
        for (const q of data.items) {
            const card = make('article', 'question-card'); card.dataset.questionId = q.id;
            const header = make('div', 'question-card-head'); header.append(make('span', 'question-code', q.code), make('span', 'picked-badge', 'Đã chọn')); card.append(header);
            const levels = { 1: 'Nhận biết', 2: 'Thông hiểu', 3: 'Vận dụng' };
            card.append(make('p', 'question-meta', `${q.subskillName} · Khối ${q.grade} · ${levels[q.difficulty] || 'Mức độ chưa xác định'}`), make('p', 'question-prompt', q.text));
            const preview = make('details', 'question-preview'); preview.append(make('summary', '', 'Xem lựa chọn và đáp án'));
            const options = make('ol'); options.type = 'A';
            for (const text of [q.a, q.b, q.c, q.d]) options.append(make('li', '', text));
            preview.append(options, make('p', '', `Đáp án đúng: ${q.key}`)); card.append(preview);
            if (retainedIds.has(q.id)) card.append(make('p', 'workspace-note', 'Câu đã có trong đề nháp sẽ giữ nội dung đã lưu.'));
            const button = choice('Thêm vào đề', () => selected.has(q.id) ? removeQuestion(q.id) : addQuestion(q)); button.dataset.pick = q.id; card.append(button); body.append(card);
        }
        if (!data.items.length) body.append(make('p', 'workspace-empty', 'Chưa có câu hỏi phù hợp. Hãy đổi từ khóa, chọn nhánh khác hoặc xem toàn bộ khối.'));
        byId('bank-scope').textContent = data.scope;
        byId('bank-count').textContent = `${data.total} câu phù hợp · ${data.items.length} câu trên trang này`;
        byId('bank-page').textContent = `Trang ${data.page} / ${Math.max(1, Math.ceil(data.total / data.pageSize))}`;
        byId('bank-previous').disabled = page <= 1; byId('bank-next').disabled = page * data.pageSize >= data.total;
        updateBankButtons();
    }
    async function loadBank() {
        const request = ++bankRequest; bankAbort?.abort(); bankAbort = new AbortController();
        const body = byId('question-bank'); body.setAttribute('aria-busy', 'true');
        // Old results must not be selectable under a new scope while the network is pending.
        body.replaceChildren(make('p', 'workspace-empty', 'Đang tải câu hỏi…'));
        byId('bank-scope').textContent = `Khối ${grade} · Đang tải bộ lọc đã chọn…`;
        byId('bank-count').textContent = `Đang giữ ${order.length} câu đã chọn.`;
        byId('bank-previous').disabled = true; byId('bank-next').disabled = true;
        try {
            const data = await get(root.dataset.bankUrl, { Grade: grade, UnitId: unitId, AreaId: areaId, SubskillId: subskillId, Search: byId('question-search').value.trim(), Page: page }, bankAbort.signal);
            if (request === bankRequest) renderBank(data);
        } catch (error) { if (error.name !== 'AbortError' && request === bankRequest) failure(body, loadBank); }
        finally { if (request === bankRequest) body.setAttribute('aria-busy', 'false'); }
    }
    function markBranch() {
        root.querySelectorAll('[data-scope-level]').forEach(button => {
            const id = button.dataset.scopeId, level = button.dataset.scopeLevel;
            button.setAttribute('aria-current', String(level === 'skill' ? subskillId === id : level === 'area' ? areaId === id && !subskillId : unitId === id && !areaId));
        });
        root.querySelectorAll('.tree-node').forEach(node => {
            node.dataset.ancestor = String(node.dataset.level === 'unit' ? node.dataset.nodeId === unitId : node.dataset.nodeId === areaId);
        });
    }
    function chooseScope(node) {
        unitId = node?.unitId || null; areaId = node?.areaId || null; subskillId = node?.level === 'skill' ? node.id : null;
        page = 1; markBranch(); loadBank();
        announce(node ? `Đang xem câu hỏi theo ${node.label}.` : `Đang xem toàn bộ Khối ${grade}.`);
    }
    function scopeButton(node, text) {
        const button = choice(text || node.label, () => chooseScope(node), 'tree-choice' + (text ? ' tree-scope' : ''));
        button.dataset.scopeLevel = node.level; button.dataset.scopeId = node.id;
        if (!text && node.code) button.append(make('small', '', node.code));
        return button;
    }
    function treeNode(node, treeGrade, generation) {
        if (node.level === 'skill') return scopeButton(node);
        const details = make('details', 'tree-node'); details.dataset.level = node.level; details.dataset.nodeId = node.id;
        const summary = make('summary', '', node.label); summary.append(make('small', 'd-block text-muted', `${node.count} ${node.level === 'unit' ? 'nhóm kiến thức' : 'kỹ năng'}`)); details.append(summary);
        const children = make('div', 'tree-children'); details.append(children);
        details.addEventListener('toggle', () => {
            if (details.open && !details.dataset.loaded) {
                details.dataset.loaded = 'loading';
                const level = node.level === 'unit' ? 'area' : 'skill';
                children.append(scopeButton(node, node.level === 'unit' ? 'Xem câu hỏi của bài này' : 'Xem câu hỏi của nhóm này'));
                loadNodes(children, level, node.id, treeGrade, 1, details, generation);
            }
        });
        return details;
    }
    async function loadNodes(container, level, parentId, treeGrade, nodePage = 1, owner, generation = treeGeneration) {
        const loading = make('p', 'workspace-note', 'Đang tải…'); container.append(loading);
        try {
            const data = await get(root.dataset.treeUrl, { Grade: treeGrade, Level: level, ParentId: parentId, Page: nodePage });
            loading.remove();
            if (generation !== treeGeneration || treeGrade !== grade || !container.isConnected) return;
            for (const node of data.items) container.append(treeNode(node, treeGrade, generation));
            if (!data.items.length && nodePage === 1) container.append(make('p', 'workspace-empty', level === 'unit' ? 'Khối này chưa có bài học.' : 'Nhánh này chưa có dữ liệu đang hoạt động.'));
            if (owner) owner.dataset.loaded = 'true';
            if (data.hasMore) {
                const more = choice('Xem thêm', () => { more.remove(); loadNodes(container, level, parentId, treeGrade, nodePage + 1, owner, generation); }, 'workspace-link'); container.append(more);
            }
            markBranch();
        } catch {
            if (generation !== treeGeneration || treeGrade !== grade || !container.isConnected) return;
            loading.remove();
            const retry = choice('Chưa tải được nhánh · Thử lại', () => { retry.remove(); loadNodes(container, level, parentId, treeGrade, nodePage, owner, generation); }, 'workspace-link'); container.append(retry);
            if (owner) owner.dataset.loaded = 'error';
        }
    }
    function loadTree() { treeGeneration++; const tree = byId('taxonomy-tree'); tree.replaceChildren(); loadNodes(tree, 'unit', null, grade); }
    async function searchSkills(nodePage = 1) {
        const value = byId('skill-search').value.trim(), output = byId('skill-results');
        searchAbort?.abort(); const request = ++searchRequest; searchAbort = new AbortController();
        output.hidden = !value; byId('taxonomy-tree').hidden = !!value;
        if (!value) { output.replaceChildren(); return; }
        if (nodePage === 1) output.replaceChildren(make('p', 'workspace-note', 'Đang tìm kỹ năng…'));
        try {
            const data = await get(root.dataset.skillsUrl, { Grade: grade, Search: value, Page: nodePage }, searchAbort.signal);
            if (request !== searchRequest) return;
            if (nodePage === 1) output.replaceChildren();
            for (const node of data.items) { const button = scopeButton(node); button.append(make('small', '', node.path)); output.append(button); }
            if (!data.items.length && nodePage === 1) output.append(make('p', 'workspace-empty', 'Không tìm thấy kỹ năng phù hợp trong khối này.'));
            if (data.hasMore) { const more = choice('Xem thêm kỹ năng', () => { more.remove(); searchSkills(nodePage + 1); }, 'workspace-link'); output.append(more); }
            markBranch();
        } catch (error) { if (error.name !== 'AbortError' && request === searchRequest) failure(output, () => searchSkills()); }
    }
    byId('workspace-grade').addEventListener('change', () => {
        grade = Number(byId('workspace-grade').value); byId('Input_Grade').value = grade;
        unitId = areaId = subskillId = null; page = 1;
        byId('skill-search').value = ''; searchSkills(); loadTree(); renderSelected(); loadBank();
    });
    byId('all-knowledge').addEventListener('click', () => {
        byId('question-search').value = ''; byId('Input_Search').value = '';
        byId('skill-search').value = ''; clearTimeout(searchTimer); clearTimeout(questionTimer);
        searchSkills(); chooseScope(null);
    });
    byId('skill-search').addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => searchSkills(), 250); });
    byId('skill-search').addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); clearTimeout(searchTimer); searchSkills(); } });
    byId('question-search-form').addEventListener('submit', event => { event.preventDefault(); clearTimeout(questionTimer); byId('Input_Search').value = byId('question-search').value; page = 1; loadBank(); });
    byId('question-search').addEventListener('input', () => { byId('Input_Search').value = byId('question-search').value; clearTimeout(questionTimer); questionTimer = setTimeout(() => { page = 1; loadBank(); }, 300); });
    byId('bank-previous').addEventListener('click', () => { if (page > 1) { page--; loadBank(); } });
    byId('bank-next').addEventListener('click', () => { page++; loadBank(); });
    editor.addEventListener('submit', () => { syncFields(); saving = true; });
    renderSelected(); tabs(); loadTree(); loadBank(); root.dataset.ready = 'true';
})();
