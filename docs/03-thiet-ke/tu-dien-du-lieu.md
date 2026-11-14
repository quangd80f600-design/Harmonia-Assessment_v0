# Từ điển dữ liệu

> Nội dung trích từ báo cáo đề án môn Hệ thống thông tin (bản nộp tháng 11/2026).

Phụ lục trình bày chi tiết 34 bảng của cơ sở dữ liệu. Quy ước: PK – khóa chính; FK – khóa ngoại; UK – duy nhất trên một cột; cột Null đánh dấu x nghĩa là cho phép để trống. Ràng buộc trên nhiều cột được liệt kê dưới mỗi bảng. Các cột CreatedByUser_ID / UpdatedByUser_ID lưu mã tài khoản để ghi vết nhưng chỉ có khóa ngoại thật ở LearningObservation và StudentReportComment.

## Học vụ

### A.1. Class

**Mô tả:** Lớp học theo năm học.

| **STT** | **Tên cột**      | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                          |
|---------|------------------|------------------|----------|----------|------------------------------------|
| 1       | Class_ID         | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng) |
| 2       | ClassCode        | varchar(50)      |          |          | Mã lớp (duy nhất trong năm học)    |
| 3       | ClassName        | varchar(150)     |          |          | Tên lớp                            |
| 4       | Grade            | smallint         |          |          | Khối lớp (6 – 12)                  |
| 5       | AcademicYear     | varchar(9)       |          |          | Năm học, dạng YYYY-YYYY            |
| 6       | Status           | varchar(20)      |          |          | Trạng thái: Active, Inactive       |
| 7       | CreatedAt        | timestamptz      |          |          | Thời điểm tạo                      |
| 8       | UpdatedAt        | timestamptz      | x        |          | Thời điểm cập nhật gần nhất        |
| 9       | CreatedByUser_ID | text             |          |          | Tài khoản tạo bản ghi              |
| 10      | UpdatedByUser_ID | text             | x        |          | Tài khoản cập nhật gần nhất        |

**Ràng buộc:**

- Khóa chính: (Class_ID)

- Duy nhất (ClassCode, AcademicYear)

- Kiểm tra: Grade ∈ \[6, 12\]; Status ∈ {Active, Inactive}

- Kiểm tra: AcademicYear dạng YYYY-YYYY, năm sau = năm đầu + 1

### A.2. Student

**Mô tả:** Hồ sơ học sinh và thông tin người giám hộ chính.

| **STT** | **Tên cột**                | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                          |
|---------|----------------------------|------------------|----------|----------|------------------------------------|
| 1       | Student_ID                 | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng) |
| 2       | StudentCode                | varchar(50)      |          | UK       | Mã học sinh                        |
| 3       | FullName                   | varchar(150)     |          |          | Họ và tên                          |
| 4       | DateOfBirth                | date             |          |          | Ngày sinh                          |
| 5       | CreatedAt                  | timestamptz      |          |          | Thời điểm tạo                      |
| 6       | UpdatedAt                  | timestamptz      | x        |          | Thời điểm cập nhật gần nhất        |
| 7       | Gender                     | varchar(20)      |          |          | Giới tính: Male, Female            |
| 8       | Status                     | varchar(20)      |          |          | Trạng thái: Active, Inactive       |
| 9       | PrimaryGuardianName        | varchar(150)     | x        |          | Họ tên người giám hộ chính         |
| 10      | PrimaryGuardianPhonenumber | varchar(20)      | x        |          | Số điện thoại người giám hộ chính  |
| 11      | CreatedByUser_ID           | text             |          |          | Tài khoản tạo bản ghi              |
| 12      | UpdatedByUser_ID           | text             | x        |          | Tài khoản cập nhật gần nhất        |

**Ràng buộc:**

- Khóa chính: (Student_ID)

- Kiểm tra: Gender ∈ {Male, Female}; Status ∈ {Active, Inactive}

### A.3. ClassMembership

**Mô tả:** Thời gian học sinh thuộc một lớp; cho phép chuyển lớp mà vẫn giữ lịch sử.

| **STT** | **Tên cột**        | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                   |
|---------|--------------------|------------------|----------|----------|---------------------------------------------|
| 1       | ClassMembership_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)          |
| 2       | Student_ID         | bigint           |          | FK       | Tham chiếu học sinh → Student.Student_ID    |
| 3       | Class_ID           | bigint           |          | FK       | Tham chiếu lớp → Class.Class_ID             |
| 4       | StartDate          | date             |          |          | Ngày bắt đầu thuộc lớp (tính cả ngày này)   |
| 5       | EndDate            | date             | x        |          | Ngày kết thúc (không tính); NULL = đang học |
| 6       | CreatedAt          | timestamptz      |          |          | Thời điểm tạo                               |
| 7       | UpdatedAt          | timestamptz      | x        |          | Thời điểm cập nhật gần nhất                 |
| 8       | CreatedByUser_ID   | text             |          |          | Tài khoản tạo bản ghi                       |
| 9       | UpdatedByUser_ID   | text             | x        |          | Tài khoản cập nhật gần nhất                 |

**Ràng buộc:**

- Khóa chính: (ClassMembership_ID)

- Khóa ngoại: Class_ID → Class.Class_ID (ON DELETE NO ACTION)

- Khóa ngoại: Student_ID → Student.Student_ID (ON DELETE NO ACTION)

- Kiểm tra: EndDate \> StartDate (nếu có)

- Loại trừ (EXCLUDE): một Student không có hai khoảng \[StartDate, EndDate) chồng lấn

### A.4. Teacher

**Mô tả:** Hồ sơ giáo viên.

| **STT** | **Tên cột**      | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                          |
|---------|------------------|------------------|----------|----------|------------------------------------|
| 1       | Teacher_ID       | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng) |
| 2       | TeacherCode      | varchar(50)      |          | UK       | Mã giáo viên                       |
| 3       | FullName         | varchar(150)     |          |          | Họ và tên                          |
| 4       | Email            | varchar(150)     |          | UK       | Email (duy nhất)                   |
| 5       | PhoneNumber      | varchar(20)      |          | UK       | Số điện thoại (duy nhất)           |
| 6       | Status           | varchar(20)      |          |          | Trạng thái: Active, Inactive       |
| 7       | CreatedAt        | timestamptz      |          |          | Thời điểm tạo                      |
| 8       | UpdatedAt        | timestamptz      | x        |          | Thời điểm cập nhật gần nhất        |
| 9       | CreatedByUser_ID | text             |          |          | Tài khoản tạo bản ghi              |
| 10      | UpdatedByUser_ID | text             | x        |          | Tài khoản cập nhật gần nhất        |

