-- Approved 11/11/2026: test-only, one nullable date + one CHECK. No startup migration.
-- Runner must verify server/cluster and set harmonia.followup_plan_target on this session.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $$ BEGIN
 IF current_database() NOT LIKE 'harmonia_followup_plan_test_%'
    OR current_setting('harmonia.followup_plan_target', true) IS DISTINCT FROM current_database() THEN
  RAISE EXCEPTION 'STOP: verified dedicated follow-up plan test target required';
 END IF;
 IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public'
            AND table_name='Assessment' AND column_name='PlannedFollowupDate') THEN
  RAISE EXCEPTION 'STOP: PlannedFollowupDate already exists; inspect migration receipt';
 END IF;
END $$;
ALTER TABLE public."Assessment"
 ADD COLUMN "PlannedFollowupDate" date NULL,
 ADD CONSTRAINT "CK_Assessment_PlannedFollowupDate"
 CHECK ("PlannedFollowupDate" IS NULL OR "PlannedFollowupDate" > "AssessmentDate");
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM public."Assessment" WHERE "PlannedFollowupDate" IS NOT NULL) THEN
  RAISE EXCEPTION 'STOP: old assessments must keep a NULL planned date';
 END IF;
END $$;
COMMIT;
