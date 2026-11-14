# ERD – Harmonia

Cơ sở dữ liệu PostgreSQL gồm **34 bảng** (25 bảng schema `public`, 9 bảng schema `auth`), **53 khóa ngoại**.

**Ký hiệu**

- `A --< B` : một A có nhiều B (B giữ khóa ngoại tới A)
- `A >-- B` : nhiều A thuộc về một B (A giữ khóa ngoại tới B)
- `A -- B`  : quan hệ một – một
- `PK` khóa chính · `FK` khóa ngoại · `UK` ràng buộc duy nhất

## 1. Nhóm học vụ và tài khoản

```text
Class
  PK Class_ID
  UK (ClassCode, AcademicYear)
  |
  |--< ClassMembership >-- Student
  |      PK ClassMembership_ID
  |      FK Class_ID
  |      FK Student_ID
  |      EXCLUDE: một học sinh không thuộc hai lớp trong cùng khoảng thời gian
  |
  `--< TeacherClassAssignment >-- Teacher
         PK TeacherClassAssignment_ID
         FK Class_ID
         FK Teacher_ID
         EXCLUDE: một giáo viên không có hai phân công chồng lấn cho cùng lớp

Student
  PK Student_ID
  UK StudentCode

Teacher
  PK Teacher_ID
  UK TeacherCode, Email, PhoneNumber
  |
  `-- TeacherUserLinks -- AspNetUsers   (1 – 1)
         PK/FK User_ID
         FK Teacher_ID (UK)
```

## 2. Nhóm xác thực (schema auth)

```text
AspNetUsers
  PK Id
  |
  |--< AspNetUserRoles >-- AspNetRoles
  |      PK (UserId, RoleId)
  |      FK UserId
  |      FK RoleId
  |
  |--< AspNetUserClaims
  |      PK Id
  |      FK UserId
  |
  |--< AspNetUserLogins
  |      PK (LoginProvider, ProviderKey)
  |      FK UserId
  |
  |--< AspNetUserTokens
  |      PK (UserId, LoginProvider, Name)
  |      FK UserId
  |
  `-- TeacherUserLinks -- Teacher   (1 – 1)
         PK/FK User_ID
         FK Teacher_ID (UK)

AspNetRoles
  PK Id
  |
  `--< AspNetRoleClaims
         PK Id
         FK RoleId

__EFMigrationsHistory   (bảng độc lập)
  PK MigrationId
```

## 3. Nhóm taxonomy và ngân hàng đề

```text
CurriculumUnit
  PK CurriculumUnit_ID
  UK (Grade, UnitNumber)
  |
  `--< KnowledgeArea
         PK KnowledgeArea_ID
         FK CurriculumUnit_ID
         UK KnowledgeAreaCode
         |
         `--< Subskill
                PK Subskill_ID
                FK KnowledgeArea_ID
                UK SubskillCode
                |
                |--< Question
                |      PK Question_ID
                |      FK Subskill_ID
                |      UK QuestionCode
                |      |
                |      `--< QuestionInTest >-- Test
                |             PK QuestionInTest_ID
                |             FK Test_ID
                |             FK Question_ID
                |             FK SnSSubskill_ID  >-- Subskill   (bản chụp)
                |             UK (Test_ID, Question_ID)
                |             UK (Test_ID, QuestionNumber)
                |
                |--< InterventionResource
                |      PK InterventionResource_ID
                |      FK Subskill_ID
                |
                `--< InterventionTheory
                       PK InterventionTheory_ID
                       FK Subskill_ID
```

## 4. Nhóm đợt đánh giá và kết quả