**Ràng buộc:**

- Khóa chính: (Teacher_ID)

- Kiểm tra: Status ∈ {Active, Inactive}

### A.5. TeacherClassAssignment

**Mô tả:** Phân công giáo viên phụ trách lớp theo khoảng thời gian; là căn cứ phân quyền theo lớp.

| **STT** | **Tên cột**               | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                 |
|---------|---------------------------|------------------|----------|----------|-----------------------------------------------------------|
| 1       | TeacherClassAssignment_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                        |
| 2       | Class_ID                  | bigint           |          | FK       | Tham chiếu lớp → Class.Class_ID                           |
| 3       | Teacher_ID                | bigint           |          | FK       | Tham chiếu giáo viên → Teacher.Teacher_ID                 |
| 4       | StartDate                 | date             |          |          | Ngày bắt đầu phân công (tính cả ngày này)                 |
| 5       | EndDate                   | date             | x        |          | Ngày kết thúc phân công (không tính); NULL = còn hiệu lực |
| 6       | CreatedAt                 | timestamptz      |          |          | Thời điểm tạo                                             |
| 7       | UpdatedAt                 | timestamptz      | x        |          | Thời điểm cập nhật gần nhất                               |
| 8       | CreatedByUser_ID          | text             |          |          | Tài khoản tạo bản ghi                                     |
| 9       | UpdatedByUser_ID          | text             | x        |          | Tài khoản cập nhật gần nhất                               |

**Ràng buộc:**

- Khóa chính: (TeacherClassAssignment_ID)

- Khóa ngoại: Class_ID → Class.Class_ID (ON DELETE NO ACTION)

- Khóa ngoại: Teacher_ID → Teacher.Teacher_ID (ON DELETE NO ACTION)

- Kiểm tra: EndDate \> StartDate (nếu có)

- Loại trừ (EXCLUDE): cùng Teacher + Class không có hai khoảng \[StartDate, EndDate) chồng lấn

## Xác thực và phân quyền (schema auth)

### A.6. AspNetUsers

**Mô tả:** Tài khoản đăng nhập (ASP.NET Core Identity), bổ sung cờ hoạt động và buộc đổi mật khẩu.

| **STT** | **Tên cột**          | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                         |
|---------|----------------------|------------------|----------|----------|-------------------------------------------------------------------|
| 1       | Id                   | text             |          | PK       | Mã tài khoản (GUID)                                               |
| 2       | IsActive             | boolean          |          |          | Tài khoản đang hoạt động                                          |
| 3       | MustChangePassword   | boolean          |          |          | Buộc đổi mật khẩu ở lần đăng nhập kế tiếp                         |
| 4       | UserName             | varchar(256)     | x        |          | Tên đăng nhập                                                     |
| 5       | NormalizedUserName   | varchar(256)     | x        | UK       | Tên đăng nhập chuẩn hóa                                           |
| 6       | Email                | varchar(256)     | x        |          | Email tài khoản                                                   |
| 7       | NormalizedEmail      | varchar(256)     | x        |          | Email chuẩn hóa để tìm kiếm                                       |
| 8       | EmailConfirmed       | boolean          |          |          | Email đã xác nhận                                                 |
| 9       | PasswordHash         | text             | x        |          | Mật khẩu đã băm                                                   |
| 10      | SecurityStamp        | text             | x        |          | Dấu bảo mật; đổi khi đăng xuất / đổi mật khẩu để vô hiệu phiên cũ |
| 11      | ConcurrencyStamp     | text             | x        |          | Dấu kiểm soát cập nhật đồng thời                                  |
| 12      | PhoneNumber          | text             | x        |          | Số điện thoại tài khoản                                           |
| 13      | PhoneNumberConfirmed | boolean          |          |          | Số điện thoại đã xác nhận                                         |
| 14      | TwoFactorEnabled     | boolean          |          |          | Bật xác thực hai lớp                                              |
| 15      | LockoutEnd           | timestamptz      | x        |          | Thời điểm hết khóa tài khoản                                      |
| 16      | LockoutEnabled       | boolean          |          |          | Cho phép khóa tài khoản khi sai mật khẩu                          |
| 17      | AccessFailedCount    | integer          |          |          | Số lần đăng nhập sai liên tiếp                                    |

**Ràng buộc:**

- Khóa chính: (Id)

### A.7. AspNetRoles

**Mô tả:** Danh mục vai trò: Teacher, AcademicManager.

| **STT** | **Tên cột**      | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                        |
|---------|------------------|------------------|----------|----------|----------------------------------|
| 1       | Id               | text             |          | PK       | Mã vai trò                       |
| 2       | Name             | varchar(256)     | x        |          | Tên vai trò                      |
| 3       | NormalizedName   | varchar(256)     | x        | UK       | Tên vai trò chuẩn hóa            |
| 4       | ConcurrencyStamp | text             | x        |          | Dấu kiểm soát cập nhật đồng thời |

**Ràng buộc:**

- Khóa chính: (Id)

### A.8. AspNetUserRoles

**Mô tả:** Gán vai trò cho tài khoản (một tài khoản có một hoặc hai vai trò).

| **STT** | **Tên cột** | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                  |
|---------|-------------|------------------|----------|----------|----------------------------|
| 1       | UserId      | text             |          | PK,FK    | Tài khoản → AspNetUsers.Id |
| 2       | RoleId      | text             |          | PK,FK    | Vai trò → AspNetRoles.Id   |

**Ràng buộc:**

- Khóa chính: (UserId, RoleId)

- Khóa ngoại: RoleId → AspNetRoles.Id (ON DELETE CASCADE)

- Khóa ngoại: UserId → AspNetUsers.Id (ON DELETE CASCADE)

- PK (UserId, RoleId)

### A.9. TeacherUserLinks

**Mô tả:** Liên kết một tài khoản với một hồ sơ giáo viên (quan hệ 1 – 1).

| **STT** | **Tên cột** | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                       |
|---------|-------------|------------------|----------|----------|-------------------------------------------------|
| 1       | User_ID     | text             |          | PK,FK    | Tài khoản đăng nhập → AspNetUsers.Id            |
| 2       | Teacher_ID  | bigint           |          | FK,UK    | Hồ sơ giáo viên (duy nhất) → Teacher.Teacher_ID |

