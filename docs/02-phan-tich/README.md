# 02. Phân tích yêu cầu

> Nội dung trích từ báo cáo đề án môn Hệ thống thông tin (bản nộp tháng 11/2026).

- [Sơ đồ và đặc tả use case](use-case.md)

## Kịch bản nghiệp vụ

Các kịch bản nghiệp vụ được đánh mã S1–S10 để truy vết từ yêu cầu đến mã nguồn và kiểm thử:

| **Mã** | **Kịch bản**           | **Nội dung**                                                                                        |
|--------|------------------------|-----------------------------------------------------------------------------------------------------|
| S1     | Chẩn đoán cá nhân      | Tỷ lệ đúng / sai theo Subskill của một học sinh trong một đợt đánh giá                              |
| S2     | Phân tích lớp          | S2.1: tổng hợp theo Subskill, xếp theo tỷ lệ sai giảm dần; S2.2: chi tiết từng câu hỏi của Subskill |
| S3     | Theo dõi sau can thiệp | So sánh trước / sau theo từng học sinh × Subskill mục tiêu của gói                                  |
| S4     | Tình hình tham gia     | Sĩ số theo lớp tại ngày đánh giá; học sinh đã có / chưa có kết quả                                  |
| S5     | Tiến độ học sinh       | Kết quả của một học sinh qua nhiều đợt đánh giá                                                     |
| S7     | Snapshot đề            | Lưu bản chụp câu hỏi khi tạo đề, không sửa đề đã sử dụng                                            |
| S8     | Nhập kết quả           | Nhập file CSV, kiểm tra, chấm và ghi trong một giao dịch                                            |
| S9     | Quản lý can thiệp      | Tạo gói, chọn học liệu, giao học sinh, ghi ngày phát                                                |
| S10    | Báo cáo học sinh       | Báo cáo cá nhân gồm kết quả, nhận xét đã duyệt và kế hoạch can thiệp                                |

*Ghi chú: Kịch bản S6 (phân tích tiến bộ chung của cả lớp qua nhiều đợt đánh giá) chưa được thực hiện trong phạm vi đề án do hạn chế về thời gian; dữ liệu cần thiết đã được thiết kế và lưu trữ đầy đủ trong cơ sở dữ liệu.*

## 3.3. Xác định tác nhân

| **Tác nhân**   | **Loại**             | **Mô tả**                                                                                                                               |
|----------------|----------------------|-----------------------------------------------------------------------------------------------------------------------------------------|
| Giáo viên      | Chính                | Soạn câu hỏi, đề; tạo đợt đánh giá, nhập kết quả, phân tích, can thiệp, ghi nhận, lập nhận xét – chỉ trong các lớp đang được phân công  |
| Quản lý học vụ | Chính                | Quản lý lớp, học sinh, giáo viên, tài khoản và phân công; sửa câu hỏi, sửa lý thuyết; thực hiện được mọi nghiệp vụ trên toàn bộ dữ liệu |
| Gemini API     | Phụ (hệ thống ngoài) | Sinh bản nháp nhận xét khi giáo viên yêu cầu; không bắt buộc                                                                            |

Hai vai trò Giáo viên và Quản lý học vụ **độc lập**, Quản lý học vụ không tự nhiên kế thừa Giáo viên. Một tài khoản có thể giữ cả hai vai trò khi được liên kết với một hồ sơ giáo viên.

## 3.4. Yêu cầu chức năng

| **Mã** | **Nhóm chức năng**   | **Mô tả**                                                                                                                            | **Tác nhân**                   |
|--------|----------------------|--------------------------------------------------------------------------------------------------------------------------------------|--------------------------------|
| F01    | Tài khoản và bảo mật | Đăng nhập, đăng xuất, đổi mật khẩu; cấp, đặt lại mật khẩu, khóa / mở tài khoản giáo viên; liên kết tài khoản với hồ sơ giáo viên     | GV, QLHV                       |
| F02    | Quản lý học vụ       | Tạo / xóa lớp; nhập danh sách học sinh từ CSV; tạo hồ sơ giáo viên; phân công giáo viên vào lớp theo khoảng thời gian                | QLHV (GV chỉ xem)              |
| F03    | Ngân hàng câu hỏi    | Tra cứu câu hỏi theo taxonomy; tạo, sao chép câu hỏi; nhập câu hỏi từ CSV; sửa câu hỏi chưa được dùng                                | GV, QLHV (sửa: QLHV)           |
| F04    | Đề và đợt đánh giá   | Tạo, sửa đề nháp; phát hành; sao chép đề; tạo đợt đánh giá cho lớp                                                                   | GV, QLHV                       |
| F05    | Nhập kết quả         | Tải mẫu CSV theo đề; xem trước, kiểm tra và xác nhận nhập kết quả                                                                    | GV, QLHV                       |
| F06    | Phân tích, chẩn đoán | Tình hình tham gia (S4), phân tích lớp (S2), chẩn đoán cá nhân (S1), tiến độ học sinh (S5), hẹn ngày đánh giá theo dõi               | GV, QLHV                       |
| F07    | Can thiệp            | Tạo gói, chọn học liệu, giao học sinh, ghi ngày phát; liên kết đợt theo dõi và so sánh trước / sau (S3)                              | GV, QLHV                       |
| F08    | Phiếu ôn tập         | Soạn phiếu cá nhân / nhóm từ lý thuyết và câu luyện tập; gom nhóm theo đúng tập Subskill sai; in phiếu và đáp án; thư viện lý thuyết | GV, QLHV (sửa lý thuyết: QLHV) |
| F09    | Nhận xét và báo cáo  | Ghi nhận quá trình học tập; lập, sinh gợi ý AI, duyệt, hủy nhận xét; xem và in báo cáo học sinh (S10)                                | GV, QLHV                       |
| F10    | Bảng điều khiển      | Tổng hợp công việc theo vai trò: lớp, đợt đánh giá, can thiệp, lịch theo dõi                                                         | GV, QLHV                       |

