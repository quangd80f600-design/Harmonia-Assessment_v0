(() => {
    "use strict";

    document.querySelectorAll("[data-individual-diagnosis]").forEach(page => {
        const buttons = Array.from(page.querySelectorAll("[data-subskill-select]"));
        const panels = Array.from(page.querySelectorAll("[data-subskill-panel]"));
        const detailScroll = page.querySelector("[data-individual-detail-scroll]");
        const announcement = page.querySelector("[data-subskill-announcement]");

        function select(button) {
            const target = panels.find(panel => panel.id === button.dataset.subskillSelect);
            if (!target) return;

            buttons.forEach(item => item.setAttribute("aria-pressed", String(item === button)));
            panels.forEach(panel => {
                panel.hidden = panel !== target;
                panel.classList.toggle("is-default", panel === target);
            });
            if (detailScroll) detailScroll.scrollTop = 0;
            if (announcement) {
                announcement.textContent = `Đang hiển thị bằng chứng: ${target.dataset.subskillName}. Nội dung ở mục 02.`;
            }
        }

        // The first Subskill and its evidence are already selected in the Razor output.
        panels.forEach(panel => { panel.hidden = !panel.classList.contains("is-default"); });
        buttons.forEach((button, index) => {
            button.addEventListener("click", () => select(button));
            button.addEventListener("keydown", event => {
                let next;
                switch (event.key) {
                    case "ArrowDown": next = (index + 1) % buttons.length; break;
                    case "ArrowUp": next = (index - 1 + buttons.length) % buttons.length; break;
                    case "Home": next = 0; break;
                    case "End": next = buttons.length - 1; break;
                    default: return; // Tab, Enter and Space retain native button behavior.
                }
                event.preventDefault();
                buttons[next].focus();
                select(buttons[next]);
            });
        });
    });
})();