**Ràng buộc:**

- Khóa chính: (User_ID)

- Khóa ngoại: User_ID → AspNetUsers.Id (ON DELETE RESTRICT)

- Khóa ngoại: Teacher_ID → Teacher.Teacher_ID (ON DELETE RESTRICT)

- UK Teacher_ID: một hồ sơ GV liên kết tối đa một tài khoản

### A.10. AspNetUserClaims

**Mô tả:** Claim bổ sung của tài khoản (bảng chuẩn của Identity).

| **STT** | **Tên cột** | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                  |
|---------|-------------|------------------|----------|----------|----------------------------|
| 1       | Id          | integer          |          | PK       | Mã claim                   |
| 2       | UserId      | text             |          | FK       | Tài khoản → AspNetUsers.Id |
| 3       | ClaimType   | text             | x        |          | Loại claim                 |
| 4       | ClaimValue  | text             | x        |          | Giá trị claim              |

**Ràng buộc:**

- Khóa chính: (Id)

- Khóa ngoại: UserId → AspNetUsers.Id (ON DELETE CASCADE)

### A.11. AspNetRoleClaims

**Mô tả:** Claim gắn với vai trò (bảng chuẩn của Identity).

| **STT** | **Tên cột** | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                |
|---------|-------------|------------------|----------|----------|--------------------------|
| 1       | Id          | integer          |          | PK       | Mã claim                 |
| 2       | RoleId      | text             |          | FK       | Vai trò → AspNetRoles.Id |
| 3       | ClaimType   | text             | x        |          | Loại claim               |
| 4       | ClaimValue  | text             | x        |          | Giá trị claim            |

**Ràng buộc:**

- Khóa chính: (Id)

- Khóa ngoại: RoleId → AspNetRoles.Id (ON DELETE CASCADE)

### A.12. AspNetUserLogins

**Mô tả:** Đăng nhập qua nhà cung cấp ngoài (bảng chuẩn của Identity, chưa sử dụng).

| **STT** | **Tên cột**         | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                        |
|---------|---------------------|------------------|----------|----------|----------------------------------|
| 1       | LoginProvider       | text             |          | PK       | Nhà cung cấp đăng nhập           |
| 2       | ProviderKey         | text             |          | PK       | Khóa người dùng tại nhà cung cấp |
| 3       | ProviderDisplayName | text             | x        |          | Tên hiển thị của nhà cung cấp    |
| 4       | UserId              | text             |          | FK       | Tài khoản → AspNetUsers.Id       |

**Ràng buộc:**

- Khóa chính: (LoginProvider, ProviderKey)

- Khóa ngoại: UserId → AspNetUsers.Id (ON DELETE CASCADE)

### A.13. AspNetUserTokens

**Mô tả:** Token của tài khoản (bảng chuẩn của Identity).

| **STT** | **Tên cột**   | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                  |
|---------|---------------|------------------|----------|----------|----------------------------|
| 1       | UserId        | text             |          | PK,FK    | Tài khoản → AspNetUsers.Id |
| 2       | LoginProvider | text             |          | PK       | Nhà cung cấp đăng nhập     |
| 3       | Name          | text             |          | PK       | Tên token                  |
| 4       | Value         | text             | x        |          | Giá trị token              |

**Ràng buộc:**

- Khóa chính: (UserId, LoginProvider, Name)

- Khóa ngoại: UserId → AspNetUsers.Id (ON DELETE CASCADE)

### A.14. \_\_EFMigrationsHistory

**Mô tả:** Lịch sử migration của schema auth do Entity Framework Core quản lý.

| **STT** | **Tên cột**    | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                 |
|---------|----------------|------------------|----------|----------|---------------------------|
| 1       | MigrationId    | varchar(150)     |          | PK       | Mã định danh (khóa chính) |
| 2       | ProductVersion | varchar(32)      |          |          | Phiên bản EF Core         |

**Ràng buộc:**

- Khóa chính: (MigrationId)

## Taxonomy và ngân hàng đề

### A.15. CurriculumUnit

**Mô tả:** Chương / đơn vị bài học theo khối; khối lớp được lưu ở cột Grade (không có bảng Grade riêng).

| **STT** | **Tên cột**       | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                          |
|---------|-------------------|------------------|----------|----------|------------------------------------|
| 1       | CurriculumUnit_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng) |
| 2       | Grade             | smallint         |          |          | Khối lớp (6 – 12)                  |
| 3       | UnitNumber        | smallint         |          |          | Số thứ tự chương trong khối        |
| 4       | UnitName          | text             |          |          | Tên chương                         |

**Ràng buộc:**

- Khóa chính: (CurriculumUnit_ID)

- Duy nhất (Grade, UnitNumber)

- Kiểm tra: Grade ∈ \[6, 12\]; UnitNumber \> 0

### A.16. KnowledgeArea

**Mô tả:** Mạch kiến thức thuộc một chương.

| **STT** | **Tên cột**       | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                            |
|---------|-------------------|------------------|----------|----------|------------------------------------------------------|
| 1       | KnowledgeArea_ID  | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                   |
| 2       | KnowledgeAreaCode | varchar(50)      |          | UK       | Mã mạch kiến thức                                    |
| 3       | KnowledgeAreaName | varchar(255)     |          |          | Tên mạch kiến thức                                   |
| 4       | Description       | text             | x        |          | Mô tả mạch kiến thức                                 |
| 5       | CreatedAt         | timestamptz      |          |          | Thời điểm tạo                                        |
| 6       | UpdatedAt         | timestamptz      | x        |          | Thời điểm cập nhật gần nhất                          |
| 7       | Status            | varchar(20)      |          |          | Trạng thái: Active, Inactive                         |
| 8       | CurriculumUnit_ID | bigint           |          | FK       | Tham chiếu chương → CurriculumUnit.CurriculumUnit_ID |
| 9       | SortOrder         | smallint         |          |          | Thứ tự sắp xếp                                       |

**Ràng buộc:**

- Khóa chính: (KnowledgeArea_ID)

- Khóa ngoại: CurriculumUnit_ID → CurriculumUnit.CurriculumUnit_ID (ON DELETE NO ACTION)

- Kiểm tra: Status ∈ {Active, Inactive}; SortOrder \> 0

### A.17. Subskill

