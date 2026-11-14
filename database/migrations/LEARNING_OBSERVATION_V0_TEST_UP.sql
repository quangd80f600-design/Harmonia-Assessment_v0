-- Stage 1 ONLY. No parent-comment table, AI integration or demo migration.
-- Runner must first verify host/port/live database/cluster against its private manifest.
BEGIN;
DO $$ BEGIN
    IF current_database() !~ '^harmonia_observation_test_[0-9a-f]{10}$' THEN
        RAISE EXCEPTION 'Test target guard failed';
    END IF;
    IF to_regclass('public."LearningObservation"') IS NOT NULL THEN
        RAISE EXCEPTION 'Already applied; inspect expected schema rather than replay DDL';
    END IF;
END $$;
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
COMMIT;
