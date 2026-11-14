-- =====================================================================
-- Harmonia Assessment – lược đồ cơ sở dữ liệu (PostgreSQL)
-- 34 bảng: 25 bảng schema public (nghiệp vụ) + 9 bảng schema auth (tài khoản)
-- Tệp được sinh từ catalog lược đồ (information_schema / pg_catalog) của CSDL
-- phát triển; dùng để tham khảo và dựng CSDL trống. Không chứa dữ liệu.
-- Kiểm tra trên PostgreSQL 16+; môi trường phát triển dùng PostgreSQL 18.
-- =====================================================================

CREATE EXTENSION IF NOT EXISTS btree_gist;   -- cần cho ràng buộc EXCLUDE

CREATE SCHEMA IF NOT EXISTS auth;

SET search_path = public, auth;

CREATE TABLE public."Class" (
    "Class_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "ClassCode" character varying(50) NOT NULL,
    "ClassName" character varying(150) NOT NULL,
    "Grade" smallint NOT NULL,
    "AcademicYear" character varying(9) NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    CONSTRAINT "Class_pkey" PRIMARY KEY ("Class_ID"),
    CONSTRAINT "CU_ClassCode_AcademicYear" UNIQUE ("ClassCode", "AcademicYear"),
    CONSTRAINT "CK_Class_AcademicYear" CHECK (
CASE
    WHEN (("AcademicYear")::text ~ '^[0-9]{4}-[0-9]{4}$'::text) THEN ((SUBSTRING("AcademicYear" FROM 6 FOR 4))::integer = ((SUBSTRING("AcademicYear" FROM 1 FOR 4))::integer + 1))
    ELSE false
END),
    CONSTRAINT "CK_Class_Status" CHECK ((("Status")::text = ANY ((ARRAY['Active'::character varying, 'Inactive'::character varying])::text[]))),
    CONSTRAINT "CK_Grade" CHECK ((("Grade" >= 6) AND ("Grade" <= 12)))
);

CREATE TABLE public."Student" (
    "Student_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "StudentCode" character varying(50) NOT NULL,
    "FullName" character varying(150) NOT NULL,
    "DateOfBirth" date NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "Gender" character varying(20) NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "PrimaryGuardianName" character varying(150),
    "PrimaryGuardianPhonenumber" character varying(20),
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    CONSTRAINT "PK_student" PRIMARY KEY ("Student_ID"),
    CONSTRAINT "CU_StudentCode" UNIQUE ("StudentCode"),
    CONSTRAINT "CK_Student_Gender" CHECK ((("Gender")::text = ANY ((ARRAY['Male'::character varying, 'Female'::character varying])::text[]))),
    CONSTRAINT "CK_Student_Status" CHECK ((("Status")::text = ANY ((ARRAY['Active'::character varying, 'Inactive'::character varying])::text[])))
);

CREATE TABLE public."Teacher" (
    "Teacher_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "TeacherCode" character varying(50) NOT NULL,
    "FullName" character varying(150) NOT NULL,
    "Email" character varying(150) NOT NULL,
    "PhoneNumber" character varying(20) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    CONSTRAINT "Teacher_pkey" PRIMARY KEY ("Teacher_ID"),
    CONSTRAINT "CU_Email" UNIQUE ("Email"),
    CONSTRAINT "CU_PhoneNumber" UNIQUE ("PhoneNumber"),
    CONSTRAINT "CU_TeacherCode" UNIQUE ("TeacherCode"),
    CONSTRAINT "CK_Teacher_Status" CHECK ((("Status")::text = ANY ((ARRAY['Active'::character varying, 'Inactive'::character varying])::text[])))
);

CREATE TABLE public."ClassMembership" (
    "ClassMembership_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Student_ID" bigint NOT NULL,
    "Class_ID" bigint NOT NULL,
    "StartDate" date NOT NULL,
    "EndDate" date,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    CONSTRAINT "ClassMembership_pkey" PRIMARY KEY ("ClassMembership_ID"),
    CONSTRAINT "CK_EndDateStartDate" CHECK ((("EndDate" IS NULL) OR ("EndDate" > "StartDate"))),
    CONSTRAINT "EX_ClassMembership_NoOverlap" EXCLUDE USING gist ("Student_ID" WITH =, daterange("StartDate", "EndDate", '[)'::text) WITH &&)
);

CREATE TABLE public."TeacherClassAssignment" (
    "TeacherClassAssignment_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Class_ID" bigint NOT NULL,
    "Teacher_ID" bigint NOT NULL,
    "StartDate" date NOT NULL,
    "EndDate" date,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    CONSTRAINT "TeacherClassAssignment_pkey" PRIMARY KEY ("TeacherClassAssignment_ID"),
    CONSTRAINT "CK_EndDateStartDate" CHECK ((("EndDate" IS NULL) OR ("EndDate" > "StartDate"))),
    CONSTRAINT "EX_TeacherClassAssignment_NoOverlap" EXCLUDE USING gist ("Teacher_ID" WITH =, "Class_ID" WITH =, daterange("StartDate", "EndDate", '[)'::text) WITH &&)
);

CREATE TABLE public."CurriculumUnit" (
    "CurriculumUnit_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Grade" smallint NOT NULL,
    "UnitNumber" smallint NOT NULL,
    "UnitName" text NOT NULL,
    CONSTRAINT "CurriculumUnit_pkey" PRIMARY KEY ("CurriculumUnit_ID"),
    CONSTRAINT "CK_UnitNumberGrade" UNIQUE ("Grade", "UnitNumber"),
    CONSTRAINT "CK_Grade" CHECK ((("Grade" >= 6) AND ("Grade" <= 12))),
    CONSTRAINT "CK_UnitNumber" CHECK (("UnitNumber" > 0))
);

