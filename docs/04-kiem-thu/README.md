# 04. Kiểm thử nghiệm thu

> Nội dung trích từ báo cáo đề án môn Hệ thống thông tin (bản nộp tháng 11/2026).

- Bảng theo dõi gốc: [bang-theo-doi-kiem-thu.xlsx](bang-theo-doi-kiem-thu.xlsx)
- Ảnh minh chứng: [minh-chung/](minh-chung/)

## 5.3. Kiểm thử

### 5.3.1. Phương pháp kiểm thử

Hệ thống được kiểm thử theo hình thức **kiểm thử nghiệm thu người dùng** (User Acceptance Testing), thực hiện thủ công theo phương pháp hộp đen: người kiểm thử đóng vai Quản lý học vụ và Giáo viên, thao tác trực tiếp trên giao diện bằng trình duyệt Google Chrome rồi đối chiếu kết quả hiển thị với kết quả mong đợi đã viết trước.

Mỗi ca kiểm thử được ghi vào bảng theo dõi gồm: mã ca, nhóm chức năng, các bước và dữ liệu thử, kết quả mong đợi, kết quả thực tế, trạng thái và ảnh chụp màn hình làm minh chứng. Một ca chỉ được đánh dấu Đạt khi kết quả thực tế khớp đủ với kết quả mong đợi.

Dữ liệu kiểm thử là dữ liệu mô phỏng, không dùng dữ liệu học sinh thật: lớp 9A1 năm học 2026 – 2027 gồm 30 học sinh, đề đánh giá 20 câu trắc nghiệm thuộc 5 kỹ năng, một đợt đánh giá gốc và một đợt đánh giá lại; các file CSV được chuẩn bị riêng cho từng tình huống (hợp lệ, có dòng lỗi, nhập lặp). Ứng dụng chạy trên máy phát triển với CSDL PostgreSQL cục bộ. Kết quả tổng hợp theo nhóm chức năng được trình bày ở bảng dưới đây.

| **Nhóm chức năng**           | **Mã ca**   | **Số ca** | **Đạt** |
|------------------------------|-------------|-----------|---------|
| Tài khoản và phân quyền      | TC01 – TC05 | 5         | 5       |
| Quản lý học sinh             | TC06        | 1         | 1       |
| Ngân hàng câu hỏi            | TC07 – TC10 | 4         | 4       |
| Soạn và phát hành đề         | TC11 – TC13 | 3         | 3       |
| Đợt đánh giá và nhập kết quả | TC14 – TC17 | 4         | 4       |
| Phân tích kết quả            | TC18 – TC19 | 2         | 2       |
| Can thiệp và phiếu ôn tập    | TC20 – TC22 | 3         | 3       |
| Theo dõi sau can thiệp       | TC23 – TC24 | 2         | 2       |
| Tổng cộng                    |             | 24        | 24      |

### 5.3.2. Các ca kiểm thử

Bảng dưới đây liệt kê các ca kiểm thử đã thực hiện cùng kết quả mong đợi.

