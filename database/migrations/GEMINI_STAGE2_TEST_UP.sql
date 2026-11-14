-- Approved environment recovery only. Existing SQL guards remain unchanged.
BEGIN;
DO $$ BEGIN
 IF current_database() !~ '^harmonia_gemini_test_[0-9a-f]{10}$' OR current_setting('harmonia.gemini_target',true) IS DISTINCT FROM current_database() THEN
 RAISE EXCEPTION 'Dedicated verified Gemini test target required'; END IF;
END $$;
-- Unchanged DDL body from QUESTION_USAGE_PURPOSE_UP.sql
ALTER TABLE public."Question"
    ADD COLUMN "UsagePurpose" VARCHAR(20) NOT NULL DEFAULT 'ForTest',
    ADD CONSTRAINT "CK_Question_UsagePurpose"
        CHECK ("UsagePurpose" IN ('ForTest', 'ForIntervention'));
-- Unchanged DDL body from ASSESSMENT_FOLLOWUP_PLAN_UP.sql
ALTER TABLE public."Assessment"
 ADD COLUMN "PlannedFollowupDate" date NULL,
 ADD CONSTRAINT "CK_Assessment_PlannedFollowupDate"
 CHECK ("PlannedFollowupDate" IS NULL OR "PlannedFollowupDate" > "AssessmentDate");
-- Unchanged DDL body from INTERVENTION_WORKSHEET_V0_TEST_UP.sql
CREATE TABLE public."InterventionTheory" (
 "InterventionTheory_ID" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "Subskill_ID" bigint NOT NULL REFERENCES public."Subskill"("Subskill_ID") ON DELETE NO ACTION,
 "Title" text NOT NULL CHECK (length(btrim("Title")) BETWEEN 1 AND 200),
 "Content" text NOT NULL CHECK (length(btrim("Content")) BETWEEN 1 AND 20000),
 "Status" varchar(20) NOT NULL DEFAULT 'Active' CHECK ("Status" IN ('Active','Inactive'))
);
CREATE INDEX ON public."InterventionTheory" ("Subskill_ID");
CREATE TABLE public."InterventionPackageTheory" (
 "InterventionPackageTheory_ID" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "InterventionPackage_ID" bigint NOT NULL REFERENCES public."InterventionPackage"("InterventionPackage_ID") ON DELETE NO ACTION,
 "InterventionTheory_ID" bigint NOT NULL REFERENCES public."InterventionTheory"("InterventionTheory_ID") ON DELETE NO ACTION,
 "SortOrder" smallint NOT NULL CHECK ("SortOrder" > 0),
 "SnSTitle" text NOT NULL CHECK (length(btrim("SnSTitle")) > 0),
 "SnSContent" text NOT NULL CHECK (length(btrim("SnSContent")) > 0),
 "SnSSubskill_ID" bigint NOT NULL REFERENCES public."Subskill"("Subskill_ID") ON DELETE NO ACTION,
 UNIQUE ("InterventionPackage_ID", "InterventionTheory_ID"),
 UNIQUE ("InterventionPackage_ID", "SortOrder")
);
CREATE INDEX ON public."InterventionPackageTheory" ("InterventionTheory_ID");
CREATE INDEX ON public."InterventionPackageTheory" ("SnSSubskill_ID");
CREATE TABLE public."InterventionPackageQuestion" (
 "InterventionPackageQuestion_ID" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "InterventionPackage_ID" bigint NOT NULL REFERENCES public."InterventionPackage"("InterventionPackage_ID") ON DELETE NO ACTION,
 "Question_ID" bigint NOT NULL REFERENCES public."Question"("Question_ID") ON DELETE NO ACTION,
 "QuestionNumber" smallint NOT NULL CHECK ("QuestionNumber" > 0),
 "SnSQuestionText" text NOT NULL CHECK (length(btrim("SnSQuestionText")) > 0),
 "SnSOptionA" text NOT NULL CHECK (length(btrim("SnSOptionA")) > 0),
 "SnSOptionB" text NOT NULL CHECK (length(btrim("SnSOptionB")) > 0),
 "SnSOptionC" text NOT NULL CHECK (length(btrim("SnSOptionC")) > 0),
 "SnSOptionD" text NOT NULL CHECK (length(btrim("SnSOptionD")) > 0),
 "SnSAnswerKey" char(1) NOT NULL CHECK ("SnSAnswerKey" IN ('A','B','C','D')),
 "SnSExplanation" text NOT NULL,
 "SnSSubskill_ID" bigint NOT NULL REFERENCES public."Subskill"("Subskill_ID") ON DELETE NO ACTION,
 UNIQUE ("InterventionPackage_ID", "Question_ID"),
 UNIQUE ("InterventionPackage_ID", "QuestionNumber")
);
CREATE INDEX ON public."InterventionPackageQuestion" ("Question_ID");
CREATE INDEX ON public."InterventionPackageQuestion" ("SnSSubskill_ID");