CREATE TABLE public."KnowledgeArea" (
    "KnowledgeArea_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "KnowledgeAreaCode" character varying(50) NOT NULL,
    "KnowledgeAreaName" character varying(255) NOT NULL,
    "Description" text,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "CurriculumUnit_ID" bigint NOT NULL,
    "SortOrder" smallint NOT NULL,
    CONSTRAINT "KnowledgeArea_pkey" PRIMARY KEY ("KnowledgeArea_ID"),
    CONSTRAINT "CU_KnowledgeAreaCode" UNIQUE ("KnowledgeAreaCode"),
    CONSTRAINT "CK_KnowledgeArea_Status" CHECK ((("Status")::text = ANY ((ARRAY['Active'::character varying, 'Inactive'::character varying])::text[]))),
    CONSTRAINT "CK_SortOrder" CHECK (("SortOrder" > 0))
);

CREATE TABLE public."Subskill" (
    "Subskill_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "KnowledgeArea_ID" bigint NOT NULL,
    "SubskillCode" character varying(50) NOT NULL,
    "SubskillName" character varying(255) NOT NULL,
    "Description" text NOT NULL,
    "SortOrder" smallint NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    CONSTRAINT "Subskill_pkey" PRIMARY KEY ("Subskill_ID"),
    CONSTRAINT "CU_SubskillCode" UNIQUE ("SubskillCode"),
    CONSTRAINT "CK_SortOrder" CHECK (("SortOrder" > 0)),
    CONSTRAINT "CK_Subskill_Status" CHECK ((("Status")::text = ANY ((ARRAY['Active'::character varying, 'Inactive'::character varying])::text[])))
);

CREATE TABLE public."Question" (
    "Question_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Subskill_ID" bigint NOT NULL,
    "QuestionCode" character varying(50) NOT NULL,
    "QuestionText" text NOT NULL,
    "OptionA" text NOT NULL,
    "OptionB" text NOT NULL,
    "OptionC" text NOT NULL,
    "OptionD" text NOT NULL,
    "AnswerKey" character varying(1) NOT NULL,
    "DifficultyLevel" smallint NOT NULL,
    "Explanation" text NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    "UsagePurpose" character varying(20) DEFAULT 'ForTest'::character varying NOT NULL,
    CONSTRAINT "Question_pkey" PRIMARY KEY ("Question_ID"),
    CONSTRAINT "CU_QuestionCode" UNIQUE ("QuestionCode"),
    CONSTRAINT "CK_AnswerKey" CHECK ((("AnswerKey")::text = ANY ((ARRAY['A'::character varying, 'B'::character varying, 'C'::character varying, 'D'::character varying])::text[]))),
    CONSTRAINT "CK_Question_DifficultyLevel" CHECK ((("DifficultyLevel" >= 1) AND ("DifficultyLevel" <= 3))),
    CONSTRAINT "CK_Question_Status" CHECK ((("Status")::text = ANY ((ARRAY['Active'::character varying, 'Inactive'::character varying])::text[]))),
    CONSTRAINT "CK_Question_UsagePurpose" CHECK ((("UsagePurpose")::text = ANY ((ARRAY['ForTest'::character varying, 'ForIntervention'::character varying])::text[])))
);

CREATE TABLE public."Test" (
    "Test_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    "TestName" character varying(255) NOT NULL,
    "Description" text,
    "Status" character varying(20) DEFAULT 'Draft'::character varying NOT NULL,
    CONSTRAINT "Test_pkey" PRIMARY KEY ("Test_ID"),
    CONSTRAINT "CK_TestStatus" CHECK ((("Status")::text = ANY ((ARRAY['Draft'::character varying, 'Published'::character varying, 'Archived'::character varying])::text[])))
);

CREATE TABLE public."QuestionInTest" (
    "QuestionInTest_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Question_ID" bigint NOT NULL,
    "Test_ID" bigint NOT NULL,
    "SnSQuestionText" text NOT NULL,
    "SnSOptionA" text NOT NULL,
    "SnSOptionB" text NOT NULL,
    "SnSOptionC" text NOT NULL,
    "SnSOptionD" text NOT NULL,
    "SnSAnswerKey" character varying(1) NOT NULL,
    "SnSSubskill_ID" bigint NOT NULL,
    "QuestionNumber" smallint NOT NULL,
    CONSTRAINT "QuestionInTest_pkey" PRIMARY KEY ("QuestionInTest_ID"),
    CONSTRAINT "CU_TestQuestionInTest" UNIQUE ("Test_ID", "Question_ID"),
    CONSTRAINT "CU_TestQuestionNumber" UNIQUE ("Test_ID", "QuestionNumber"),
    CONSTRAINT "CK_QuestionNumber" CHECK (("QuestionNumber" > 0)),
    CONSTRAINT "CK_SnSAnswerKey" CHECK ((("SnSAnswerKey")::text = ANY ((ARRAY['A'::character varying, 'B'::character varying, 'C'::character varying, 'D'::character varying])::text[])))
);

CREATE TABLE public."Assessment" (
    "Assessment_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Test_ID" bigint NOT NULL,
    "Class_ID" bigint NOT NULL,
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "AssessmentName" character varying(255) NOT NULL,
    "AssessmentDate" date NOT NULL,
    "Status" character varying(20) DEFAULT 'Draft'::character varying NOT NULL,
    "Teacher_ID" bigint NOT NULL,
    "PlannedFollowupDate" date,
    CONSTRAINT "Assessment_pkey" PRIMARY KEY ("Assessment_ID"),
    CONSTRAINT "CK_AssessmentStatus" CHECK ((("Status")::text = ANY ((ARRAY['Draft'::character varying, 'Open'::character varying, 'Completed'::character varying, 'Cancelled'::character varying])::text[]))),
    CONSTRAINT "CK_Assessment_PlannedFollowupDate" CHECK ((("PlannedFollowupDate" IS NULL) OR ("PlannedFollowupDate" > "AssessmentDate")))
);