**Mô tả:** Kỹ năng con – đơn vị nhỏ nhất dùng để chẩn đoán kết quả.

| **STT** | **Tên cột**      | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                  |
|---------|------------------|------------------|----------|----------|------------------------------------------------------------|
| 1       | Subskill_ID      | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                         |
| 2       | KnowledgeArea_ID | bigint           |          | FK       | Tham chiếu mạch kiến thức → KnowledgeArea.KnowledgeArea_ID |
| 3       | SubskillCode     | varchar(50)      |          | UK       | Mã Subskill                                                |
| 4       | SubskillName     | varchar(255)     |          |          | Tên Subskill                                               |
| 5       | Description      | text             |          |          | Mô tả Subskill                                             |
| 6       | SortOrder        | smallint         |          |          | Thứ tự sắp xếp                                             |
| 7       | Status           | varchar(20)      |          |          | Trạng thái: Active, Inactive                               |

**Ràng buộc:**

- Khóa chính: (Subskill_ID)

- Khóa ngoại: KnowledgeArea_ID → KnowledgeArea.KnowledgeArea_ID (ON DELETE NO ACTION)

- Kiểm tra: Status ∈ {Active, Inactive}; SortOrder \> 0

### A.18. Question

**Mô tả:** Câu hỏi trắc nghiệm 4 lựa chọn trong ngân hàng, gắn với một Subskill.

| **STT** | **Tên cột**     | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                 |
|---------|-----------------|------------------|----------|----------|-----------------------------------------------------------|
| 1       | Question_ID     | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                        |
| 2       | Subskill_ID     | bigint           |          | FK       | Tham chiếu Subskill                                       |
| 3       | QuestionCode    | varchar(50)      |          | UK       | Mã câu hỏi                                                |
| 4       | QuestionText    | text             |          |          | Nội dung câu hỏi                                          |
| 5       | OptionA         | text             |          |          | Lựa chọn A                                                |
| 6       | OptionB         | text             |          |          | Lựa chọn B                                                |
| 7       | OptionC         | text             |          |          | Lựa chọn C                                                |
| 8       | OptionD         | text             |          |          | Lựa chọn D                                                |
| 9       | AnswerKey       | varchar(1)       |          |          | Đáp án đúng (A, B, C, D)                                  |
| 10      | DifficultyLevel | smallint         |          |          | Mức độ khó (1 – 3)                                        |
| 11      | Explanation     | text             |          |          | Lời giải thích đáp án                                     |
| 12      | Status          | varchar(20)      |          |          | Trạng thái: Active, Inactive                              |
| 13      | UsagePurpose    | varchar(20)      |          |          | Mục đích: ForTest (đánh giá), ForIntervention (luyện tập) |

**Ràng buộc:**

- Khóa chính: (Question_ID)

- Khóa ngoại: Subskill_ID → Subskill.Subskill_ID (ON DELETE NO ACTION)

- Kiểm tra: AnswerKey ∈ {A, B, C, D}; DifficultyLevel ∈ \[1, 3\]

- Kiểm tra: Status ∈ {Active, Inactive}

- Kiểm tra: UsagePurpose ∈ {ForTest, ForIntervention}

### A.19. Test

**Mô tả:** Đề kiểm tra.

| **STT** | **Tên cột**      | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                              |
|---------|------------------|------------------|----------|----------|----------------------------------------|
| 1       | Test_ID          | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)     |
| 2       | CreatedAt        | timestamptz      |          |          | Thời điểm tạo                          |
| 3       | UpdatedAt        | timestamptz      | x        |          | Thời điểm cập nhật gần nhất            |
| 4       | CreatedByUser_ID | text             |          |          | Tài khoản tạo bản ghi                  |
| 5       | UpdatedByUser_ID | text             | x        |          | Tài khoản cập nhật gần nhất            |
| 6       | TestName         | varchar(255)     |          |          | Tên đề                                 |
| 7       | Description      | text             | x        |          | Mô tả đề                               |
| 8       | Status           | varchar(20)      |          |          | Trạng thái: Draft, Published, Archived |

**Ràng buộc:**

- Khóa chính: (Test_ID)

- Kiểm tra: Status ∈ {Draft, Published, Archived}

### A.20. QuestionInTest

**Mô tả:** Câu hỏi trong đề, lưu bản chụp (snapshot) nội dung, đáp án và Subskill tại thời điểm tạo đề.

| **STT** | **Tên cột**       | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                            |
|---------|-------------------|------------------|----------|----------|------------------------------------------------------|
| 1       | QuestionInTest_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                   |
| 2       | Question_ID       | bigint           |          | FK       | Câu hỏi nguồn trong ngân hàng → Question.Question_ID |
| 3       | Test_ID           | bigint           |          | FK       | Tham chiếu đề → Test.Test_ID                         |
| 4       | SnSQuestionText   | text             |          |          | Nội dung câu hỏi (snapshot)                          |
| 5       | SnSOptionA        | text             |          |          | Lựa chọn A (snapshot)                                |
| 6       | SnSOptionB        | text             |          |          | Lựa chọn B (snapshot)                                |
| 7       | SnSOptionC        | text             |          |          | Lựa chọn C (snapshot)                                |
| 8       | SnSOptionD        | text             |          |          | Lựa chọn D (snapshot)                                |
| 9       | SnSAnswerKey      | varchar(1)       |          |          | Đáp án đúng (snapshot)                               |
| 10      | SnSSubskill_ID    | bigint           |          | FK       | Subskill tại thời điểm chụp (snapshot)               |
| 11      | QuestionNumber    | smallint         |          |          | Số thứ tự câu trong đề / phiếu                       |

**Ràng buộc:**

- Khóa chính: (QuestionInTest_ID)

- Khóa ngoại: Question_ID → Question.Question_ID (ON DELETE NO ACTION)

- Khóa ngoại: SnSSubskill_ID → Subskill.Subskill_ID (ON DELETE NO ACTION)

- Khóa ngoại: Test_ID → Test.Test_ID (ON DELETE NO ACTION)

- Duy nhất (Test_ID, Question_ID); Duy nhất (Test_ID, QuestionNumber)

- Kiểm tra: QuestionNumber \> 0; SnSAnswerKey ∈ {A, B, C, D}

- SnS\*: bản chụp (snapshot) câu hỏi tại thời điểm đưa vào đề

## Đợt đánh giá và kết quả

### A.21. Assessment