| **Mã** | **Chức năng**     | **Tình huống kiểm thử**                                                       | **Kết quả mong đợi**                                                                                               | **Kết quả** |
|--------|-------------------|-------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------|-------------|
| TC01   | Tài khoản         | Quản lý học vụ tạo hồ sơ giáo viên và cấp tài khoản mới                       | Tạo được tài khoản và mật khẩu tạm; giáo viên xuất hiện trong danh sách, không trùng                               | Đạt         |
| TC02   | Tài khoản         | Liên kết vai trò Giáo viên vào chính tài khoản Quản lý học vụ                 | Tài khoản giữ vai trò Quản lý và có thêm vai trò Giáo viên; không tạo tài khoản thứ hai                            | Đạt         |
| TC03   | Tài khoản         | Giáo viên mới đăng nhập bằng mật khẩu tạm                                     | Hệ thống bắt đổi mật khẩu trước khi dùng chức năng; lần sau đăng nhập bằng mật khẩu mới                            | Đạt         |
| TC04   | Phân quyền        | Quản lý phân công lớp; giáo viên mở danh sách lớp của mình                    | Giáo viên thấy đúng lớp được phân công; phân công hiển thị đúng ở hồ sơ giáo viên                                  | Đạt         |
| TC05   | Phân quyền        | Giáo viên đăng nhập, mở trực tiếp URL của lớp không được phân công            | Hệ thống từ chối, hiện “Dữ liệu không tồn tại hoặc nằm ngoài phạm vi quyền”, không lộ dữ liệu lớp                  | Đạt         |
| TC06   | Học sinh          | Quản lý học vụ nhập danh sách học sinh hợp lệ từ CSV (30 học sinh lớp 9A1)    | Bản xem trước đúng số học sinh và ngày vào lớp; sau khi xác nhận, lớp có đủ học sinh, không trùng                  | Đạt         |
| TC07   | Ngân hàng câu hỏi | Tìm nội dung kiến thức qua cây Khối → Bài → Nhóm kiến thức → Kỹ năng          | Nhánh đang chọn hiển thị rõ; đổi bộ lọc không gây lỗi; danh sách câu hỏi cập nhật đúng                             | Đạt         |
| TC08   | Ngân hàng câu hỏi | Giáo viên tạo câu hỏi mới gắn với một kỹ năng (Subskill)                      | Câu hỏi được lưu đúng nội dung, bốn lựa chọn, đáp án đúng và kỹ năng                                               | Đạt         |
| TC09   | Ngân hàng câu hỏi | Nhập file CSV câu hỏi có một dòng sai đáp án                                  | Báo đúng dòng và nguyên nhân lỗi; không câu hỏi nào trong file được ghi                                            | Đạt         |
| TC10   | Ngân hàng câu hỏi | Giáo viên mở trực tiếp URL sửa một câu hỏi trong ngân hàng dùng chung         | Hệ thống chặn thao tác sửa; giáo viên chỉ được tạo bản sao để điều chỉnh                                           | Đạt         |
| TC11   | Soạn đề           | Soạn đề nháp, chọn câu hỏi từ nhiều kỹ năng, đổi bộ lọc giữa các lần chọn     | Câu đã chọn được giữ khi đổi bộ lọc; không bị thêm trùng                                                           | Đạt         |
| TC12   | Soạn đề           | Bỏ câu, thêm câu, đổi thứ tự rồi lưu và mở lại đề nháp                        | Danh sách câu và thứ tự được giữ đúng như trước khi lưu                                                            | Đạt         |
| TC13   | Soạn đề           | Phát hành đề rồi thử sửa lại nội dung                                         | Đề chuyển sang trạng thái đã phát hành; không sửa đè được, chỉ có thể sao chép thành đề nháp mới                   | Đạt         |
| TC14   | Đợt đánh giá      | Tạo đợt đánh giá từ lớp và đề đã phát hành                                    | Đợt đánh giá được lưu đúng lớp, đề, giáo viên, ngày; mở được chức năng nhập kết quả                                | Đạt         |
| TC15   | Nhập kết quả      | Nhập file kết quả có đáp án đúng, sai và ô bỏ trống                           | Chấm đúng; ô trống tính là sai; mẫu số là toàn bộ số câu của đề                                                    | Đạt         |
| TC16   | Nhập kết quả      | Nhập file kết quả có dòng lỗi                                                 | Báo lỗi theo từng dòng; không ghi kết quả của bất kỳ dòng nào                                                      | Đạt         |
| TC17   | Nhập kết quả      | Nhập lại file kết quả cho đợt đã có dữ liệu                                   | Từ chối các học sinh đã có kết quả; không tạo dữ liệu trùng                                                        | Đạt         |
| TC18   | Phân tích         | Xem phân tích lớp (S2), chẩn đoán cá nhân (S1) và tổng quan tham gia (S4)     | Số đúng / tổng theo kỹ năng, tỷ lệ sai và sĩ số có kết quả khớp với dữ liệu đã nhập                                | Đạt         |
| TC19   | Phân tích         | So sánh học sinh sai toàn bộ với học sinh chưa có kết quả                     | Học sinh sai toàn bộ hiển thị 0 / n, 0%; học sinh chưa có kết quả hiển thị “Chưa có kết quả”, không tính là 0%     | Đạt         |
| TC20   | Can thiệp         | Soạn phiếu bổ trợ theo nội dung sai, chọn học liệu và giao cho nhiều học sinh | Danh sách gợi ý đúng học sinh có câu sai; mọi học sinh được chọn nhận cùng toàn bộ nội dung phiếu                  | Đạt         |
| TC21   | Can thiệp         | Ghi ngày phát và ghi chú nội bộ riêng cho từng học sinh                       | Lưu đúng cho từng học sinh, không ảnh hưởng học sinh khác; ghi chú nội bộ không đưa vào báo cáo phụ huynh          | Đạt         |
| TC22   | Can thiệp         | Tự chia nhóm ôn tập theo nội dung sai của đợt đánh giá                        | Học sinh cùng tập nội dung sai vào một nhóm; nhóm không trùng học sinh; học sinh đúng toàn bộ không thuộc nhóm nào | Đạt         |
| TC23   | Theo dõi (S3)     | Mở trang theo dõi khi phiếu chưa có đợt đánh giá lại                          | Trang vẫn hiển thị kết quả gốc và trạng thái chưa có đánh giá lại; không tự tính mức tăng / giảm                   | Đạt         |
| TC24   | Theo dõi (S3)     | Liên kết đợt đánh giá lại và xem so sánh trước / sau                          | So sánh đúng từng học sinh × kỹ năng; mức thay đổi tính bằng điểm phần trăm                                        | Đạt         |