CREATE TABLE public."ImportBatch" (
    "ImportBatch_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Assessment_ID" bigint NOT NULL,
    "OriginalFileName" text NOT NULL,
    "ImportedByUser_ID" text NOT NULL,
    "Status" character varying(20) DEFAULT 'Pending'::character varying NOT NULL,
    "TotalRows" integer NOT NULL,
    "ErrorSummary" text,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "CompletedAt" timestamp with time zone,
    CONSTRAINT "ImportBatch_pkey" PRIMARY KEY ("ImportBatch_ID"),
    CONSTRAINT "CK_ImportBatchStatus" CHECK ((("Status")::text = ANY ((ARRAY['Pending'::character varying, 'Succeeded'::character varying, 'Failed'::character varying])::text[]))),
    CONSTRAINT "CK_TotalRows" CHECK (("TotalRows" >= 0))
);

CREATE TABLE public."Attempt" (
    "Attempt_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Assessment_ID" bigint NOT NULL,
    "Student_ID" bigint NOT NULL,
    "ImportBatch_ID" bigint NOT NULL,
    "Score" numeric(5,2) NOT NULL,
    CONSTRAINT "Attempt_pkey" PRIMARY KEY ("Attempt_ID"),
    CONSTRAINT "CU_StudentAssessment" UNIQUE ("Student_ID", "Assessment_ID"),
    CONSTRAINT "CK_Attempt_Score_Range" CHECK ((("Score" >= (0)::numeric) AND ("Score" <= (10)::numeric)))
);

CREATE TABLE public."Response" (
    "Response_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Attempt_ID" bigint NOT NULL,
    "QuestionInTest_ID" bigint NOT NULL,
    "IsCorrect" boolean NOT NULL,
    CONSTRAINT "Response_pkey" PRIMARY KEY ("Response_ID"),
    CONSTRAINT "CU_AttemptQuestionInTest" UNIQUE ("Attempt_ID", "QuestionInTest_ID")
);

CREATE TABLE public."InterventionResource" (
    "InterventionResource_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Subskill_ID" bigint NOT NULL,
    "ResourceName" text NOT NULL,
    "ResourceURL" text NOT NULL,
    "Description" text NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    CONSTRAINT "InterventionResource_pkey" PRIMARY KEY ("InterventionResource_ID"),
    CONSTRAINT "CK_InterventionResource_Status" CHECK ((("Status")::text = ANY ((ARRAY['Active'::character varying, 'Inactive'::character varying])::text[])))
);

CREATE TABLE public."InterventionTheory" (
    "InterventionTheory_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Subskill_ID" bigint NOT NULL,
    "Title" text NOT NULL,
    "Content" text NOT NULL,
    "Status" character varying(20) DEFAULT 'Active'::character varying NOT NULL,
    CONSTRAINT "InterventionTheory_pkey" PRIMARY KEY ("InterventionTheory_ID"),
    CONSTRAINT "InterventionTheory_Content_check" CHECK (((length(btrim("Content")) >= 1) AND (length(btrim("Content")) <= 20000))),
    CONSTRAINT "InterventionTheory_Status_check" CHECK ((("Status")::text = ANY ((ARRAY['Active'::character varying, 'Inactive'::character varying])::text[]))),
    CONSTRAINT "InterventionTheory_Title_check" CHECK (((length(btrim("Title")) >= 1) AND (length(btrim("Title")) <= 200)))
);

CREATE TABLE public."InterventionPackage" (
    "InterventionPackage_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Assessment_ID" bigint NOT NULL,
    "PackageName" text NOT NULL,
    "Instructions" text,
    "Status" character varying(20) DEFAULT 'Draft'::character varying NOT NULL,
    CONSTRAINT "InterventionPackage_pkey" PRIMARY KEY ("InterventionPackage_ID"),
    CONSTRAINT "CK_StatusInterventionPackage" CHECK ((("Status")::text = ANY ((ARRAY['Draft'::character varying, 'Published'::character varying, 'Archived'::character varying])::text[])))
);

CREATE TABLE public."InterventionPackageStudent" (
    "InterventionPackageStudent_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "InterventionPackage_ID" bigint NOT NULL,
    "Student_ID" bigint NOT NULL,
    "DeliveredDate" date,
    "TeacherNote" text,
    CONSTRAINT "InterventionPackageStudent_pkey" PRIMARY KEY ("InterventionPackageStudent_ID"),
    CONSTRAINT "CU_PackageStudent" UNIQUE ("Student_ID", "InterventionPackage_ID")
);

CREATE TABLE public."InterventionPackageResource" (
    "InterventionPackageResource_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "InterventionPackage_ID" bigint NOT NULL,
    "InterventionResource_ID" bigint NOT NULL,
    "SortOrder" smallint NOT NULL,
    CONSTRAINT "InterventionPackageResource_pkey" PRIMARY KEY ("InterventionPackageResource_ID"),
    CONSTRAINT "CU_PackageResource" UNIQUE ("InterventionPackage_ID", "InterventionResource_ID"),
    CONSTRAINT "CU_ResourceSortOrder" UNIQUE ("SortOrder", "InterventionPackage_ID"),
    CONSTRAINT "CK_SortOrder" CHECK (("SortOrder" > 0))
);