**Mô tả:** Đợt đánh giá: một đề được dùng cho một lớp, do một giáo viên phụ trách, vào một ngày.

| **STT** | **Tên cột**         | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                     |
|---------|---------------------|------------------|----------|----------|-----------------------------------------------|
| 1       | Assessment_ID       | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)            |
| 2       | Test_ID             | bigint           |          | FK       | Tham chiếu đề → Test.Test_ID                  |
| 3       | Class_ID            | bigint           |          | FK       | Tham chiếu lớp → Class.Class_ID               |
| 4       | CreatedByUser_ID    | text             |          |          | Tài khoản tạo bản ghi                         |
| 5       | UpdatedByUser_ID    | text             | x        |          | Tài khoản cập nhật gần nhất                   |
| 6       | CreatedAt           | timestamptz      |          |          | Thời điểm tạo                                 |
| 7       | UpdatedAt           | timestamptz      | x        |          | Thời điểm cập nhật gần nhất                   |
| 8       | AssessmentName      | varchar(255)     |          |          | Tên đợt đánh giá                              |
| 9       | AssessmentDate      | date             |          |          | Ngày diễn ra đợt đánh giá                     |
| 10      | Status              | varchar(20)      |          |          | Trạng thái: Draft, Open, Completed, Cancelled |
| 11      | Teacher_ID          | bigint           |          | FK       | Giáo viên phụ trách đợt → Teacher.Teacher_ID  |
| 12      | PlannedFollowupDate | date             | x        |          | Ngày dự kiến đánh giá theo dõi                |

**Ràng buộc:**

- Khóa chính: (Assessment_ID)

- Khóa ngoại: Class_ID → Class.Class_ID (ON DELETE NO ACTION)

- Khóa ngoại: Teacher_ID → Teacher.Teacher_ID (ON DELETE NO ACTION)

- Khóa ngoại: Test_ID → Test.Test_ID (ON DELETE NO ACTION)

- Kiểm tra: Status ∈ {Draft, Open, Completed, Cancelled}

- Kiểm tra: PlannedFollowupDate \> AssessmentDate (nếu có)

### A.22. ImportBatch

**Mô tả:** Một lần nhập kết quả từ file CSV (thành công hoặc thất bại).

| **STT** | **Tên cột**       | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                          |
|---------|-------------------|------------------|----------|----------|----------------------------------------------------|
| 1       | ImportBatch_ID    | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                 |
| 2       | Assessment_ID     | bigint           |          | FK       | Tham chiếu đợt đánh giá → Assessment.Assessment_ID |
| 3       | OriginalFileName  | text             |          |          | Tên file CSV gốc                                   |
| 4       | ImportedByUser_ID | text             |          |          | Tài khoản thực hiện nhập                           |
| 5       | Status            | varchar(20)      |          |          | Trạng thái: Pending, Succeeded, Failed             |
| 6       | TotalRows         | integer          |          |          | Số dòng học sinh trong file                        |
| 7       | ErrorSummary      | text             | x        |          | Tóm tắt lỗi khi nhập thất bại                      |
| 8       | CreatedAt         | timestamptz      |          |          | Thời điểm tạo                                      |
| 9       | CompletedAt       | timestamptz      | x        |          | Thời điểm kết thúc lần nhập                        |

**Ràng buộc:**

- Khóa chính: (ImportBatch_ID)

- Khóa ngoại: Assessment_ID → Assessment.Assessment_ID (ON DELETE NO ACTION)

- Kiểm tra: Status ∈ {Pending, Succeeded, Failed}; TotalRows ≥ 0

### A.23. Attempt

**Mô tả:** Lượt làm bài của một học sinh trong một đợt đánh giá.

| **STT** | **Tên cột**    | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                 |
|---------|----------------|------------------|----------|----------|-----------------------------------------------------------|
| 1       | Attempt_ID     | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                        |
| 2       | Assessment_ID  | bigint           |          | FK       | Tham chiếu đợt đánh giá → Assessment.Assessment_ID        |
| 3       | Student_ID     | bigint           |          | FK       | Tham chiếu học sinh → Student.Student_ID                  |
| 4       | ImportBatch_ID | bigint           |          | FK       | Lần nhập đã tạo lượt làm bài → ImportBatch.ImportBatch_ID |
| 5       | Score          | numeric(5,2)     |          |          | Điểm thang 10 (làm tròn 2 chữ số)                         |

**Ràng buộc:**

- Khóa chính: (Attempt_ID)

- Khóa ngoại: Assessment_ID → Assessment.Assessment_ID (ON DELETE NO ACTION)

- Khóa ngoại: ImportBatch_ID → ImportBatch.ImportBatch_ID (ON DELETE NO ACTION)

- Khóa ngoại: Student_ID → Student.Student_ID (ON DELETE NO ACTION)

- Duy nhất (Student_ID, Assessment_ID)

- Kiểm tra: Score ∈ \[0, 10\]

### A.24. Response

**Mô tả:** Kết quả đúng / sai của học sinh ở từng câu (theo snapshot của đề).

| **STT** | **Tên cột**       | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                      |
|---------|-------------------|------------------|----------|----------|----------------------------------------------------------------|
| 1       | Response_ID       | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                             |
| 2       | Attempt_ID        | bigint           |          | FK       | Tham chiếu lượt làm bài → Attempt.Attempt_ID                   |
| 3       | QuestionInTest_ID | bigint           |          | FK       | Tham chiếu câu hỏi trong đề → QuestionInTest.QuestionInTest_ID |
| 4       | IsCorrect         | boolean          |          |          | Học sinh trả lời đúng hay sai                                  |

**Ràng buộc:**

- Khóa chính: (Response_ID)

- Khóa ngoại: Attempt_ID → Attempt.Attempt_ID (ON DELETE NO ACTION)

- Khóa ngoại: QuestionInTest_ID → QuestionInTest.QuestionInTest_ID (ON DELETE NO ACTION)

- Duy nhất (Attempt_ID, QuestionInTest_ID)

## Gói can thiệp và phiếu ôn tập

### A.25. InterventionPackage

**Mô tả:** Gói can thiệp tạo từ kết quả một đợt đánh giá nguồn.

