(() => {
    const choice = document.getElementById('observation');
    if (!choice) return;
    choice.addEventListener('change', () => {
        document.querySelectorAll('[data-observation-preview]').forEach(panel => {
            panel.hidden = panel.dataset.observationPreview !== choice.value;
        });
        const guidance = document.getElementById('observation-guidance');
        if (guidance) {
            const changedAiBasis = guidance.dataset.aiDraft === 'true' && choice.value !== (guidance.dataset.savedObservation || '');
            guidance.textContent = changedAiBasis
                ? 'Nháp AI đang gắn với ghi nhận khác. Hãy hủy nháp AI, chọn ghi nhận này rồi lưu nháp mới; nội dung cũ vẫn ở lịch sử.'
                : choice.value
                    ? 'Sau khi lưu nháp, Gemini sẽ dùng các tiêu chí đã chọn để gợi ý đoạn quá trình học tập.'
                    : 'Chưa chọn ghi nhận: Gemini chỉ gợi ý đoạn kết quả đánh giá; đoạn quá trình sẽ để trống.';
        }
    });
    // Approval always applies to the saved draft. Changing the editor requires saving first.
    const dirty = () => {
        document.querySelectorAll('form[action$="/Approve"] button, [data-gemini-form] button').forEach(button => { button.disabled = true; });
    };
    choice.addEventListener('change', dirty);
    document.getElementById('comment-text')?.addEventListener('input', dirty);
    document.getElementById('process-text')?.addEventListener('input', dirty);
    if (document.querySelector('[data-editor-unsaved="true"]')) dirty();
    document.querySelector('[data-inline-observation]')?.addEventListener('submit', event => {
        const draft = document.querySelector('form[action$="/Save"]');
        event.currentTarget.querySelectorAll('input[name^="Draft."]').forEach(field => {
            const original = draft?.elements.namedItem(field.name.slice(6));
            if (original) field.value = original.value;
        });
    });
    document.querySelector('[data-gemini-form]')?.addEventListener('submit', event => {
        const form = event.currentTarget;
        if (form.dataset.busy === 'true') { event.preventDefault(); return; }
        form.dataset.busy = 'true';
        document.querySelectorAll('.comment-editor button, #comment-text, #process-text, #observation').forEach(control => { control.disabled = true; });
        form.querySelector('[data-gemini-status]').textContent = 'Đang tạo gợi ý. Vui lòng chờ; nội dung chỉ là bản nháp.';
    });
})();