CREATE TABLE public."InterventionPackageTheory" (
    "InterventionPackageTheory_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "InterventionPackage_ID" bigint NOT NULL,
    "InterventionTheory_ID" bigint NOT NULL,
    "SortOrder" smallint NOT NULL,
    "SnSTitle" text NOT NULL,
    "SnSContent" text NOT NULL,
    "SnSSubskill_ID" bigint NOT NULL,
    CONSTRAINT "InterventionPackageTheory_pkey" PRIMARY KEY ("InterventionPackageTheory_ID"),
    CONSTRAINT "InterventionPackageTheory_InterventionPackage_ID_Interventi_key" UNIQUE ("InterventionPackage_ID", "InterventionTheory_ID"),
    CONSTRAINT "InterventionPackageTheory_InterventionPackage_ID_SortOrder_key" UNIQUE ("InterventionPackage_ID", "SortOrder"),
    CONSTRAINT "InterventionPackageTheory_SnSContent_check" CHECK ((length(btrim("SnSContent")) > 0)),
    CONSTRAINT "InterventionPackageTheory_SnSTitle_check" CHECK ((length(btrim("SnSTitle")) > 0)),
    CONSTRAINT "InterventionPackageTheory_SortOrder_check" CHECK (("SortOrder" > 0))
);

CREATE TABLE public."InterventionPackageQuestion" (
    "InterventionPackageQuestion_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "InterventionPackage_ID" bigint NOT NULL,
    "Question_ID" bigint NOT NULL,
    "QuestionNumber" smallint NOT NULL,
    "SnSQuestionText" text NOT NULL,
    "SnSOptionA" text NOT NULL,
    "SnSOptionB" text NOT NULL,
    "SnSOptionC" text NOT NULL,
    "SnSOptionD" text NOT NULL,
    "SnSAnswerKey" character(1) NOT NULL,
    "SnSExplanation" text NOT NULL,
    "SnSSubskill_ID" bigint NOT NULL,
    CONSTRAINT "InterventionPackageQuestion_pkey" PRIMARY KEY ("InterventionPackageQuestion_ID"),
    CONSTRAINT "InterventionPackageQuestion_InterventionPackage_ID_Questio_key1" UNIQUE ("InterventionPackage_ID", "QuestionNumber"),
    CONSTRAINT "InterventionPackageQuestion_InterventionPackage_ID_Question_key" UNIQUE ("InterventionPackage_ID", "Question_ID"),
    CONSTRAINT "InterventionPackageQuestion_QuestionNumber_check" CHECK (("QuestionNumber" > 0)),
    CONSTRAINT "InterventionPackageQuestion_SnSAnswerKey_check" CHECK (("SnSAnswerKey" = ANY (ARRAY['A'::bpchar, 'B'::bpchar, 'C'::bpchar, 'D'::bpchar]))),
    CONSTRAINT "InterventionPackageQuestion_SnSOptionA_check" CHECK ((length(btrim("SnSOptionA")) > 0)),
    CONSTRAINT "InterventionPackageQuestion_SnSOptionB_check" CHECK ((length(btrim("SnSOptionB")) > 0)),
    CONSTRAINT "InterventionPackageQuestion_SnSOptionC_check" CHECK ((length(btrim("SnSOptionC")) > 0)),
    CONSTRAINT "InterventionPackageQuestion_SnSOptionD_check" CHECK ((length(btrim("SnSOptionD")) > 0)),
    CONSTRAINT "InterventionPackageQuestion_SnSQuestionText_check" CHECK ((length(btrim("SnSQuestionText")) > 0))
);

CREATE TABLE public."InterventionPackageFollowup" (
    "InterventionPackageFollowup_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "FollowupAssessment_ID" bigint NOT NULL,
    "InterventionPackage_ID" bigint NOT NULL,
    "FollowupNote" text,
    CONSTRAINT "InterventionPackageFollowup_pkey" PRIMARY KEY ("InterventionPackageFollowup_ID"),
    CONSTRAINT "CU_PackageFollowupAssessment" UNIQUE ("InterventionPackage_ID", "FollowupAssessment_ID")
);

CREATE TABLE public."LearningObservation" (
    "LearningObservation_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Student_ID" bigint NOT NULL,
    "Class_ID" bigint NOT NULL,
    "PeriodStart" date NOT NULL,
    "PeriodEnd" date NOT NULL,
    "TaskCompletion" character varying(20),
    "Participation" character varying(20),
    "Preparation" character varying(20),
    "Cooperation" character varying(20),
    "RubricVersion" smallint DEFAULT 1 NOT NULL,
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "RowVersion" bigint DEFAULT 1 NOT NULL,
    CONSTRAINT "LearningObservation_pkey" PRIMARY KEY ("LearningObservation_ID"),
    CONSTRAINT "UQ_LearningObservation_AuthorPeriod" UNIQUE ("Student_ID", "Class_ID", "PeriodStart", "PeriodEnd", "CreatedByUser_ID"),
    CONSTRAINT "CK_LearningObservation_Cooperation" CHECK ((("Cooperation")::text = ANY ((ARRAY['Positive'::character varying, 'NeedsSupport'::character varying])::text[]))),
    CONSTRAINT "CK_LearningObservation_Participation" CHECK ((("Participation")::text = ANY ((ARRAY['Proactive'::character varying, 'WithPrompt'::character varying, 'Limited'::character varying])::text[]))),
    CONSTRAINT "CK_LearningObservation_Period" CHECK (("PeriodStart" <= "PeriodEnd")),
    CONSTRAINT "CK_LearningObservation_Preparation" CHECK ((("Preparation")::text = ANY ((ARRAY['Full'::character varying, 'Partial'::character varying, 'NotPrepared'::character varying])::text[]))),
    CONSTRAINT "CK_LearningObservation_Rubric" CHECK (("RubricVersion" = 1)),
    CONSTRAINT "CK_LearningObservation_Task" CHECK ((("TaskCompletion")::text = ANY ((ARRAY['Full'::character varying, 'Partial'::character varying, 'NotCompleted'::character varying])::text[]))),
    CONSTRAINT "CK_LearningObservation_Version" CHECK (("RowVersion" > 0))
);