| **STT** | **Tên cột**            | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                     |
|---------|------------------------|------------------|----------|----------|-----------------------------------------------|
| 1       | InterventionPackage_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)            |
| 2       | Assessment_ID          | bigint           |          | FK       | Đợt đánh giá nguồn → Assessment.Assessment_ID |
| 3       | PackageName            | text             |          |          | Tên gói can thiệp                             |
| 4       | Instructions           | text             | x        |          | Hướng dẫn sử dụng gói                         |
| 5       | Status                 | varchar(20)      |          |          | Trạng thái: Draft, Published, Archived        |

**Ràng buộc:**

- Khóa chính: (InterventionPackage_ID)

- Khóa ngoại: Assessment_ID → Assessment.Assessment_ID (ON DELETE NO ACTION)

- Kiểm tra: Status ∈ {Draft, Published, Archived}

### A.26. InterventionPackageStudent

**Mô tả:** Học sinh được giao gói can thiệp, ngày phát và ghi chú nội bộ.

| **STT** | **Tên cột**                   | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                             |
|---------|-------------------------------|------------------|----------|----------|-----------------------------------------------------------------------|
| 1       | InterventionPackageStudent_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                                    |
| 2       | InterventionPackage_ID        | bigint           |          | FK       | Tham chiếu gói can thiệp → InterventionPackage.InterventionPackage_ID |
| 3       | Student_ID                    | bigint           |          | FK       | Tham chiếu học sinh → Student.Student_ID                              |
| 4       | DeliveredDate                 | date             | x        |          | Ngày phát / gửi gói thực tế                                           |
| 5       | TeacherNote                   | text             | x        |          | Ghi chú nội bộ của giáo viên (không vào báo cáo)                      |

**Ràng buộc:**

- Khóa chính: (InterventionPackageStudent_ID)

- Khóa ngoại: InterventionPackage_ID → InterventionPackage.InterventionPackage_ID (ON DELETE NO ACTION)

- Khóa ngoại: Student_ID → Student.Student_ID (ON DELETE NO ACTION)

- Duy nhất (Student_ID, InterventionPackage_ID)

### A.27. InterventionPackageResource

**Mô tả:** Học liệu thuộc gói can thiệp và thứ tự hiển thị.

| **STT** | **Tên cột**                    | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                             |
|---------|--------------------------------|------------------|----------|----------|-----------------------------------------------------------------------|
| 1       | InterventionPackageResource_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                                    |
| 2       | InterventionPackage_ID         | bigint           |          | FK       | Tham chiếu gói can thiệp → InterventionPackage.InterventionPackage_ID |
| 3       | InterventionResource_ID        | bigint           |          | FK       | Tham chiếu học liệu → InterventionResource.InterventionResource_ID    |
| 4       | SortOrder                      | smallint         |          |          | Thứ tự sắp xếp                                                        |

**Ràng buộc:**

- Khóa chính: (InterventionPackageResource_ID)

- Khóa ngoại: InterventionPackage_ID → InterventionPackage.InterventionPackage_ID (ON DELETE NO ACTION)

- Khóa ngoại: InterventionResource_ID → InterventionResource.InterventionResource_ID (ON DELETE NO ACTION)

- Duy nhất (InterventionPackage_ID, InterventionResource_ID)

- Duy nhất (InterventionPackage_ID, SortOrder); Kiểm tra: SortOrder \> 0

### A.28. InterventionResource

**Mô tả:** Kho học liệu (đường dẫn) gắn với Subskill.

| **STT** | **Tên cột**             | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                          |
|---------|-------------------------|------------------|----------|----------|------------------------------------|
| 1       | InterventionResource_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng) |
| 2       | Subskill_ID             | bigint           |          | FK       | Tham chiếu Subskill                |
| 3       | ResourceName            | text             |          |          | Tên học liệu                       |
| 4       | ResourceURL             | text             |          |          | Đường dẫn học liệu                 |
| 5       | Description             | text             |          |          | Mô tả học liệu                     |
| 6       | Status                  | varchar(20)      |          |          | Trạng thái: Active, Inactive       |

**Ràng buộc:**

- Khóa chính: (InterventionResource_ID)

- Khóa ngoại: Subskill_ID → Subskill.Subskill_ID (ON DELETE NO ACTION)

- Kiểm tra: Status ∈ {Active, Inactive}

### A.29. InterventionPackageTheory

**Mô tả:** Lý thuyết đưa vào phiếu ôn tập của gói, lưu snapshot nội dung.

| **STT** | **Tên cột**                  | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                             |
|---------|------------------------------|------------------|----------|----------|-----------------------------------------------------------------------|
| 1       | InterventionPackageTheory_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                                    |
| 2       | InterventionPackage_ID       | bigint           |          | FK       | Tham chiếu gói can thiệp → InterventionPackage.InterventionPackage_ID |
| 3       | InterventionTheory_ID        | bigint           |          | FK       | Lý thuyết nguồn → InterventionTheory.InterventionTheory_ID            |
| 4       | SortOrder                    | smallint         |          |          | Thứ tự sắp xếp                                                        |
| 5       | SnSTitle                     | text             |          |          | Tiêu đề lý thuyết (snapshot)                                          |
| 6       | SnSContent                   | text             |          |          | Nội dung lý thuyết (snapshot)                                         |
| 7       | SnSSubskill_ID               | bigint           |          | FK       | Subskill tại thời điểm chụp (snapshot)                                |

**Ràng buộc:**

- Khóa chính: (InterventionPackageTheory_ID)

- Khóa ngoại: InterventionPackage_ID → InterventionPackage.InterventionPackage_ID (ON DELETE NO ACTION)

- Khóa ngoại: InterventionTheory_ID → InterventionTheory.InterventionTheory_ID (ON DELETE NO ACTION)

- Khóa ngoại: SnSSubskill_ID → Subskill.Subskill_ID (ON DELETE NO ACTION)

- Duy nhất (InterventionPackage_ID, InterventionTheory_ID)

- Duy nhất (InterventionPackage_ID, SortOrder); Kiểm tra: SortOrder \> 0

- Kiểm tra: SnSTitle, SnSContent không rỗng

### A.30. InterventionTheory

**Mô tả:** Thư viện lý thuyết gắn với Subskill.

| **STT** | **Tên cột**           | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                          |
|---------|-----------------------|------------------|----------|----------|------------------------------------|
| 1       | InterventionTheory_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng) |
| 2       | Subskill_ID           | bigint           |          | FK       | Tham chiếu Subskill                |
| 3       | Title                 | text             |          |          | Tiêu đề lý thuyết                  |
| 4       | Content               | text             |          |          | Nội dung lý thuyết                 |
| 5       | Status                | varchar(20)      |          |          | Trạng thái: Active, Inactive       |