### 5.3.3. Minh chứng kiểm thử

Các hình dưới đây là ảnh chụp màn hình lúc thực hiện các ca kiểm thử ở mục 5.3.2.

![TC01 – Giáo viên mới xuất hiện trong danh sách sau khi cấp tài khoản](minh-chung/TC01.jpg)

*TC01 – Giáo viên mới xuất hiện trong danh sách sau khi cấp tài khoản*

![TC02 – Tài khoản quản lý kiêm giáo viên](minh-chung/TC02.jpg)

*TC02 – Tài khoản quản lý kiêm giáo viên*

![TC03 – Giáo viên bắt buộc đổi mật khẩu ở lần đăng nhập đầu](minh-chung/TC03.jpg)

*TC03 – Giáo viên bắt buộc đổi mật khẩu ở lần đăng nhập đầu*

![TC04 – Giáo viên thấy lớp được phân công](minh-chung/TC04_01.jpg)

*TC04 – Giáo viên thấy lớp được phân công*

![TC04 – Phân công lớp trên hồ sơ giáo viên](minh-chung/TC04_02.jpg)

*TC04 – Phân công lớp trên hồ sơ giáo viên*

![TC05 – Giáo viên mở lớp ngoài phạm vi phân công bị từ chối](minh-chung/TC05.jpg)

*TC05 – Giáo viên mở lớp ngoài phạm vi phân công bị từ chối*

![TC06 – Xem trước danh sách học sinh trước khi nhập](minh-chung/TC06_01.jpg)

*TC06 – Xem trước danh sách học sinh trước khi nhập*

![TC06 – Chi tiết lớp 9A1 sau khi nhập danh sách](minh-chung/TC06_02.jpg)

*TC06 – Chi tiết lớp 9A1 sau khi nhập danh sách*

![TC07 – Cây kiến thức dùng để lọc câu hỏi](minh-chung/TC08.jpg)

*TC07 – Cây kiến thức dùng để lọc câu hỏi*

![TC08 – Câu hỏi vừa tạo trong ngân hàng câu hỏi](minh-chung/TC09.jpg)

*TC08 – Câu hỏi vừa tạo trong ngân hàng câu hỏi*

![TC09 – Nhập CSV câu hỏi bị từ chối do một dòng lỗi](minh-chung/TC10.jpg)

*TC09 – Nhập CSV câu hỏi bị từ chối do một dòng lỗi*

![TC10 – Giáo viên không sửa được câu hỏi dùng chung](minh-chung/TC11.jpg)

*TC10 – Giáo viên không sửa được câu hỏi dùng chung*

![TC11 – Soạn đề với câu hỏi từ nhiều kỹ năng](minh-chung/TC12.jpg)

*TC11 – Soạn đề với câu hỏi từ nhiều kỹ năng*

![TC12 – Chỉnh sửa và lưu lại đề nháp](minh-chung/TC13.jpg)

*TC12 – Chỉnh sửa và lưu lại đề nháp*

![TC13 – Đề đã phát hành không còn chỉnh sửa được](minh-chung/TC14.jpg)

*TC13 – Đề đã phát hành không còn chỉnh sửa được*

![TC14 – Tạo đợt đánh giá cho lớp 9A1](minh-chung/TC16.jpg)

*TC14 – Tạo đợt đánh giá cho lớp 9A1*