CREATE TABLE public."StudentReportComment" (
    "StudentReportComment_ID" bigint GENERATED ALWAYS AS IDENTITY NOT NULL,
    "Student_ID" bigint NOT NULL,
    "Assessment_ID" bigint NOT NULL,
    "LearningObservation_ID" bigint,
    "BaseApprovedComment_ID" bigint,
    "RevisionNumber" integer NOT NULL,
    "Status" character varying(20) DEFAULT 'Draft'::character varying NOT NULL,
    "CommentText" text DEFAULT ''::text NOT NULL,
    "Source" character varying(20) DEFAULT 'Manual'::character varying NOT NULL,
    "AiDraftText" text,
    "AiModel" character varying(100),
    "PromptVersion" character varying(50),
    "GeneratedAt" timestamp with time zone,
    "InputSchemaVersion" smallint DEFAULT 1 NOT NULL,
    "InputSnapshot" jsonb NOT NULL,
    "InputFingerprint" character(64) NOT NULL,
    "CreatedByUser_ID" text NOT NULL,
    "UpdatedByUser_ID" text,
    "ApprovedByUser_ID" text,
    "CreatedAt" timestamp with time zone DEFAULT now() NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "ApprovedAt" timestamp with time zone,
    "RowVersion" bigint DEFAULT 1 NOT NULL,
    "ProcessCommentText" text,
    "AiProcessDraftText" text,
    CONSTRAINT "StudentReportComment_pkey" PRIMARY KEY ("StudentReportComment_ID"),
    CONSTRAINT "UQ_StudentReportComment_Revision" UNIQUE ("Student_ID", "Assessment_ID", "RevisionNumber"),
    CONSTRAINT "CK_StudentReportComment_Approval" CHECK ((((("Status")::text = ANY ((ARRAY['Draft'::character varying, 'Discarded'::character varying])::text[])) AND ("ApprovedByUser_ID" IS NULL) AND ("ApprovedAt" IS NULL)) OR ((("Status")::text = ANY ((ARRAY['Approved'::character varying, 'Superseded'::character varying])::text[])) AND ("ApprovedByUser_ID" IS NOT NULL) AND ("ApprovedAt" IS NOT NULL) AND (btrim("CommentText") <> ''::text)))),
    CONSTRAINT "CK_StudentReportComment_ProcessLength" CHECK (((("ProcessCommentText" IS NULL) OR (char_length("ProcessCommentText") <= 10000)) AND (("AiProcessDraftText" IS NULL) OR (char_length("AiProcessDraftText") <= 10000)))),
    CONSTRAINT "CK_StudentReportComment_ProcessManual" CHECK (((("Source")::text <> 'Manual'::text) OR ("AiProcessDraftText" IS NULL))),
    CONSTRAINT "CK_StudentReportComment_Source" CHECK ((((("Source")::text = 'Manual'::text) AND ("AiDraftText" IS NULL) AND ("AiModel" IS NULL) AND ("PromptVersion" IS NULL) AND ("GeneratedAt" IS NULL)) OR ((("Source")::text = 'Gemini'::text) AND ("AiDraftText" IS NOT NULL) AND ("AiModel" IS NOT NULL) AND ("PromptVersion" IS NOT NULL) AND ("GeneratedAt" IS NOT NULL)))),
    CONSTRAINT "StudentReportComment_AiDraftText_check" CHECK ((char_length("AiDraftText") <= 10000)),
    CONSTRAINT "StudentReportComment_CommentText_check" CHECK ((char_length("CommentText") <= 10000)),
    CONSTRAINT "StudentReportComment_InputFingerprint_check" CHECK (("InputFingerprint" ~ '^[0-9a-f]{64}$'::text)),
    CONSTRAINT "StudentReportComment_InputSchemaVersion_check" CHECK (("InputSchemaVersion" = 1)),
    CONSTRAINT "StudentReportComment_InputSnapshot_check" CHECK ((jsonb_typeof("InputSnapshot") = 'object'::text)),
    CONSTRAINT "StudentReportComment_RevisionNumber_check" CHECK (("RevisionNumber" > 0)),
    CONSTRAINT "StudentReportComment_RowVersion_check" CHECK (("RowVersion" > 0)),
    CONSTRAINT "StudentReportComment_Source_check" CHECK ((("Source")::text = ANY ((ARRAY['Manual'::character varying, 'Gemini'::character varying])::text[]))),
    CONSTRAINT "StudentReportComment_Status_check" CHECK ((("Status")::text = ANY ((ARRAY['Draft'::character varying, 'Approved'::character varying, 'Superseded'::character varying, 'Discarded'::character varying])::text[])))
);

CREATE TABLE auth."AspNetUsers" (
    "Id" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "MustChangePassword" boolean NOT NULL,
    "UserName" character varying(256),
    "NormalizedUserName" character varying(256),
    "Email" character varying(256),
    "NormalizedEmail" character varying(256),
    "EmailConfirmed" boolean NOT NULL,
    "PasswordHash" text,
    "SecurityStamp" text,
    "ConcurrencyStamp" text,
    "PhoneNumber" text,
    "PhoneNumberConfirmed" boolean NOT NULL,
    "TwoFactorEnabled" boolean NOT NULL,
    "LockoutEnd" timestamp with time zone,
    "LockoutEnabled" boolean NOT NULL,
    "AccessFailedCount" integer NOT NULL,
    CONSTRAINT "PK_AspNetUsers" PRIMARY KEY ("Id")
);

