-- Reviewed V0 change: only public.Question.UsagePurpose.
-- Execute using an explicitly verified target connection and ON_ERROR_STOP.
-- Initial rollout is test-only. Applying to demo requires a separate approved deployment.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
LOCK TABLE public."Question" IN ACCESS EXCLUSIVE MODE;

-- Fail rather than silently accepting a partially applied or different schema.
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema='public' AND table_name='Question' AND column_name='UsagePurpose') THEN
        RAISE EXCEPTION 'UsagePurpose already exists; inspect migration receipt before rerunning';
    END IF;
END $$;

-- PostgreSQL fills existing rows with the approved constant without changing IDs/snapshots.
ALTER TABLE public."Question"
    ADD COLUMN "UsagePurpose" VARCHAR(20) NOT NULL DEFAULT 'ForTest',
    ADD CONSTRAINT "CK_Question_UsagePurpose"
        CHECK ("UsagePurpose" IN ('ForTest', 'ForIntervention'));

DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM public."Question" WHERE "UsagePurpose" <> 'ForTest' OR "UsagePurpose" IS NULL) THEN
        RAISE EXCEPTION 'Backfill verification failed';
    END IF;
END $$;
COMMIT;