```text
Test
  PK Test_ID
  |
  `--< Assessment >-- Class
         PK Assessment_ID
         FK Test_ID
         FK Class_ID
         FK Teacher_ID  >-- Teacher
         |
         |--< ImportBatch
         |      PK ImportBatch_ID
         |      FK Assessment_ID
         |      |
         |      `--< Attempt   (lượt làm bài do lần nhập này tạo ra)
         |
         `--< Attempt >-- Student
                PK Attempt_ID
                FK Assessment_ID
                FK Student_ID
                FK ImportBatch_ID
                UK (Student_ID, Assessment_ID)
                |
                `--< Response >-- QuestionInTest
                       PK Response_ID
                       FK Attempt_ID
                       FK QuestionInTest_ID
                       UK (Attempt_ID, QuestionInTest_ID)
```

## 5. Nhóm can thiệp và phiếu ôn tập

```text
Assessment
  PK Assessment_ID
  |
  `--< InterventionPackage
         PK InterventionPackage_ID
         FK Assessment_ID            (đợt đánh giá nguồn)
         |
         |--< InterventionPackageStudent >-- Student
         |      PK InterventionPackageStudent_ID
         |      FK InterventionPackage_ID
         |      FK Student_ID
         |      UK (Student_ID, InterventionPackage_ID)
         |
         |--< InterventionPackageResource >-- InterventionResource
         |      PK InterventionPackageResource_ID
         |      FK InterventionPackage_ID
         |      FK InterventionResource_ID
         |
         |--< InterventionPackageTheory >-- InterventionTheory
         |      PK InterventionPackageTheory_ID
         |      FK InterventionPackage_ID
         |      FK InterventionTheory_ID
         |      FK SnSSubskill_ID  >-- Subskill   (bản chụp)
         |
         |--< InterventionPackageQuestion >-- Question
         |      PK InterventionPackageQuestion_ID
         |      FK InterventionPackage_ID
         |      FK Question_ID
         |      FK SnSSubskill_ID  >-- Subskill   (bản chụp)
         |
         `--< InterventionPackageFollowup >-- Assessment
                PK InterventionPackageFollowup_ID
                FK InterventionPackage_ID
                FK FollowupAssessment_ID     (đợt đánh giá lại)
                UK (InterventionPackage_ID, FollowupAssessment_ID)
```

## 6. Nhóm quan sát học tập và nhận xét

```text
Student
  PK Student_ID
  |
  |--< LearningObservation >-- Class
  |      PK LearningObservation_ID
  |      FK Student_ID
  |      FK Class_ID
  |      FK CreatedByUser_ID  >-- AspNetUsers
  |      FK UpdatedByUser_ID  >-- AspNetUsers
  |      |
  |      `--< StudentReportComment   (căn cứ ghi nhận quá trình)
  |
  `--< StudentReportComment >-- Assessment
         PK StudentReportComment_ID
         FK Student_ID
         FK Assessment_ID
         FK LearningObservation_ID
         FK BaseApprovedComment_ID  >-- StudentReportComment   (tự tham chiếu)
         FK CreatedByUser_ID        >-- AspNetUsers
         FK UpdatedByUser_ID        >-- AspNetUsers
         FK ApprovedByUser_ID       >-- AspNetUsers
         UK (Student_ID, Assessment_ID, RevisionNumber)
```

## Tổng hợp quan hệ