CREATE TABLE auth."AspNetRoles" (
    "Id" text NOT NULL,
    "Name" character varying(256),
    "NormalizedName" character varying(256),
    "ConcurrencyStamp" text,
    CONSTRAINT "PK_AspNetRoles" PRIMARY KEY ("Id")
);

CREATE TABLE auth."AspNetUserRoles" (
    "UserId" text NOT NULL,
    "RoleId" text NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId")
);

CREATE TABLE auth."AspNetUserClaims" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "UserId" text NOT NULL,
    "ClaimType" text,
    "ClaimValue" text,
    CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY ("Id")
);

CREATE TABLE auth."AspNetRoleClaims" (
    "Id" integer GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "RoleId" text NOT NULL,
    "ClaimType" text,
    "ClaimValue" text,
    CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY ("Id")
);

CREATE TABLE auth."AspNetUserLogins" (
    "LoginProvider" text NOT NULL,
    "ProviderKey" text NOT NULL,
    "ProviderDisplayName" text,
    "UserId" text NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey")
);

CREATE TABLE auth."AspNetUserTokens" (
    "UserId" text NOT NULL,
    "LoginProvider" text NOT NULL,
    "Name" text NOT NULL,
    "Value" text,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name")
);

CREATE TABLE auth."TeacherUserLinks" (
    "User_ID" text NOT NULL,
    "Teacher_ID" bigint NOT NULL,
    CONSTRAINT "PK_TeacherUserLinks" PRIMARY KEY ("User_ID")
);