-- Unchanged DDL body from LEARNING_OBSERVATION_V0_TEST_UP.sql
CREATE TABLE public."LearningObservation" (
    "LearningObservation_ID" BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Student_ID" BIGINT NOT NULL REFERENCES public."Student"("Student_ID") ON DELETE NO ACTION,
    "Class_ID" BIGINT NOT NULL REFERENCES public."Class"("Class_ID") ON DELETE NO ACTION,
    "PeriodStart" DATE NOT NULL,
    "PeriodEnd" DATE NOT NULL,
    "TaskCompletion" VARCHAR(20) NULL,
    "Participation" VARCHAR(20) NULL,
    "Preparation" VARCHAR(20) NULL,
    "Cooperation" VARCHAR(20) NULL,
    "RubricVersion" SMALLINT NOT NULL DEFAULT 1,
    "CreatedByUser_ID" TEXT NOT NULL REFERENCES auth."AspNetUsers"("Id") ON DELETE NO ACTION,
    "UpdatedByUser_ID" TEXT NULL REFERENCES auth."AspNetUsers"("Id") ON DELETE NO ACTION,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT now(),
    "UpdatedAt" TIMESTAMPTZ NULL,
    "RowVersion" BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT "CK_LearningObservation_Period" CHECK ("PeriodStart" <= "PeriodEnd"),
    CONSTRAINT "CK_LearningObservation_Task" CHECK ("TaskCompletion" IN ('Full','Partial','NotCompleted')),
    CONSTRAINT "CK_LearningObservation_Participation" CHECK ("Participation" IN ('Proactive','WithPrompt','Limited')),
    CONSTRAINT "CK_LearningObservation_Preparation" CHECK ("Preparation" IN ('Full','Partial','NotPrepared')),
    CONSTRAINT "CK_LearningObservation_Cooperation" CHECK ("Cooperation" IN ('Positive','NeedsSupport')),
    CONSTRAINT "CK_LearningObservation_Rubric" CHECK ("RubricVersion" = 1),
    CONSTRAINT "CK_LearningObservation_Version" CHECK ("RowVersion" > 0),
    CONSTRAINT "UQ_LearningObservation_AuthorPeriod" UNIQUE ("Student_ID","Class_ID","PeriodStart","PeriodEnd","CreatedByUser_ID")
);
CREATE INDEX "IX_LearningObservation_ClassStudentPeriod" ON public."LearningObservation" ("Class_ID","Student_ID","PeriodEnd" DESC);
CREATE INDEX "IX_LearningObservation_Creator" ON public."LearningObservation" ("CreatedByUser_ID");
CREATE INDEX "IX_LearningObservation_Updater" ON public."LearningObservation" ("UpdatedByUser_ID");

