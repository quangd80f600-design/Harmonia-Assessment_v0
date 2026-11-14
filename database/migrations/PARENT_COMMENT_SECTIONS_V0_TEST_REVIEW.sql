-- Owner approved 12/11/2026: apply only on a verified NEW test clone. Never demo.
-- Adds only separate process narrative fields on a NEW test clone.
BEGIN;
DO $$ BEGIN
 IF current_database() !~ '^harmonia_report_ux_test_[0-9a-f]{10}$'
    OR current_setting('harmonia.report_sections_target',true) IS DISTINCT FROM current_database() THEN
   RAISE EXCEPTION 'Verified NEW report UX test clone required';
 END IF;
 IF to_regclass('public."StudentReportComment"') IS NULL THEN
   RAISE EXCEPTION 'Parent comment prerequisite missing';
 END IF;
END $$;
ALTER TABLE public."StudentReportComment"
 ADD COLUMN "ProcessCommentText" TEXT NULL,
 ADD COLUMN "AiProcessDraftText" TEXT NULL,
 ADD CONSTRAINT "CK_StudentReportComment_ProcessLength" CHECK (
   ("ProcessCommentText" IS NULL OR char_length("ProcessCommentText")<=10000)
   AND ("AiProcessDraftText" IS NULL OR char_length("AiProcessDraftText")<=10000)),
 ADD CONSTRAINT "CK_StudentReportComment_ProcessManual" CHECK (
   "Source"<>'Manual' OR "AiProcessDraftText" IS NULL);
COMMIT;
