(() => {
    "use strict";

    document.querySelectorAll("[data-class-delete]").forEach(form => {
        const confirmed = form.querySelector('input[name="confirmed"]');
        const button = form.querySelector("[data-class-delete-button]");
        if (!confirmed || !button) return;

        form.addEventListener("submit", event => {
            confirmed.value = "false";
            if (!window.confirm(`Bạn có chắc muốn xóa vĩnh viễn lớp "${form.dataset.className}"?\nChỉ xóa lớp không có dữ liệu tham chiếu. Thao tác không thể hoàn tác.`)) {
                event.preventDefault();
                return;
            }
            confirmed.value = "true";
        });
        // Without JavaScript, a destructive POST cannot bypass the confirmation dialog.
        button.disabled = false;
    });
})();