## 3.5. Yêu cầu phi chức năng

| **Nhóm**         | **Yêu cầu**                                                                                                                                                                |
|------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Toàn vẹn dữ liệu | Nhập kết quả, giao gói can thiệp là giao dịch nguyên tử – không có kết quả ghi một phần; đề đã dùng không thể sửa; ràng buộc nghiệp vụ được cài đặt cả ở tầng CSDL         |
| Bảo mật          | Mật khẩu tối thiểu 12 ký tự; khóa 15 phút sau 5 lần sai; phiên 30 phút; cookie HttpOnly, Secure; chống CSRF; mọi trang yêu cầu đăng nhập; dữ liệu ngoài phạm vi trả về 404 |
| Riêng tư         | Không gửi họ tên, mã học sinh, thông tin phụ huynh cho dịch vụ AI; ghi chú nội bộ của giáo viên không xuất hiện trong báo cáo                                              |
| Đồng thời        | Phát hiện xung đột khi hai người cùng sửa (RowVersion, dấu vân tay dữ liệu, khóa dòng)                                                                                     |
| Khả dụng         | Giao diện tiếng Việt, giao diện dành cho máy tính; báo cáo và phiếu in được khổ A4                                                                                         |
| Hiệu năng        | Phục vụ quy mô một trường; file nhập tối đa 1 MB, 200 học sinh, 200 câu                                                                                                    |
| Bảo trì          | Tách tầng Controller – Service – Data; quy tắc nghiệp vụ nằm trong Service; tính năng mới có cờ bật / tắt trong cấu hình                                                   |

## 3.6. Quy tắc nghiệp vụ

| **Mã** | **Quy tắc**                                                                                                                                                                    |
|--------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| BR01   | Một học sinh tại một thời điểm chỉ thuộc một lớp; khoảng thời gian tính theo \[StartDate, EndDate), EndDate trống nghĩa là còn hiệu lực                                        |
| BR02   | Giáo viên chỉ xem lớp có phân công còn hiệu lực hôm nay; chỉ ghi dữ liệu đợt đánh giá khi được phân công cả hôm nay và tại ngày đánh giá                                       |
| BR03   | Khi đưa câu hỏi vào đề, hệ thống lưu bản chụp nội dung, đáp án và Subskill; mọi phân tích dùng Subskill trong bản chụp                                                         |
| BR04   | Chỉ đề ở trạng thái Draft và chưa được dùng mới được sửa hoặc phát hành; câu hỏi đưa vào đề kiểm tra chỉ dùng loại ForTest, câu loại ForIntervention chỉ dùng cho phiếu ôn tập |
| BR05   | Chỉ nhập kết quả cho đợt đánh giá đang mở (Open); học sinh phải thuộc lớp tại ngày đánh giá và chưa có kết quả; ô trống được chấm là sai                                       |
| BR06   | Điểm = 10 × số câu đúng / tổng số câu, làm tròn 2 chữ số thập phân                                                                                                             |
| BR07   | Học sinh chưa có kết quả được hiển thị là Missing, không tính là 0 điểm                                                                                                        |
| BR08   | Mọi học sinh trong cùng gói can thiệp nhận cùng học liệu và cùng Subskill mục tiêu; giao gói lần đầu chuyển gói sang Published và khóa học liệu                                |
| BR09   | Mỗi học sinh trong một đợt đánh giá có tối đa một bản nhận xét nháp và một bản đã duyệt; nội dung AI phải được giáo viên duyệt                                                 |
| BR10   | So sánh trước / sau là quan sát, không kết luận nguyên nhân do can thiệp                                                                                                       |
