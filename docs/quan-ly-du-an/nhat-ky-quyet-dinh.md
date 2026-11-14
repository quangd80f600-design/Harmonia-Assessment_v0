# Nhật ký quyết định

Ghi lại các quyết định thiết kế quan trọng theo mẫu: **bối cảnh → các phương án → quyết định → hệ quả**. Mỗi quyết định được truy vết tới yêu cầu, quy tắc nghiệp vụ (BR) hoặc kịch bản (S) trong [02-phan-tich](../02-phan-tich/README.md).

| Mã | Quyết định | Liên quan |
|---|---|---|
| [QĐ-01](#qđ-01--tổ-chức-thi-trên-giấy-nhập-kết-quả-bằng-csv) | Tổ chức thi trên giấy, nhập kết quả bằng CSV | Phạm vi, S8 |
| [QĐ-02](#qđ-02--chọn-postgresql-và-đưa-quy-tắc-xuống-tầng-csdl) | Chọn PostgreSQL, đưa quy tắc xuống tầng CSDL | BR01, BR09 |
| [QĐ-03](#qđ-03--database-first-với-ef-core) | Database-First với EF Core | Thiết kế CSDL |
| [QĐ-04](#qđ-04--lưu-bản-chụp-snapshot-câu-hỏi-khi-tạo-đề) | Lưu bản chụp câu hỏi khi tạo đề | S7, BR03 |
| [QĐ-05](#qđ-05--nhập-kết-quả-theo-nguyên-tắc-tất-cả-hoặc-không) | Nhập kết quả theo nguyên tắc "tất cả hoặc không" | S8, BR05 |
| [QĐ-06](#qđ-06--phân-quyền-theo-phạm-vi-dữ-liệu-trả-về-404) | Phân quyền theo phạm vi dữ liệu, trả về 404 | BR02 |
| [QĐ-07](#qđ-07--hai-vai-trò-độc-lập-một-tài-khoản-có-thể-giữ-cả-hai) | Hai vai trò độc lập, một tài khoản có thể giữ cả hai | F01 |
| [QĐ-08](#qđ-08--phân-biệt-chưa-có-kết-quả-với-0) | Phân biệt "chưa có kết quả" với 0% | S1, S4, BR07 |
| [QĐ-09](#qđ-09--gemini-chỉ-gợi-ý-giáo-viên-duyệt-cuối-cùng) | Gemini chỉ gợi ý, giáo viên duyệt cuối cùng | BR09 |
| [QĐ-10](#qđ-10--response-chỉ-lưu-đúng--sai-ở-v0) | Response chỉ lưu đúng / sai ở V0 | S1, S2 |
| [QĐ-11](#qđ-11--hoãn-kịch-bản-s6) | Hoãn kịch bản S6 | S6 |
| [QĐ-12](#qđ-12--nghiệm-thu-bằng-kiểm-thử-thủ-công-có-minh-chứng) | Nghiệm thu bằng kiểm thử thủ công có minh chứng | 04-kiem-thu |

---

### QĐ-01 – Tổ chức thi trên giấy, nhập kết quả bằng CSV
- **Bối cảnh:** trường THCS chủ yếu kiểm tra trên giấy hoặc phiếu tô; thi trực tuyến đòi hỏi thiết bị, đường truyền và chống gian lận.
- **Phương án:** (a) xây dựng thi trực tuyến; (b) giữ thi trên giấy, nhập kết quả bằng file.
- **Quyết định:** (b). Thi trực tuyến, chấm bằng AI, học tập thích ứng, LMS được ghi vào danh sách **ngoài phạm vi**.
- **Hệ quả:** tập trung nguồn lực vào phần chẩn đoán và can thiệp – giá trị chính của đề tài; cần thiết kế kỹ khâu kiểm tra file CSV.

### QĐ-02 – Chọn PostgreSQL và đưa quy tắc xuống tầng CSDL
- **Bối cảnh:** quy tắc "một học sinh chỉ thuộc một lớp tại một thời điểm", "một nhận xét Draft / Approved cho mỗi học sinh trong một đợt" rất dễ bị vi phạm khi hai người thao tác đồng thời.
- **Phương án:** MySQL, SQL Server, SQLite, PostgreSQL.
- **Quyết định:** PostgreSQL – có ràng buộc `EXCLUDE` (chặn khoảng ngày chồng lấn), chỉ mục duy nhất có điều kiện, `CHECK` phức hợp, `JSONB`; mã nguồn mở, miễn phí.
- **Hệ quả:** dữ liệu được bảo vệ cả khi mã ứng dụng có lỗi; cần extension `btree_gist`.

### QĐ-03 – Database-First với EF Core
- **Bối cảnh:** với đề án HTTT, thiết kế CSDL là sản phẩm chính và cần được rà soát độc lập với mã nguồn.
- **Quyết định:** thiết kế và kiểm chứng lược đồ bằng SQL trước, sau đó scaffold lớp thực thể C#; riêng schema `auth` của Identity dùng migration.
- **Hệ quả:** lược đồ là nguồn sự thật ([database/schema.sql](../../database/schema.sql)); mỗi thay đổi lược đồ có script riêng trong [database/migrations](../../database/migrations/).

### QĐ-04 – Lưu bản chụp (snapshot) câu hỏi khi tạo đề
- **Bối cảnh:** nếu phân tích kết quả nối thẳng sang bảng `Question`, khi câu hỏi bị sửa thì kết quả cũ sai mà không ai biết.
- **Quyết định:** `QuestionInTest` lưu nội dung, lựa chọn, đáp án và Subskill tại thời điểm tạo đề (cột `SnS*`); đề đã phát hành không sửa được, muốn thay đổi phải sao chép thành đề nháp mới.
- **Hệ quả:** kết quả lịch sử luôn giải thích được; nguyên tắc này được áp dụng lại cho phiếu ôn tập.

### QĐ-05 – Nhập kết quả theo nguyên tắc "tất cả hoặc không"
- **Bối cảnh:** nhập một phần file rất khó phát hiện và khó sửa.
- **Quyết định:** quy trình Xem trước → Xác nhận; kiểm tra toàn bộ file trước khi ghi; ghi `ImportBatch`, `Attempt`, `Response` trong một giao dịch; lỗi bất kỳ dòng nào thì không ghi dòng nào.
- **Hệ quả:** không bao giờ có kết quả ghi dở; mọi lần nhập, kể cả thất bại, đều để lại `ImportBatch`.

### QĐ-06 – Phân quyền theo phạm vi dữ liệu, trả về 404
- **Bối cảnh:** chỉ kiểm tra "là giáo viên" là chưa đủ – giáo viên A có thể đổi ID trên đường dẫn để xem lớp của giáo viên B.
- **Quyết định:** bộ lọc phân quyền kiểm tra quyền với từng lớp / đợt đánh giá trước mỗi yêu cầu, dựa trên phân công còn hiệu lực; ngoài phạm vi trả về 404 "Dữ liệu không tồn tại hoặc nằm ngoài phạm vi quyền".
- **Hệ quả:** không lộ cả sự tồn tại của dữ liệu ngoài quyền; đã nghiệm thu bằng ca TC05.

### QĐ-07 – Hai vai trò độc lập, một tài khoản có thể giữ cả hai
- **Bối cảnh:** tổ trưởng chuyên môn vừa quản lý vừa trực tiếp dạy lớp.
- **Quyết định:** vai trò Quản lý học vụ không tự kế thừa Giáo viên; một tài khoản quản lý có thể được liên kết thêm hồ sơ giáo viên để có vai trò Giáo viên.
- **Hệ quả:** không phải tạo hai tài khoản cho một người; đã nghiệm thu bằng ca TC02.

### QĐ-08 – Phân biệt "chưa có kết quả" với 0%
- **Bối cảnh:** coi học sinh vắng bài là 0 điểm làm sai lệch tỷ lệ của lớp.
- **Quyết định:** học sinh chưa có kết quả hiển thị "Chưa có kết quả / Missing", không tính vào mẫu số; mẫu số bằng 0 khác tỷ lệ 0%.
- **Hệ quả:** báo cáo đáng tin cậy hơn; đã nghiệm thu bằng ca TC19.

### QĐ-09 – Gemini chỉ gợi ý, giáo viên duyệt cuối cùng
- **Bối cảnh:** viết nhận xét cho từng học sinh tốn thời gian, nhưng nội dung gửi phụ huynh phải do giáo viên chịu trách nhiệm.
- **Quyết định:** Gemini là tính năng tùy chọn; dữ liệu gửi đi đã ẩn danh (không họ tên, mã học sinh); kết quả luôn là bản nháp; hệ thống đóng giao dịch CSDL trước khi gọi dịch vụ ngoài và kiểm tra lại toàn bộ trước khi ghi.
- **Hệ quả:** lỗi hoặc quá thời gian chờ của Gemini không làm mất bản nháp của giáo viên; CSDL lưu bản gốc AI, tên model, phiên bản prompt để truy vết.

### QĐ-10 – Response chỉ lưu đúng / sai ở V0
- **Bối cảnh:** file kết quả từ công cụ chấm phiếu chỉ cần đúng / sai để chẩn đoán theo Subskill.
- **Quyết định:** V0 chỉ lưu đúng / sai, không lưu lựa chọn A/B/C/D.
- **Hệ quả:** chưa phân tích được phương án nhiễu – ghi vào hạn chế và hướng phát triển.

### QĐ-11 – Hoãn kịch bản S6
- **Bối cảnh:** thời gian thực hiện đề án có hạn.
- **Quyết định:** hoãn S6 (phân tích tiến bộ chung của lớp qua nhiều đợt); dữ liệu cần thiết đã được lưu đầy đủ.
- **Hệ quả:** có thể bổ sung sau mà không cần đổi lược đồ.

### QĐ-12 – Nghiệm thu bằng kiểm thử thủ công có minh chứng
- **Bối cảnh:** đề án cá nhân, cần bằng chứng rõ ràng cho từng chức năng.
- **Quyết định:** viết ca kiểm thử (bước, dữ liệu, kết quả mong đợi) trước khi chạy; kiểm thử hộp đen trên Chrome với dữ liệu mô phỏng; chụp màn hình làm minh chứng; chỉ đánh dấu Đạt khi khớp đủ kết quả mong đợi.
- **Hệ quả:** 24 ca đạt, có minh chứng ([04-kiem-thu](../04-kiem-thu/README.md)); chưa kiểm thử hiệu năng và chưa thử với người dùng thật.