![TC15 – Xem trước kết quả chấm trước khi xác nhận](minh-chung/TC18.jpg)

*TC15 – Xem trước kết quả chấm trước khi xác nhận*

![TC16, TC17 – Nhập file lỗi hoặc nhập lặp đều bị từ chối toàn bộ](minh-chung/TC1920.jpg)

*TC16, TC17 – Nhập file lỗi hoặc nhập lặp đều bị từ chối toàn bộ*

![TC18 – Phân tích tỷ lệ sai theo nội dung kiến thức của lớp (S2)](minh-chung/TC21_01.jpg)

*TC18 – Phân tích tỷ lệ sai theo nội dung kiến thức của lớp (S2)*

![TC18 – Phân tích kết quả cá nhân (S1)](minh-chung/TC21_02.jpg)

*TC18 – Phân tích kết quả cá nhân (S1)*

![TC18 – Tổng quan tình hình ghi nhận kết quả (S4)](minh-chung/TC21_03.jpg)

*TC18 – Tổng quan tình hình ghi nhận kết quả (S4)*

![TC19 – Học sinh làm sai toàn bộ: 0/20, 0%](minh-chung/TC22_01.jpg)

*TC19 – Học sinh làm sai toàn bộ: 0/20, 0%*

![TC19 – Đợt chưa có kết quả hiển thị “Chưa có kết quả”](minh-chung/TC22_02.jpg)

*TC19 – Đợt chưa có kết quả hiển thị “Chưa có kết quả”*

![TC20 – Soạn phiếu bổ trợ và chọn học sinh nhận](minh-chung/TC23.jpg)

*TC20 – Soạn phiếu bổ trợ và chọn học sinh nhận*

![TC21 – Ngày phát và ghi chú riêng cho từng học sinh](minh-chung/TC24.jpg)

*TC21 – Ngày phát và ghi chú riêng cho từng học sinh*

![TC22 – Tự chia nhóm ôn tập theo nội dung sai](minh-chung/TC36.jpg)

*TC22 – Tự chia nhóm ôn tập theo nội dung sai*

![TC23 – Theo dõi khi chưa có đợt đánh giá lại](minh-chung/TC25.jpg)

*TC23 – Theo dõi khi chưa có đợt đánh giá lại*

![TC24 – So sánh kết quả trước và sau can thiệp](minh-chung/TC26.jpg)

*TC24 – So sánh kết quả trước và sau can thiệp*

### 5.3.4. Nhận xét

Cả 24 ca kiểm thử đều đạt. Các ca tập trung vào luồng nghiệp vụ chính và những quy tắc quan trọng của hệ thống: phân quyền theo phạm vi dữ liệu, nhập dữ liệu theo nguyên tắc “tất cả hoặc không”, đề đã phát hành không bị sửa đè, phân biệt học sinh chưa có kết quả với học sinh được 0%.

Do thời gian có hạn, kiểm thử mới dừng ở mức thủ công trên dữ liệu mô phỏng do em tự thực hiện; chưa kiểm thử hiệu năng, chưa thử nhiều người dùng thao tác đồng thời và chưa thử nghiệm với giáo viên, học sinh thật.

## Đối chiếu mã ca kiểm thử

Bảng theo dõi gốc ghi lại toàn bộ ca đã lập kế hoạch, kể cả các ca chưa chạy (TC17, TC30) và bị chặn (TC15 – chức năng xuất đề ra PDF chưa có). Báo cáo chỉ đưa vào các ca đã chạy, đạt và có ảnh minh chứng, đánh số lại liên tục:

| Mã trong báo cáo | Mã trong bảng theo dõi |
|---|---|
| TC01 | TC01 |
| TC02 | TC02 |
| TC03 | TC03 |
| TC04 | TC04 |
| TC05 | TC05 |
| TC06 | TC06 |
| TC07 | TC08 |
| TC08 | TC09 |
| TC09 | TC10 |
| TC10 | TC11 |
| TC11 | TC12 |
| TC12 | TC13 |
| TC13 | TC14 |
| TC14 | TC16 |
| TC15 | TC18 |
| TC16 | TC19 |
| TC17 | TC20 |
| TC18 | TC21 |
| TC19 | TC22 |
| TC20 | TC23 |
| TC21 | TC24 |
| TC22 | TC32 (ảnh TC36) |
| TC23 | TC25 |
| TC24 | TC26 |