CREATE TABLE auth."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- ---------------------------------------------------------------------
-- Khóa ngoại (53)
-- ---------------------------------------------------------------------
ALTER TABLE public."ClassMembership" ADD CONSTRAINT "ClassMembership_Class_ID_fkey" FOREIGN KEY ("Class_ID") REFERENCES "Class"("Class_ID");
ALTER TABLE public."ClassMembership" ADD CONSTRAINT "ClassMembership_Student_ID_fkey" FOREIGN KEY ("Student_ID") REFERENCES "Student"("Student_ID");
ALTER TABLE public."TeacherClassAssignment" ADD CONSTRAINT "TeacherClassAssignment_Class_ID_fkey" FOREIGN KEY ("Class_ID") REFERENCES "Class"("Class_ID");
ALTER TABLE public."TeacherClassAssignment" ADD CONSTRAINT "TeacherClassAssignment_Teacher_ID_fkey" FOREIGN KEY ("Teacher_ID") REFERENCES "Teacher"("Teacher_ID");
ALTER TABLE public."KnowledgeArea" ADD CONSTRAINT "KnowledgeArea_CurriculumUnit_ID_fkey" FOREIGN KEY ("CurriculumUnit_ID") REFERENCES "CurriculumUnit"("CurriculumUnit_ID");
ALTER TABLE public."Subskill" ADD CONSTRAINT "Subskill_KnowledgeArea_ID_fkey" FOREIGN KEY ("KnowledgeArea_ID") REFERENCES "KnowledgeArea"("KnowledgeArea_ID");
ALTER TABLE public."Question" ADD CONSTRAINT "Question_Subskill_ID_fkey" FOREIGN KEY ("Subskill_ID") REFERENCES "Subskill"("Subskill_ID");
ALTER TABLE public."QuestionInTest" ADD CONSTRAINT "QuestionInTest_Question_ID_fkey" FOREIGN KEY ("Question_ID") REFERENCES "Question"("Question_ID");
ALTER TABLE public."QuestionInTest" ADD CONSTRAINT "QuestionInTest_SnSSubskill_ID_fkey" FOREIGN KEY ("SnSSubskill_ID") REFERENCES "Subskill"("Subskill_ID");
ALTER TABLE public."QuestionInTest" ADD CONSTRAINT "QuestionInTest_Test_ID_fkey" FOREIGN KEY ("Test_ID") REFERENCES "Test"("Test_ID");
ALTER TABLE public."Assessment" ADD CONSTRAINT "Assessment_Class_ID_fkey" FOREIGN KEY ("Class_ID") REFERENCES "Class"("Class_ID");
ALTER TABLE public."Assessment" ADD CONSTRAINT "Assessment_Teacher_ID_fkey" FOREIGN KEY ("Teacher_ID") REFERENCES "Teacher"("Teacher_ID");
ALTER TABLE public."Assessment" ADD CONSTRAINT "Assessment_Test_ID_fkey" FOREIGN KEY ("Test_ID") REFERENCES "Test"("Test_ID");
ALTER TABLE public."ImportBatch" ADD CONSTRAINT "ImportBatch_Assessment_ID_fkey" FOREIGN KEY ("Assessment_ID") REFERENCES "Assessment"("Assessment_ID");
ALTER TABLE public."Attempt" ADD CONSTRAINT "Attempt_Assessment_ID_fkey" FOREIGN KEY ("Assessment_ID") REFERENCES "Assessment"("Assessment_ID");
ALTER TABLE public."Attempt" ADD CONSTRAINT "Attempt_ImportBatch_ID_fkey" FOREIGN KEY ("ImportBatch_ID") REFERENCES "ImportBatch"("ImportBatch_ID");
ALTER TABLE public."Attempt" ADD CONSTRAINT "Attempt_Student_ID_fkey" FOREIGN KEY ("Student_ID") REFERENCES "Student"("Student_ID");
ALTER TABLE public."Response" ADD CONSTRAINT "Response_Attempt_ID_fkey" FOREIGN KEY ("Attempt_ID") REFERENCES "Attempt"("Attempt_ID");
ALTER TABLE public."Response" ADD CONSTRAINT "Response_QuestionInTest_ID_fkey" FOREIGN KEY ("QuestionInTest_ID") REFERENCES "QuestionInTest"("QuestionInTest_ID");
ALTER TABLE public."InterventionResource" ADD CONSTRAINT "InterventionResource_Subskill_ID_fkey" FOREIGN KEY ("Subskill_ID") REFERENCES "Subskill"("Subskill_ID");
ALTER TABLE public."InterventionTheory" ADD CONSTRAINT "InterventionTheory_Subskill_ID_fkey" FOREIGN KEY ("Subskill_ID") REFERENCES "Subskill"("Subskill_ID");
ALTER TABLE public."InterventionPackage" ADD CONSTRAINT "InterventionPackage_Assessment_ID_fkey" FOREIGN KEY ("Assessment_ID") REFERENCES "Assessment"("Assessment_ID");
ALTER TABLE public."InterventionPackageStudent" ADD CONSTRAINT "InterventionPackageStudent_InterventionPackage_ID_fkey" FOREIGN KEY ("InterventionPackage_ID") REFERENCES "InterventionPackage"("InterventionPackage_ID");
ALTER TABLE public."InterventionPackageStudent" ADD CONSTRAINT "InterventionPackageStudent_Student_ID_fkey" FOREIGN KEY ("Student_ID") REFERENCES "Student"("Student_ID");
ALTER TABLE public."InterventionPackageResource" ADD CONSTRAINT "InterventionPackageResource_InterventionPackage_ID_fkey" FOREIGN KEY ("InterventionPackage_ID") REFERENCES "InterventionPackage"("InterventionPackage_ID");
ALTER TABLE public."InterventionPackageResource" ADD CONSTRAINT "InterventionPackageResource_InterventionResource_ID_fkey" FOREIGN KEY ("InterventionResource_ID") REFERENCES "InterventionResource"("InterventionResource_ID");
ALTER TABLE public."InterventionPackageTheory" ADD CONSTRAINT "InterventionPackageTheory_InterventionPackage_ID_fkey" FOREIGN KEY ("InterventionPackage_ID") REFERENCES "InterventionPackage"("InterventionPackage_ID");
ALTER TABLE public."InterventionPackageTheory" ADD CONSTRAINT "InterventionPackageTheory_InterventionTheory_ID_fkey" FOREIGN KEY ("InterventionTheory_ID") REFERENCES "InterventionTheory"("InterventionTheory_ID");
ALTER TABLE public."InterventionPackageTheory" ADD CONSTRAINT "InterventionPackageTheory_SnSSubskill_ID_fkey" FOREIGN KEY ("SnSSubskill_ID") REFERENCES "Subskill"("Subskill_ID");
ALTER TABLE public."InterventionPackageQuestion" ADD CONSTRAINT "InterventionPackageQuestion_InterventionPackage_ID_fkey" FOREIGN KEY ("InterventionPackage_ID") REFERENCES "InterventionPackage"("InterventionPackage_ID");
ALTER TABLE public."InterventionPackageQuestion" ADD CONSTRAINT "InterventionPackageQuestion_Question_ID_fkey" FOREIGN KEY ("Question_ID") REFERENCES "Question"("Question_ID");
ALTER TABLE public."InterventionPackageQuestion" ADD CONSTRAINT "InterventionPackageQuestion_SnSSubskill_ID_fkey" FOREIGN KEY ("SnSSubskill_ID") REFERENCES "Subskill"("Subskill_ID");
ALTER TABLE public."InterventionPackageFollowup" ADD CONSTRAINT "InterventionPackageFollowup_FollowupAssessment_ID_fkey" FOREIGN KEY ("FollowupAssessment_ID") REFERENCES "Assessment"("Assessment_ID");
ALTER TABLE public."InterventionPackageFollowup" ADD CONSTRAINT "InterventionPackageFollowup_InterventionPackage_ID_fkey" FOREIGN KEY ("InterventionPackage_ID") REFERENCES "InterventionPackage"("InterventionPackage_ID");
ALTER TABLE public."LearningObservation" ADD CONSTRAINT "LearningObservation_Class_ID_fkey" FOREIGN KEY ("Class_ID") REFERENCES "Class"("Class_ID");
ALTER TABLE public."LearningObservation" ADD CONSTRAINT "LearningObservation_CreatedByUser_ID_fkey" FOREIGN KEY ("CreatedByUser_ID") REFERENCES auth."AspNetUsers"("Id");
ALTER TABLE public."LearningObservation" ADD CONSTRAINT "LearningObservation_Student_ID_fkey" FOREIGN KEY ("Student_ID") REFERENCES "Student"("Student_ID");
ALTER TABLE public."LearningObservation" ADD CONSTRAINT "LearningObservation_UpdatedByUser_ID_fkey" FOREIGN KEY ("UpdatedByUser_ID") REFERENCES auth."AspNetUsers"("Id");
ALTER TABLE public."StudentReportComment" ADD CONSTRAINT "StudentReportComment_ApprovedByUser_ID_fkey" FOREIGN KEY ("ApprovedByUser_ID") REFERENCES auth."AspNetUsers"("Id");
ALTER TABLE public."StudentReportComment" ADD CONSTRAINT "StudentReportComment_Assessment_ID_fkey" FOREIGN KEY ("Assessment_ID") REFERENCES "Assessment"("Assessment_ID");
ALTER TABLE public."StudentReportComment" ADD CONSTRAINT "StudentReportComment_BaseApprovedComment_ID_fkey" FOREIGN KEY ("BaseApprovedComment_ID") REFERENCES "StudentReportComment"("StudentReportComment_ID");
ALTER TABLE public."StudentReportComment" ADD CONSTRAINT "StudentReportComment_CreatedByUser_ID_fkey" FOREIGN KEY ("CreatedByUser_ID") REFERENCES auth."AspNetUsers"("Id");
ALTER TABLE public."StudentReportComment" ADD CONSTRAINT "StudentReportComment_LearningObservation_ID_fkey" FOREIGN KEY ("LearningObservation_ID") REFERENCES "LearningObservation"("LearningObservation_ID");
ALTER TABLE public."StudentReportComment" ADD CONSTRAINT "StudentReportComment_Student_ID_fkey" FOREIGN KEY ("Student_ID") REFERENCES "Student"("Student_ID");
ALTER TABLE public."StudentReportComment" ADD CONSTRAINT "StudentReportComment_UpdatedByUser_ID_fkey" FOREIGN KEY ("UpdatedByUser_ID") REFERENCES auth."AspNetUsers"("Id");
ALTER TABLE auth."AspNetUserRoles" ADD CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES auth."AspNetRoles"("Id") ON DELETE CASCADE;
ALTER TABLE auth."AspNetUserRoles" ADD CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES auth."AspNetUsers"("Id") ON DELETE CASCADE;
ALTER TABLE auth."AspNetUserClaims" ADD CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES auth."AspNetUsers"("Id") ON DELETE CASCADE;
ALTER TABLE auth."AspNetRoleClaims" ADD CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES auth."AspNetRoles"("Id") ON DELETE CASCADE;
ALTER TABLE auth."AspNetUserLogins" ADD CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES auth."AspNetUsers"("Id") ON DELETE CASCADE;
ALTER TABLE auth."AspNetUserTokens" ADD CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES auth."AspNetUsers"("Id") ON DELETE CASCADE;
ALTER TABLE auth."TeacherUserLinks" ADD CONSTRAINT "FK_TeacherUserLinks_AspNetUsers_User_ID" FOREIGN KEY ("User_ID") REFERENCES auth."AspNetUsers"("Id") ON DELETE RESTRICT;
ALTER TABLE auth."TeacherUserLinks" ADD CONSTRAINT "FK_TeacherUserLinks_Teacher" FOREIGN KEY ("Teacher_ID") REFERENCES "Teacher"("Teacher_ID") ON DELETE RESTRICT;