**Ràng buộc:**

- Khóa chính: (InterventionTheory_ID)

- Khóa ngoại: Subskill_ID → Subskill.Subskill_ID (ON DELETE NO ACTION)

- Kiểm tra: Status ∈ {Active, Inactive}

- Kiểm tra: Title 1–200 ký tự; Content 1–20 000 ký tự

### A.31. InterventionPackageQuestion

**Mô tả:** Câu luyện tập đưa vào phiếu ôn tập của gói, lưu snapshot nội dung.

| **STT** | **Tên cột**                    | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                             |
|---------|--------------------------------|------------------|----------|----------|-----------------------------------------------------------------------|
| 1       | InterventionPackageQuestion_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                                    |
| 2       | InterventionPackage_ID         | bigint           |          | FK       | Tham chiếu gói can thiệp → InterventionPackage.InterventionPackage_ID |
| 3       | Question_ID                    | bigint           |          | FK       | Câu hỏi nguồn (ForIntervention) → Question.Question_ID                |
| 4       | QuestionNumber                 | smallint         |          |          | Số thứ tự câu trong đề / phiếu                                        |
| 5       | SnSQuestionText                | text             |          |          | Nội dung câu hỏi (snapshot)                                           |
| 6       | SnSOptionA                     | text             |          |          | Lựa chọn A (snapshot)                                                 |
| 7       | SnSOptionB                     | text             |          |          | Lựa chọn B (snapshot)                                                 |
| 8       | SnSOptionC                     | text             |          |          | Lựa chọn C (snapshot)                                                 |
| 9       | SnSOptionD                     | text             |          |          | Lựa chọn D (snapshot)                                                 |
| 10      | SnSAnswerKey                   | char(1)          |          |          | Đáp án đúng (snapshot)                                                |
| 11      | SnSExplanation                 | text             |          |          | Lời giải thích (snapshot)                                             |
| 12      | SnSSubskill_ID                 | bigint           |          | FK       | Subskill tại thời điểm chụp (snapshot)                                |

**Ràng buộc:**

- Khóa chính: (InterventionPackageQuestion_ID)

- Khóa ngoại: InterventionPackage_ID → InterventionPackage.InterventionPackage_ID (ON DELETE NO ACTION)

- Khóa ngoại: Question_ID → Question.Question_ID (ON DELETE NO ACTION)

- Khóa ngoại: SnSSubskill_ID → Subskill.Subskill_ID (ON DELETE NO ACTION)

- Duy nhất (InterventionPackage_ID, Question_ID)

- Duy nhất (InterventionPackage_ID, QuestionNumber)

- Kiểm tra: QuestionNumber \> 0; SnSAnswerKey ∈ {A, B, C, D}

- Kiểm tra: SnSQuestionText, SnSOptionA–D không rỗng

### A.32. InterventionPackageFollowup

**Mô tả:** Liên kết gói can thiệp với các đợt đánh giá theo dõi để so sánh trước / sau.

| **STT** | **Tên cột**                    | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                              |
|---------|--------------------------------|------------------|----------|----------|------------------------------------------------------------------------|
| 1       | InterventionPackageFollowup_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                                     |
| 2       | FollowupAssessment_ID          | bigint           |          | FK       | Đợt đánh giá dùng để theo dõi sau can thiệp → Assessment.Assessment_ID |
| 3       | InterventionPackage_ID         | bigint           |          | FK       | Tham chiếu gói can thiệp → InterventionPackage.InterventionPackage_ID  |
| 4       | FollowupNote                   | text             | x        |          | Ghi chú về đợt theo dõi                                                |

**Ràng buộc:**

- Khóa chính: (InterventionPackageFollowup_ID)

- Khóa ngoại: FollowupAssessment_ID → Assessment.Assessment_ID (ON DELETE NO ACTION)

- Khóa ngoại: InterventionPackage_ID → InterventionPackage.InterventionPackage_ID (ON DELETE NO ACTION)

- Duy nhất (InterventionPackage_ID, FollowupAssessment_ID)

## Quan sát học tập và nhận xét

### A.33. LearningObservation

**Mô tả:** Ghi nhận quá trình học tập của học sinh theo lớp và giai đoạn (4 tiêu chí).

| **STT** | **Tên cột**            | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                 |
|---------|------------------------|------------------|----------|----------|-----------------------------------------------------------|
| 1       | LearningObservation_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                        |
| 2       | Student_ID             | bigint           |          | FK       | Tham chiếu học sinh → Student.Student_ID                  |
| 3       | Class_ID               | bigint           |          | FK       | Tham chiếu lớp → Class.Class_ID                           |
| 4       | PeriodStart            | date             |          |          | Ngày bắt đầu giai đoạn ghi nhận                           |
| 5       | PeriodEnd              | date             |          |          | Ngày kết thúc giai đoạn ghi nhận                          |
| 6       | TaskCompletion         | varchar(20)      | x        |          | Tiêu chí hoàn thành nhiệm vụ: Full, Partial, NotCompleted |
| 7       | Participation          | varchar(20)      | x        |          | Tiêu chí tham gia: Proactive, WithPrompt, Limited         |
| 8       | Preparation            | varchar(20)      | x        |          | Tiêu chí chuẩn bị bài: Full, Partial, NotPrepared         |
| 9       | Cooperation            | varchar(20)      | x        |          | Tiêu chí hợp tác: Positive, NeedsSupport                  |
| 10      | RubricVersion          | smallint         |          |          | Phiên bản bộ tiêu chí ghi nhận                            |
| 11      | CreatedByUser_ID       | text             |          | FK       | Giáo viên ghi nhận → AspNetUsers.Id                       |
| 12      | UpdatedByUser_ID       | text             | x        | FK       | Tài khoản cập nhật gần nhất → AspNetUsers.Id              |
| 13      | CreatedAt              | timestamptz      |          |          | Thời điểm tạo                                             |
| 14      | UpdatedAt              | timestamptz      | x        |          | Thời điểm cập nhật gần nhất                               |
| 15      | RowVersion             | bigint           |          |          | Số phiên bản bản ghi (chống ghi đè đồng thời)             |

**Ràng buộc:**

- Khóa chính: (LearningObservation_ID)

- Khóa ngoại: Class_ID → Class.Class_ID (ON DELETE NO ACTION)