| # | Bảng con (giữ FK) | Bảng cha | Cột khóa ngoại | Bản số (con – cha) |
|---|---|---|---|---|
| 1 | ClassMembership | Class | Class_ID | N – 1 |
| 2 | ClassMembership | Student | Student_ID | N – 1 |
| 3 | TeacherClassAssignment | Class | Class_ID | N – 1 |
| 4 | TeacherClassAssignment | Teacher | Teacher_ID | N – 1 |
| 5 | TeacherUserLinks | Teacher | Teacher_ID | 1 – 1 |
| 6 | TeacherUserLinks | AspNetUsers | User_ID | 1 – 1 |
| 7 | AspNetUserRoles | AspNetRoles | RoleId | N – 1 |
| 8 | AspNetUserRoles | AspNetUsers | UserId | N – 1 |
| 9 | AspNetRoleClaims | AspNetRoles | RoleId | N – 1 |
| 10 | AspNetUserClaims | AspNetUsers | UserId | N – 1 |
| 11 | AspNetUserLogins | AspNetUsers | UserId | N – 1 |
| 12 | AspNetUserTokens | AspNetUsers | UserId | N – 1 |
| 13 | KnowledgeArea | CurriculumUnit | CurriculumUnit_ID | N – 1 |
| 14 | Subskill | KnowledgeArea | KnowledgeArea_ID | N – 1 |
| 15 | Question | Subskill | Subskill_ID | N – 1 |
| 16 | QuestionInTest | Question | Question_ID | N – 1 |
| 17 | QuestionInTest | Subskill | SnSSubskill_ID | N – 1 |
| 18 | QuestionInTest | Test | Test_ID | N – 1 |
| 19 | Assessment | Class | Class_ID | N – 1 |
| 20 | Assessment | Teacher | Teacher_ID | N – 1 |
| 21 | Assessment | Test | Test_ID | N – 1 |
| 22 | ImportBatch | Assessment | Assessment_ID | N – 1 |
| 23 | Attempt | Assessment | Assessment_ID | N – 1 |
| 24 | Attempt | ImportBatch | ImportBatch_ID | N – 1 |
| 25 | Attempt | Student | Student_ID | N – 1 |
| 26 | Response | Attempt | Attempt_ID | N – 1 |
| 27 | Response | QuestionInTest | QuestionInTest_ID | N – 1 |
| 28 | InterventionPackage | Assessment | Assessment_ID | N – 1 |
| 29 | InterventionPackageStudent | InterventionPackage | InterventionPackage_ID | N – 1 |
| 30 | InterventionPackageStudent | Student | Student_ID | N – 1 |
| 31 | InterventionPackageResource | InterventionPackage | InterventionPackage_ID | N – 1 |
| 32 | InterventionPackageResource | InterventionResource | InterventionResource_ID | N – 1 |
| 33 | InterventionResource | Subskill | Subskill_ID | N – 1 |
| 34 | InterventionPackageTheory | InterventionPackage | InterventionPackage_ID | N – 1 |
| 35 | InterventionPackageTheory | InterventionTheory | InterventionTheory_ID | N – 1 |
| 36 | InterventionPackageTheory | Subskill | SnSSubskill_ID | N – 1 |
| 37 | InterventionTheory | Subskill | Subskill_ID | N – 1 |
| 38 | InterventionPackageQuestion | InterventionPackage | InterventionPackage_ID | N – 1 |
| 39 | InterventionPackageQuestion | Question | Question_ID | N – 1 |
| 40 | InterventionPackageQuestion | Subskill | SnSSubskill_ID | N – 1 |
| 41 | InterventionPackageFollowup | Assessment | FollowupAssessment_ID | N – 1 |
| 42 | InterventionPackageFollowup | InterventionPackage | InterventionPackage_ID | N – 1 |
| 43 | LearningObservation | Class | Class_ID | N – 1 |
| 44 | LearningObservation | AspNetUsers | CreatedByUser_ID | N – 1 |
| 45 | LearningObservation | Student | Student_ID | N – 1 |
| 46 | LearningObservation | AspNetUsers | UpdatedByUser_ID | N – 1 |
| 47 | StudentReportComment | AspNetUsers | ApprovedByUser_ID | N – 1 |
| 48 | StudentReportComment | Assessment | Assessment_ID | N – 1 |
| 49 | StudentReportComment | StudentReportComment | BaseApprovedComment_ID | N – 1 (tự tham chiếu) |
| 50 | StudentReportComment | AspNetUsers | CreatedByUser_ID | N – 1 |
| 51 | StudentReportComment | LearningObservation | LearningObservation_ID | N – 1 |
| 52 | StudentReportComment | Student | Student_ID | N – 1 |
| 53 | StudentReportComment | AspNetUsers | UpdatedByUser_ID | N – 1 |