-- ---------------------------------------------------------------------
-- Chỉ mục bổ sung (ngoài chỉ mục của khóa chính / ràng buộc duy nhất)
-- ---------------------------------------------------------------------
CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON auth."AspNetRoleClaims" ("RoleId");
CREATE UNIQUE INDEX "RoleNameIndex" ON auth."AspNetRoles" ("NormalizedName");
CREATE INDEX "IX_AspNetUserClaims_UserId" ON auth."AspNetUserClaims" ("UserId");
CREATE INDEX "IX_AspNetUserLogins_UserId" ON auth."AspNetUserLogins" ("UserId");
CREATE INDEX "IX_AspNetUserRoles_RoleId" ON auth."AspNetUserRoles" ("RoleId");
CREATE INDEX "EmailIndex" ON auth."AspNetUsers" ("NormalizedEmail");
CREATE UNIQUE INDEX "UserNameIndex" ON auth."AspNetUsers" ("NormalizedUserName");
CREATE UNIQUE INDEX "IX_TeacherUserLinks_Teacher_ID" ON auth."TeacherUserLinks" ("Teacher_ID");
CREATE INDEX "InterventionPackageQuestion_Question_ID_idx" ON public."InterventionPackageQuestion" ("Question_ID");
CREATE INDEX "InterventionPackageQuestion_SnSSubskill_ID_idx" ON public."InterventionPackageQuestion" ("SnSSubskill_ID");
CREATE INDEX "InterventionPackageTheory_InterventionTheory_ID_idx" ON public."InterventionPackageTheory" ("InterventionTheory_ID");
CREATE INDEX "InterventionPackageTheory_SnSSubskill_ID_idx" ON public."InterventionPackageTheory" ("SnSSubskill_ID");
CREATE INDEX "InterventionTheory_Subskill_ID_idx" ON public."InterventionTheory" ("Subskill_ID");
CREATE INDEX "IX_LearningObservation_ClassStudentPeriod" ON public."LearningObservation" ("Class_ID", "Student_ID", "PeriodEnd");
CREATE INDEX "IX_LearningObservation_Creator" ON public."LearningObservation" ("CreatedByUser_ID");
CREATE INDEX "IX_LearningObservation_Updater" ON public."LearningObservation" ("UpdatedByUser_ID");
CREATE INDEX "IX_StudentReportComment_Approver" ON public."StudentReportComment" ("ApprovedByUser_ID");
CREATE INDEX "IX_StudentReportComment_Assessment" ON public."StudentReportComment" ("Assessment_ID");
CREATE INDEX "IX_StudentReportComment_Base" ON public."StudentReportComment" ("BaseApprovedComment_ID");
CREATE INDEX "IX_StudentReportComment_Creator" ON public."StudentReportComment" ("CreatedByUser_ID");
CREATE INDEX "IX_StudentReportComment_Observation" ON public."StudentReportComment" ("LearningObservation_ID");
CREATE INDEX "IX_StudentReportComment_Updater" ON public."StudentReportComment" ("UpdatedByUser_ID");
CREATE UNIQUE INDEX "UQ_StudentReportComment_Approved" ON public."StudentReportComment" ("Student_ID", "Assessment_ID") WHERE (("Status")::text = 'Approved'::text);
CREATE UNIQUE INDEX "UQ_StudentReportComment_Draft" ON public."StudentReportComment" ("Student_ID", "Assessment_ID") WHERE (("Status")::text = 'Draft'::text);
