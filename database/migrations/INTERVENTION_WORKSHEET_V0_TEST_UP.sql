-- Approved internal worksheets V0. TEST ONLY; no startup migration.
BEGIN;
DO $$ BEGIN
 IF current_database() NOT LIKE 'harmonia_worksheet_test_%' THEN
  RAISE EXCEPTION 'STOP: worksheet migration is restricted to a dedicated test database';
 END IF;
END $$;
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
COMMIT;
