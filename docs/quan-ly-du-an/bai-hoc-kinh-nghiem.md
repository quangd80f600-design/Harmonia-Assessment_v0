# Bài học kinh nghiệm

> Nội dung trích từ báo cáo đề án môn Hệ thống thông tin (bản nộp tháng 11/2026).

Qua quá trình thực hiện đề án, em rút ra các bài học sau:

## a) Về phân tích và quản lý phạm vi

- **Chốt phạm vi bằng văn bản và giữ kỷ luật.** Em duy trì danh sách “ngoài phạm vi” (thi trực tuyến, chấm bằng AI, LMS…) và nhật ký quyết định cho từng kịch bản S1–S10. Nhờ đó, mỗi khi có ý tưởng mới, em có căn cứ để quyết định làm ngay, để sau hay không làm, tránh dàn trải.

- **Phân biệt “đã viết mã”, “đã kiểm thử” và “đã nghiệm thu”.** Nhiều lần một chức năng có mã nguồn nhưng chưa được kiểm chứng độc lập. Việc ghi rõ trạng thái giúp em không báo cáo quá mức thực tế.

- **Tài liệu dễ lệch với mã nguồn.** Một số tài liệu ban đầu vẫn ghi “20 bảng”, “Gemini chưa làm” trong khi hệ thống đã thay đổi. Bài học là coi mã nguồn và lược đồ CSDL là nguồn sự thật, và cập nhật tài liệu ngay sau mỗi lần nghiệm thu.

## b) Về thiết kế dữ liệu

- **Dữ liệu lịch sử phải bất biến.** Ban đầu, phân tích kết quả có thể nối thẳng sang bảng Question. Nếu câu hỏi bị sửa, kết quả cũ sẽ sai mà không ai biết. Thiết kế bản chụp (snapshot) trong QuestionInTest giải quyết triệt để vấn đề này và trở thành nguyên tắc chung cho phiếu ôn tập.

- **Đặt quy tắc nghiệp vụ ở tầng CSDL.** Ràng buộc EXCLUDE, CHECK và chỉ mục duy nhất có điều kiện đã chặn được nhiều lỗi mà kiểm tra ở tầng ứng dụng bỏ sót, đặc biệt khi hai người thao tác đồng thời.

- **“Không có dữ liệu” khác “bằng 0”.** Phân biệt học sinh vắng bài với học sinh 0 điểm, và mẫu số bằng 0 với tỷ lệ 0%, là chi tiết nhỏ nhưng quyết định độ tin cậy của báo cáo.

## c) Về cài đặt

- **Ghi dữ liệu theo kiểu “tất cả hoặc không”.** Nhập kết quả phải ghi đủ cho mọi học sinh hoặc không ghi gì; nếu ghi một phần thì rất khó phát hiện và sửa.

- **Phân quyền theo dữ liệu.** Chỉ kiểm tra “là giáo viên” là chưa đủ: giáo viên A không được xem lớp của giáo viên B chỉ bằng cách đổi ID trên đường dẫn.

## d) Về kiểm thử

- **Viết ca kiểm thử trước khi chạy.** Ghi rõ các bước, dữ liệu và kết quả mong đợi giúp đánh giá Đạt / Không đạt khách quan, không dựa vào cảm giác “thấy chạy được”.

- **Chuẩn bị dữ liệu mô phỏng có chủ đích.** Dữ liệu cần có sẵn các tình huống biên như học sinh đúng toàn bộ, sai toàn bộ một kỹ năng, bỏ trống, chưa có kết quả, file có một dòng lỗi; chỉ thử với dữ liệu “đẹp” thì khó phát hiện lỗi.

- **Lưu minh chứng ngay khi chạy.** Ảnh chụp màn hình gắn với mã ca giúp truy vết và trình bày kết quả; ca nào không có minh chứng thì khó khẳng định là đã đạt.
