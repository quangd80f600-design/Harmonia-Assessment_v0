# Mục tiêu và phạm vi

> Nội dung trích từ báo cáo đề án môn Hệ thống thông tin (bản nộp tháng 11/2026).

## 1.2. Mục tiêu đề tài

### 1.2.1. Mục tiêu tổng quát

Xây dựng một hệ thống thông tin dạng ứng dụng web giúp giáo viên và quản lý học vụ quản lý toàn bộ vòng đời của một đợt đánh giá: từ ngân hàng câu hỏi, đề kiểm tra, nhập kết quả, phân tích, chẩn đoán theo kỹ năng, can thiệp và theo dõi, đến báo cáo gửi phụ huynh. Kết quả phân tích phải **giải thích được**: mọi con số đều truy ngược được về câu hỏi, câu trả lời và đề kiểm tra cụ thể.

### 1.2.2. Mục tiêu cụ thể

- Phân tích nghiệp vụ đánh giá – chẩn đoán – can thiệp tại trường trung học cơ sở và mô hình hóa bằng UML 2.5.1.

- Thiết kế cơ sở dữ liệu quan hệ trên PostgreSQL với đầy đủ ràng buộc toàn vẹn, bảo đảm dữ liệu lịch sử không bị thay đổi khi ngân hàng câu hỏi được chỉnh sửa.

- Xây dựng các chức năng chính: quản lý lớp, học sinh, giáo viên và phân công; ngân hàng câu hỏi theo taxonomy; tạo và phát hành đề; nhập kết quả từ file CSV; phân tích kết quả lớp và chẩn đoán cá nhân theo Subskill; quản lý gói can thiệp, phiếu ôn tập và so sánh trước / sau; ghi nhận quá trình học tập; lập nhận xét và báo cáo học sinh.

- Phân quyền theo vai trò và theo phạm vi dữ liệu: giáo viên chỉ thao tác trên lớp đang được phân công.

- Tích hợp có kiểm soát trí tuệ nhân tạo (Gemini API) để gợi ý bản nháp nhận xét; giáo viên luôn là người duyệt cuối cùng.

- Kiểm thử các nghiệp vụ trọng yếu.

## 1.3. Đối tượng và phạm vi nghiên cứu

### 1.3.1. Đối tượng nghiên cứu

- Quy trình đánh giá định kỳ, phân tích kết quả và can thiệp sau đánh giá trong dạy học môn Tiếng Anh cấp trung học cơ sở.

- Phương pháp phân tích, thiết kế hệ thống thông tin hướng đối tượng với UML.

- Nền tảng ASP.NET Core MVC, Entity Framework Core, hệ quản trị cơ sở dữ liệu PostgreSQL và dịch vụ Gemini API.

### 1.3.2. Phạm vi đề tài

**Người dùng hệ thống** gồm hai vai trò: **Giáo viên** và **Quản lý học vụ** (tổ trưởng chuyên môn / phụ trách học vụ). Học sinh và phụ huynh là **người nhận báo cáo**, không có tài khoản trong phiên bản này.

**Nội dung chuyên môn:** phiên bản demo dùng taxonomy Tiếng Anh lớp 9 theo chương trình Global Success \[10\] với phân cấp Khối → Chương (Unit) → Mạch kiến thức → Subskill. Câu hỏi là trắc nghiệm bốn lựa chọn A–D, một đáp án đúng.

**Ngoài phạm vi:** tổ chức thi trực tuyến, chấm bài bằng AI, học tập thích ứng, hệ thống quản lý học tập (LMS) đầy đủ, quản lý học phí – tuyển sinh, cổng thông tin cho phụ huynh và học sinh. Bài kiểm tra được tổ chức trên giấy, kết quả được nhập vào hệ thống bằng file CSV.

**Dữ liệu:** toàn bộ dữ liệu dùng để phát triển và kiểm thử là dữ liệu mô phỏng (lớp 9A1 gồm 30 học sinh giả lập, đề đánh giá 20 câu trắc nghiệm), không phải dữ liệu học sinh thật.

## 1.4. Phương pháp thực hiện

- **Khảo sát và phân tích tài liệu:** tìm hiểu quy trình kiểm tra, đánh giá, chương trình Tiếng Anh 9 Global Success để xây dựng taxonomy kỹ năng.

- **Phân tích, thiết kế hướng đối tượng:** mô hình hóa yêu cầu bằng sơ đồ use case, đặc tả use case theo mẫu của Alistair Cockburn; mô hình hóa hành vi bằng activity, sequence và state machine; mô hình hóa cấu trúc bằng class diagram. Ký pháp tuân theo đặc tả OMG UML 2.5.1.

- **Thiết kế cơ sở dữ liệu theo hướng Database-First:** thiết kế lược đồ và ràng buộc trực tiếp trên PostgreSQL, sau đó sinh lớp thực thể C# bằng EF Core.

- **Phát triển lặp theo kịch bản nghiệp vụ:** hệ thống được chia thành các kịch bản S1–S10; mỗi kịch bản được cài đặt và kiểm tra xong rồi mới chuyển sang kịch bản tiếp theo.

- **Kiểm thử nghiệm thu thủ công**: viết ca kiểm thử gồm các bước và kết quả mong đợi, thao tác trên trình duyệt Chrome với dữ liệu mô phỏng, lưu ảnh chụp màn hình làm minh chứng.

## Hạn chế hiện tại

- Bảng Response chỉ lưu đúng / sai, **chưa lưu đáp án học sinh đã chọn**, nên chưa phân tích được phương án nhiễu.

- Đợt đánh giá chỉ được tạo ở trạng thái Open; **chưa có chức năng đóng hoặc hủy đợt đánh giá**, chưa có chức năng lưu trữ đề và gói can thiệp cũ.

- Chưa có **nhật ký thao tác giáo viên** riêng; chỉ có các cột ghi vết người tạo / sửa, và phần lớn chưa có khóa ngoại tới bảng tài khoản.

- Chỉ nhập dữ liệu bằng CSV, chưa hỗ trợ Excel hay xuất trực tiếp từ các nền tảng chấm bài; chỉ hỗ trợ câu trắc nghiệm bốn lựa chọn.

- Taxonomy mới xây dựng cho Tiếng Anh lớp 9; chưa có phân tích tiến bộ theo lớp qua nhiều đợt (S6).

- Hệ thống mới chạy trên máy phát triển và được kiểm thử trên dữ liệu mô phỏng, chưa đưa lên máy chủ và chưa thử nghiệm với giáo viên, học sinh thật.

## Hướng phát triển

- Bổ sung cột đáp án đã chọn cho Response và phân tích phương án nhiễu.

- Hoàn thiện vòng đời trạng thái đợt đánh giá, đề, gói can thiệp; bổ sung nhật ký thao tác.

- Hỗ trợ nhập Excel và định dạng xuất của các công cụ chấm phiếu phổ biến.

- Phân tích tiến bộ theo lớp (S6) và mở rộng taxonomy cho các khối, môn khác.
