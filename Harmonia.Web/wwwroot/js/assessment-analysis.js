(() => {
    "use strict";

    document.querySelectorAll("[data-question-analysis]").forEach(page => {
        const buttons = Array.from(page.querySelectorAll("[data-question-select]"));
        const panels = Array.from(page.querySelectorAll("[data-question-panel]"));
        const placeholder = page.querySelector("[data-question-placeholder]");
        const announcement = page.querySelector("[data-question-announcement]");

        function select(button) {
            const target = panels.find(panel => panel.id === button.dataset.questionSelect);
            if (!target) return;
            buttons.forEach(item => item.setAttribute("aria-pressed", String(item === button)));
            panels.forEach(panel => {
                panel.hidden = panel !== target;
                panel.classList.toggle("is-default", panel === target);
            });
            if (placeholder) placeholder.hidden = true;
            // Keep the newly selected question visible if the previous detail was scrolled.
            const detailScroll = page.querySelector("[data-question-detail-scroll]");
            if (detailScroll) detailScroll.scrollTop = 0;
            if (announcement) {
                announcement.textContent = `Đang hiển thị ${target.dataset.questionTitle}. Nội dung ở mục 03.`;
            }
        }

        // Initial selection is rendered server-side, including the all-missing state.
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
                    default: return; // Enter / Space retain native button behavior.
                }
                event.preventDefault();
                buttons[next].focus();
                select(buttons[next]);
            });
        });
    });
})();