- Khóa ngoại: CreatedByUser_ID → AspNetUsers.Id (ON DELETE NO ACTION)

- Khóa ngoại: Student_ID → Student.Student_ID (ON DELETE NO ACTION)

- Khóa ngoại: UpdatedByUser_ID → AspNetUsers.Id (ON DELETE NO ACTION)

- Duy nhất (Student_ID, Class_ID, PeriodStart, PeriodEnd, CreatedByUser_ID)

- Kiểm tra: PeriodStart ≤ PeriodEnd; RubricVersion = 1; RowVersion \> 0

- Kiểm tra: TaskCompletion ∈ {Full, Partial, NotCompleted}

- Kiểm tra: Participation ∈ {Proactive, WithPrompt, Limited}

- Kiểm tra: Preparation ∈ {Full, Partial, NotPrepared}

- Kiểm tra: Cooperation ∈ {Positive, NeedsSupport}

### A.34. StudentReportComment

**Mô tả:** Nhận xét gửi phụ huynh theo học sinh và đợt đánh giá, có lịch sử phiên bản và nguồn AI.

| **STT** | **Tên cột**             | **Kiểu dữ liệu** | **Null** | **Khóa** | **Mô tả**                                                                           |
|---------|-------------------------|------------------|----------|----------|-------------------------------------------------------------------------------------|
| 1       | StudentReportComment_ID | bigint           |          | PK       | Mã định danh (khóa chính, tự tăng)                                                  |
| 2       | Student_ID              | bigint           |          | FK       | Tham chiếu học sinh → Student.Student_ID                                            |
| 3       | Assessment_ID           | bigint           |          | FK       | Tham chiếu đợt đánh giá → Assessment.Assessment_ID                                  |
| 4       | LearningObservation_ID  | bigint           | x        | FK       | Ghi nhận quá trình dùng làm căn cứ → LearningObservation.LearningObservation_ID     |
| 5       | BaseApprovedComment_ID  | bigint           | x        | FK       | Bản đã duyệt mà bản nháp này kế thừa → StudentReportComment.StudentReportComment_ID |
| 6       | RevisionNumber          | integer          |          |          | Số phiên bản nhận xét                                                               |
| 7       | Status                  | varchar(20)      |          |          | Trạng thái: Draft, Approved, Superseded, Discarded                                  |
| 8       | CommentText             | text             |          |          | Nội dung nhận xét                                                                   |
| 9       | Source                  | varchar(20)      |          |          | Nguồn nội dung: Manual, Gemini                                                      |
| 10      | AiDraftText             | text             | x        |          | Bản gốc nội dung do Gemini sinh                                                     |
| 11      | AiModel                 | varchar(100)     | x        |          | Tên model AI đã dùng                                                                |
| 12      | PromptVersion           | varchar(50)      | x        |          | Phiên bản prompt đã dùng                                                            |
| 13      | GeneratedAt             | timestamptz      | x        |          | Thời điểm AI sinh nội dung                                                          |
| 14      | InputSchemaVersion      | smallint         |          |          | Phiên bản cấu trúc dữ liệu căn cứ                                                   |
| 15      | InputSnapshot           | jsonb            |          |          | Bản chụp dữ liệu căn cứ dùng để soạn nhận xét (JSON)                                |
| 16      | InputFingerprint        | char(64)         |          |          | Mã băm SHA-256 của dữ liệu căn cứ                                                   |
| 17      | CreatedByUser_ID        | text             |          | FK       | Tài khoản tạo bản ghi → AspNetUsers.Id                                              |
| 18      | UpdatedByUser_ID        | text             | x        | FK       | Tài khoản cập nhật gần nhất → AspNetUsers.Id                                        |
| 19      | ApprovedByUser_ID       | text             | x        | FK       | Tài khoản đã duyệt → AspNetUsers.Id                                                 |
| 20      | CreatedAt               | timestamptz      |          |          | Thời điểm tạo                                                                       |
| 21      | UpdatedAt               | timestamptz      | x        |          | Thời điểm cập nhật gần nhất                                                         |
| 22      | ApprovedAt              | timestamptz      | x        |          | Thời điểm duyệt                                                                     |
| 23      | RowVersion              | bigint           |          |          | Số phiên bản bản ghi (chống ghi đè đồng thời)                                       |
| 24      | ProcessCommentText      | text             | x        |          | Phần nhận xét về quá trình học tập                                                  |
| 25      | AiProcessDraftText      | text             | x        |          | Bản gốc phần nhận xét quá trình do AI sinh                                          |

**Ràng buộc:**

- Khóa chính: (StudentReportComment_ID)

- Khóa ngoại: ApprovedByUser_ID → AspNetUsers.Id (ON DELETE NO ACTION)

- Khóa ngoại: Assessment_ID → Assessment.Assessment_ID (ON DELETE NO ACTION)

- Khóa ngoại: BaseApprovedComment_ID → StudentReportComment.StudentReportComment_ID (ON DELETE NO ACTION)

- Khóa ngoại: CreatedByUser_ID → AspNetUsers.Id (ON DELETE NO ACTION)

- Khóa ngoại: LearningObservation_ID → LearningObservation.LearningObservation_ID (ON DELETE NO ACTION)

- Khóa ngoại: Student_ID → Student.Student_ID (ON DELETE NO ACTION)

- Khóa ngoại: UpdatedByUser_ID → AspNetUsers.Id (ON DELETE NO ACTION)

- Duy nhất (Student_ID, Assessment_ID, RevisionNumber)

- Duy nhất có điều kiện: tối đa 1 bản Draft và 1 bản Approved cho mỗi (Student_ID, Assessment_ID)

- Kiểm tra: Status ∈ {Draft, Approved, Superseded, Discarded}

- Kiểm tra: Approved/Superseded ⇔ có ApprovedByUser_ID, ApprovedAt và CommentText không rỗng

- Kiểm tra: Source ∈ {Manual, Gemini}; Gemini ⇔ có AiDraftText, AiModel, PromptVersion, GeneratedAt

- Kiểm tra: Source = Manual ⇒ AiProcessDraftText NULL

- Kiểm tra: các trường văn bản ≤ 10 000 ký tự; InputFingerprint = SHA-256 (64 hex)

- Kiểm tra: InputSnapshot là JSON object; InputSchemaVersion = 1; RevisionNumber, RowVersion \> 0
