# Kế hoạch và tiến độ theo kịch bản nghiệp vụ

Dự án được chia theo **kịch bản nghiệp vụ S1–S10** thay vì theo màn hình. Mỗi kịch bản đi qua bốn trạng thái:

1. **Đã đặc tả** – có yêu cầu, quy tắc nghiệp vụ, use case / sơ đồ.
2. **Đã cài đặt** – có mã nguồn chạy được.
3. **Đã kiểm thử** – đã chạy ca kiểm thử nghiệm thu.
4. **Đã nghiệm thu** – ca kiểm thử đạt và có ảnh minh chứng.

Việc tách "đã viết mã", "đã kiểm thử" và "đã nghiệm thu" giúp không báo cáo quá mức thực tế (xem [bài học kinh nghiệm](bai-hoc-kinh-nghiem.md)).

## Phân rã công việc

Công việc được chia thành sáu nhóm, mỗi nhóm gắn với các kịch bản / chức năng tương ứng:

| Nhóm công việc | Nội dung | Kịch bản / chức năng |
|---|---|---|
| 1. Nền tảng dữ liệu | Lược đồ CSDL, taxonomy Tiếng Anh 9, ngân hàng câu hỏi, đề có bản chụp | S7, F03, F04 |
| 2. Chẩn đoán | Nhập kết quả CSV, phân tích cá nhân, phân tích lớp, tình hình tham gia | S8, S1, S2, S4 |
| 3. Tài khoản và phân quyền | Identity, hai vai trò, phân quyền theo phạm vi dữ liệu | F01, F02 |
| 4. Can thiệp | Gói can thiệp, phiếu ôn tập cá nhân / nhóm, theo dõi trước / sau | S9, S3, F08 |
| 5. Báo cáo | Tiến độ học sinh, ghi nhận quá trình, nhận xét có gợi ý AI, báo cáo học sinh | S5, S10, F09 |
| 6. Nghiệm thu | Viết ca kiểm thử, kiểm thử thủ công, chụp minh chứng, hoàn thiện tài liệu | toàn hệ thống |

## Trạng thái hiện tại

| Kịch bản | Đặc tả | Cài đặt | Kiểm thử nghiệm thu | Ghi chú |
|---|:-:|:-:|:-:|---|
| S1 – Chẩn đoán cá nhân | ✅ | ✅ | ✅ TC18, TC19 | |
| S2 – Phân tích lớp | ✅ | ✅ | ✅ TC18 | |
| S3 – Theo dõi sau can thiệp | ✅ | ✅ | ✅ TC23, TC24 | |
| S4 – Tình hình tham gia | ✅ | ✅ | ✅ TC18, TC19 | |
| S5 – Tiến độ học sinh | ✅ | ✅ | ⬜ | chưa có ca nghiệm thu riêng |
| S6 – Tiến bộ chung của lớp | ✅ | ⏸ | – | **hoãn** ([QĐ-11](nhat-ky-quyet-dinh.md#qđ-11--hoãn-kịch-bản-s6)) |
| S7 – Snapshot đề | ✅ | ✅ | ✅ TC11–TC13 | |
| S8 – Nhập kết quả | ✅ | ✅ | ✅ TC15–TC17 | |
| S9 – Quản lý can thiệp | ✅ | ✅ | ✅ TC20–TC22 | |
| S10 – Báo cáo học sinh | ✅ | ✅ | ⬜ | ca in báo cáo PDF chưa chạy |
| Tài khoản, phân quyền (F01–F02) | ✅ | ✅ | ✅ TC01–TC06 | |
| Ngân hàng câu hỏi (F03) | ✅ | ✅ | ✅ TC07–TC10 | |
| Tạo đợt đánh giá (F04) | ✅ | ✅ | ✅ TC14 | |

Ký hiệu: ✅ hoàn thành · ⬜ chưa thực hiện · ⏸ tạm hoãn.

## Việc còn lại

- Bổ sung ca nghiệm thu cho tiến độ học sinh (S5).
- Bổ sung ca nghiệm thu in / lưu PDF báo cáo học sinh (S10).
- Chức năng xuất đề kiểm tra ra PDF để in cho học sinh (ca TC15 trong bảng theo dõi gốc đang bị chặn).
- Phân tích tiến bộ chung của lớp qua nhiều đợt (S6 – tạm hoãn).
- Lưu lựa chọn A/B/C/D của học sinh để phân tích phương án nhiễu.
- Hoàn thiện vòng đời trạng thái đợt đánh giá, đề và gói can thiệp; nhật ký thao tác của giáo viên.

Các mục khác ở phần [hướng phát triển](pham-vi.md#hướng-phát-triển).