-- Unchanged DDL body from STUDENT_REPORT_COMMENT_V0_TEST_UP.sql
CREATE TABLE public."StudentReportComment" (
    "StudentReportComment_ID" BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "Student_ID" BIGINT NOT NULL REFERENCES public."Student"("Student_ID") ON DELETE NO ACTION,
    "Assessment_ID" BIGINT NOT NULL REFERENCES public."Assessment"("Assessment_ID") ON DELETE NO ACTION,
    "LearningObservation_ID" BIGINT NULL REFERENCES public."LearningObservation"("LearningObservation_ID") ON DELETE NO ACTION,
    "BaseApprovedComment_ID" BIGINT NULL REFERENCES public."StudentReportComment"("StudentReportComment_ID") ON DELETE NO ACTION,
    "RevisionNumber" INTEGER NOT NULL CHECK ("RevisionNumber">0),
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Draft' CHECK ("Status" IN ('Draft','Approved','Superseded','Discarded')),
    "CommentText" TEXT NOT NULL DEFAULT '' CHECK (char_length("CommentText")<=10000),
    "Source" VARCHAR(20) NOT NULL DEFAULT 'Manual' CHECK ("Source" IN ('Manual','Gemini')),
    "AiDraftText" TEXT NULL CHECK (char_length("AiDraftText")<=10000),
    "AiModel" VARCHAR(100) NULL,
    "PromptVersion" VARCHAR(50) NULL,
    "GeneratedAt" TIMESTAMPTZ NULL,
    "InputSchemaVersion" SMALLINT NOT NULL DEFAULT 1 CHECK ("InputSchemaVersion"=1),
    "InputSnapshot" JSONB NOT NULL CHECK (jsonb_typeof("InputSnapshot")='object'),
    "InputFingerprint" CHAR(64) NOT NULL CHECK ("InputFingerprint" ~ '^[0-9a-f]{64}$'),
    "CreatedByUser_ID" TEXT NOT NULL REFERENCES auth."AspNetUsers"("Id") ON DELETE NO ACTION,
    "UpdatedByUser_ID" TEXT NULL REFERENCES auth."AspNetUsers"("Id") ON DELETE NO ACTION,
    "ApprovedByUser_ID" TEXT NULL REFERENCES auth."AspNetUsers"("Id") ON DELETE NO ACTION,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT now(),
    "UpdatedAt" TIMESTAMPTZ NULL,
    "ApprovedAt" TIMESTAMPTZ NULL,
    "RowVersion" BIGINT NOT NULL DEFAULT 1 CHECK ("RowVersion">0),
    CONSTRAINT "CK_StudentReportComment_Approval" CHECK (
      ("Status" IN ('Draft','Discarded') AND "ApprovedByUser_ID" IS NULL AND "ApprovedAt" IS NULL)
      OR ("Status" IN ('Approved','Superseded') AND "ApprovedByUser_ID" IS NOT NULL AND "ApprovedAt" IS NOT NULL AND btrim("CommentText")<>'')),
    CONSTRAINT "CK_StudentReportComment_Source" CHECK (
      ("Source"='Manual' AND "AiDraftText" IS NULL AND "AiModel" IS NULL AND "PromptVersion" IS NULL AND "GeneratedAt" IS NULL)
      OR ("Source"='Gemini' AND "AiDraftText" IS NOT NULL AND "AiModel" IS NOT NULL AND "PromptVersion" IS NOT NULL AND "GeneratedAt" IS NOT NULL)),
    CONSTRAINT "UQ_StudentReportComment_Revision" UNIQUE ("Student_ID","Assessment_ID","RevisionNumber")
);
CREATE UNIQUE INDEX "UQ_StudentReportComment_Draft" ON public."StudentReportComment" ("Student_ID","Assessment_ID") WHERE "Status"='Draft';
CREATE UNIQUE INDEX "UQ_StudentReportComment_Approved" ON public."StudentReportComment" ("Student_ID","Assessment_ID") WHERE "Status"='Approved';
CREATE INDEX "IX_StudentReportComment_Assessment" ON public."StudentReportComment" ("Assessment_ID");
CREATE INDEX "IX_StudentReportComment_Observation" ON public."StudentReportComment" ("LearningObservation_ID");
CREATE INDEX "IX_StudentReportComment_Base" ON public."StudentReportComment" ("BaseApprovedComment_ID");
CREATE INDEX "IX_StudentReportComment_Creator" ON public."StudentReportComment" ("CreatedByUser_ID");
CREATE INDEX "IX_StudentReportComment_Updater" ON public."StudentReportComment" ("UpdatedByUser_ID");
CREATE INDEX "IX_StudentReportComment_Approver" ON public."StudentReportComment" ("ApprovedByUser_ID");

COMMIT;
