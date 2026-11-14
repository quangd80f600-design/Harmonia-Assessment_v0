-- Stage 2 only, NEW independent parent-comment test database. No demo migration.
BEGIN;
DO $$ BEGIN
    IF current_database() !~ '^harmonia_parent_comment_test_[0-9a-f]{10}$' THEN RAISE EXCEPTION 'Test target guard failed'; END IF;
    IF to_regclass('public."LearningObservation"') IS NULL THEN RAISE EXCEPTION 'Stage 1 prerequisite missing'; END IF;
    IF to_regclass('public."StudentReportComment"') IS NOT NULL THEN RAISE EXCEPTION 'Already applied; verify expected schema rather than replay'; END IF;
END $$;
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
