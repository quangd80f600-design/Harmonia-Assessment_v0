-- HARMONIA S1–S4 ALL QUERIES | archived 2026-11-05
-- Query Tool will show multiple result sets. Prefer running one query at a time.


-- ================================================================
-- FILE: S1_IndividualDiagnosis.sql
-- ================================================================
-- HARMONIA ASSESSMENT V0
-- S1 — Individual Diagnosis (diagnosis by Subskill for one student)
-- TEST INPUT: HMD-HS002; Assessment 01
-- Change values in WHERE for manual testing in pgAdmin.

SELECT
    st."StudentCode",
    st."FullName",
    a."AssessmentName",
    ss."SubskillCode",
    ss."SubskillName",
    COUNT(r."Response_ID") AS answered_questions,
    COUNT(r."Response_ID") FILTER (
        WHERE r."IsCorrect" = TRUE
    ) AS correct_questions,
    ROUND(
        100.0 * COUNT(r."Response_ID") FILTER (
            WHERE r."IsCorrect" = TRUE
        ) / NULLIF(COUNT(r."Response_ID"), 0),
        2
    ) AS accuracy_pct

FROM public."Attempt" AS att

INNER JOIN public."Student" AS st
    ON att."Student_ID" = st."Student_ID"

INNER JOIN public."Assessment" AS a
    ON att."Assessment_ID" = a."Assessment_ID"

INNER JOIN public."Response" AS r
    ON att."Attempt_ID" = r."Attempt_ID"

INNER JOIN public."QuestionInTest" AS qit
    ON r."QuestionInTest_ID" = qit."QuestionInTest_ID"
    AND qit."Test_ID" = a."Test_ID"

INNER JOIN public."Subskill" AS ss
    ON qit."SnSSubskill_ID" = ss."Subskill_ID"

WHERE
    st."StudentCode" = 'HMD-HS002'
    AND a."AssessmentName" = 'HMD - 9A1 - Assessment 01'

GROUP BY
    st."Student_ID", st."StudentCode", st."FullName",
    a."Assessment_ID", a."AssessmentName",
    ss."Subskill_ID", ss."SubskillCode", ss."SubskillName"

ORDER BY accuracy_pct ASC, ss."SubskillCode" ASC;


-- ================================================================
-- FILE: S2_1_ClassroomSummary.sql
-- ================================================================
-- HARMONIA ASSESSMENT V0
-- S2.1 — Classroom Analysis: Subskill Summary
-- One row per Assessment × Subskill. Ranked by incorrect Response rate.

SELECT
    c."ClassName",
    a."AssessmentName",
    ss."SubskillCode",
    ss."SubskillName",

    -- Questions in the Test assigned to this Subskill
    COUNT(DISTINCT qit."QuestionInTest_ID") AS question_count,

    -- Actual Response records (not the number of students)
    COUNT(r."Response_ID") AS total_responses,

    COUNT(r."Response_ID") FILTER (
        WHERE r."IsCorrect" = TRUE
    ) AS correct_responses,

    COUNT(r."Response_ID") FILTER (
        WHERE r."IsCorrect" = FALSE
    ) AS incorrect_responses,

    ROUND(
        100.0 * COUNT(r."Response_ID") FILTER (
            WHERE r."IsCorrect" = FALSE
        ) / NULLIF(COUNT(r."Response_ID"), 0),
        2
    ) AS error_pct

FROM public."Assessment" AS a

INNER JOIN public."Class" AS c
    ON a."Class_ID" = c."Class_ID"

INNER JOIN public."QuestionInTest" AS qit
    ON a."Test_ID" = qit."Test_ID"

INNER JOIN public."Subskill" AS ss
    ON qit."SnSSubskill_ID" = ss."Subskill_ID"

LEFT JOIN public."Attempt" AS att
    ON a."Assessment_ID" = att."Assessment_ID"

LEFT JOIN public."Response" AS r
    ON att."Attempt_ID" = r."Attempt_ID"
    AND qit."QuestionInTest_ID" = r."QuestionInTest_ID"

WHERE
    a."AssessmentName" = 'HMD - 9A1 - Assessment 01'

GROUP BY
    c."Class_ID",
    c."ClassName",
    a."Assessment_ID",
    a."AssessmentName",
    ss."Subskill_ID",
    ss."SubskillCode",
    ss."SubskillName"

ORDER BY
    error_pct DESC NULLS LAST,
    incorrect_responses DESC,
    ss."SubskillCode" ASC;


-- ================================================================
-- FILE: S2_2_QuestionDrilldown.sql
-- ================================================================
-- HARMONIA ASSESSMENT V0
-- S2.2 — Classroom Analysis: Question Error Detail
-- One row per QuestionInTest in the selected Subskill.
-- Seed example: HMD-U3-COND-MODAL, questions 33–40 in Assessment 01.

SELECT
    ss."SubskillName",
    qit."QuestionInTest_ID",
    qit."QuestionNumber",
    qit."SnSQuestionText",
    qit."SnSAnswerKey",

    COUNT(r."Response_ID") AS total_responses,

    COUNT(r."Response_ID") FILTER (
        WHERE r."IsCorrect" = FALSE
    ) AS incorrect_responses,

    ROUND(
        100.0 * COUNT(r."Response_ID") FILTER (
            WHERE r."IsCorrect" = FALSE
        ) / NULLIF(COUNT(r."Response_ID"), 0),
        2
    ) AS error_pct

FROM public."Assessment" AS a

INNER JOIN public."QuestionInTest" AS qit
    ON a."Test_ID" = qit."Test_ID"

INNER JOIN public."Subskill" AS ss
    ON qit."SnSSubskill_ID" = ss."Subskill_ID"

LEFT JOIN public."Attempt" AS att
    ON a."Assessment_ID" = att."Assessment_ID"

LEFT JOIN public."Response" AS r
    ON att."Attempt_ID" = r."Attempt_ID"
    AND qit."QuestionInTest_ID" = r."QuestionInTest_ID"

WHERE
    a."AssessmentName" = 'HMD - 9A1 - Assessment 01'
    AND ss."SubskillCode" = 'HMD-U3-COND-MODAL'

GROUP BY
    ss."Subskill_ID",
    ss."SubskillName",
    qit."QuestionInTest_ID",
    qit."QuestionNumber",
    qit."SnSQuestionText",
    qit."SnSAnswerKey"

ORDER BY
    error_pct DESC NULLS LAST,
    incorrect_responses DESC,
    qit."QuestionNumber" ASC;


-- ================================================================
-- FILE: S3_InterventionFollowupBySubskill.sql
-- ================================================================
-- HARMONIA ASSESSMENT V0
-- S3 — Intervention Follow-up, per Student × target Subskill
-- V0 rule: each InterventionPackage may contain resources spanning multiple
-- Subskills; every student assigned a package gets the full target set.
-- For different groups' target sets, create different packages.
-- Comparison is observational; do not claim causal impact.

WITH target_students AS (
    SELECT
        p."InterventionPackage_ID" AS package_id,
        p."Assessment_ID" AS before_id,
        f."FollowupAssessment_ID" AS after_id,
        ps."Student_ID" AS student_id
    FROM public."InterventionPackage" p
    JOIN public."InterventionPackageStudent" ps
        ON ps."InterventionPackage_ID" =
           p."InterventionPackage_ID"
    JOIN public."InterventionPackageFollowup" f
        ON f."InterventionPackage_ID" =
           p."InterventionPackage_ID"
    WHERE p."PackageName" =
        'HMD-P01 - Relative pronouns and conditional modals'
),

target_subskills AS (
    SELECT DISTINCT
        pr."InterventionPackage_ID" AS package_id,
        ss."Subskill_ID" AS subskill_id,
        ss."SubskillCode",
        ss."SubskillName"
    FROM public."InterventionPackageResource" pr
    JOIN public."InterventionResource" ir
        ON ir."InterventionResource_ID" =
           pr."InterventionResource_ID"
    JOIN public."Subskill" ss
        ON ss."Subskill_ID" = ir."Subskill_ID"
),

student_results AS (
    SELECT
        ts.package_id,
        att."Student_ID" AS student_id,
        att."Assessment_ID" AS assessment_id,
        qit."SnSSubskill_ID" AS subskill_id,

        COUNT(r."Response_ID") AS total_responses,

        COUNT(r."Response_ID") FILTER (
            WHERE r."IsCorrect" = TRUE
        ) AS correct_responses

    FROM target_students ts

    JOIN public."Attempt" att
        ON att."Student_ID" = ts.student_id
        AND att."Assessment_ID" IN (
            ts.before_id, ts.after_id
        )

    JOIN public."Assessment" a
        ON a."Assessment_ID" = att."Assessment_ID"

    JOIN public."Response" r
        ON r."Attempt_ID" = att."Attempt_ID"

    JOIN public."QuestionInTest" qit
        ON qit."QuestionInTest_ID" =
           r."QuestionInTest_ID"
        AND qit."Test_ID" = a."Test_ID"

    JOIN target_subskills ss
        ON ss.package_id = ts.package_id
        AND ss.subskill_id = qit."SnSSubskill_ID"

    GROUP BY
        ts.package_id,
        att."Student_ID",
        att."Assessment_ID",
        qit."SnSSubskill_ID"
)

SELECT
    st."StudentCode",
    st."FullName",
    ss."SubskillCode",
    ss."SubskillName",

    b.correct_responses AS before_correct,
    b.total_responses AS before_total,

    af.correct_responses AS after_correct,
    af.total_responses AS after_total,

    ROUND(
        100.0 * b.correct_responses
        / NULLIF(b.total_responses, 0), 2
    ) AS before_pct,

    ROUND(
        100.0 * af.correct_responses
        / NULLIF(af.total_responses, 0), 2
    ) AS after_pct,

    ROUND(
        100.0 * (
            af.correct_responses::numeric
            / NULLIF(af.total_responses, 0)
            -
            b.correct_responses::numeric
            / NULLIF(b.total_responses, 0)
        ), 2
    ) AS delta_pp,

    CASE
        WHEN b.total_responses IS NULL
          OR b.total_responses = 0
            THEN 'Missing baseline'

        WHEN af.total_responses IS NULL
          OR af.total_responses = 0
            THEN 'Missing follow-up'

        WHEN af.correct_responses::numeric
             / af.total_responses
             >
             b.correct_responses::numeric
             / b.total_responses
            THEN 'Improved'

        WHEN af.correct_responses::numeric
             / af.total_responses
             =
             b.correct_responses::numeric
             / b.total_responses
            THEN 'Unchanged'

        ELSE 'Declined'
    END AS observed_change

FROM target_students ts

JOIN public."Student" st
    ON st."Student_ID" = ts.student_id

JOIN target_subskills ss
    ON ss.package_id = ts.package_id

LEFT JOIN student_results b
    ON b.package_id = ts.package_id
    AND b.student_id = ts.student_id
    AND b.subskill_id = ss.subskill_id
    AND b.assessment_id = ts.before_id

LEFT JOIN student_results af
    ON af.package_id = ts.package_id
    AND af.student_id = ts.student_id
    AND af.subskill_id = ss.subskill_id
    AND af.assessment_id = ts.after_id

ORDER BY
    st."StudentCode",
    ss."SubskillCode";


-- ================================================================
-- FILE: S4_1_AssessmentParticipationSummary.sql
-- ================================================================
-- HARMONIA ASSESSMENT V0
-- S4.1 — Assessment Participation Summary
-- Expected: AS01 = 30/30; AS02 = 29/30; missing is not score zero.

SELECT
    c."ClassName",
    a."AssessmentName",
    a."AssessmentDate",

    COUNT(DISTINCT cm."Student_ID")
        AS expected_students,

    COUNT(DISTINCT att."Student_ID")
        AS students_with_result,

    COUNT(DISTINCT cm."Student_ID")
    - COUNT(DISTINCT att."Student_ID")
        AS students_without_result

FROM public."Assessment" AS a

INNER JOIN public."Class" AS c
    ON a."Class_ID" = c."Class_ID"

INNER JOIN public."ClassMembership" AS cm
    ON cm."Class_ID" = a."Class_ID"
    AND cm."StartDate" <= a."AssessmentDate"
    AND (
        cm."EndDate" IS NULL
        OR cm."EndDate" > a."AssessmentDate"
    )

LEFT JOIN public."Attempt" AS att
    ON att."Assessment_ID" = a."Assessment_ID"
    AND att."Student_ID" = cm."Student_ID"

WHERE
    a."AssessmentName" IN (
        'HMD - 9A1 - Assessment 01',
        'HMD - 9A1 - Assessment 02'
    )

GROUP BY
    c."Class_ID",
    c."ClassName",
    a."Assessment_ID",
    a."AssessmentName",
    a."AssessmentDate"

ORDER BY a."AssessmentDate";


-- ================================================================
-- FILE: S4_2_StudentParticipationDetail.sql
-- ================================================================
-- HARMONIA ASSESSMENT V0
-- S4.2 — Student Participation Detail
-- In AS02: 30 rows, HS030 Missing with NULL Score; HS028/HS029 late import.

SELECT
    c."ClassName",
    a."AssessmentName",
    st."StudentCode",
    st."FullName",

    CASE
        WHEN att."Attempt_ID" IS NULL
            THEN 'Missing'
        ELSE 'Recorded'
    END AS result_status,

    att."Score",
    ib."OriginalFileName" AS import_batch_file

FROM public."Assessment" AS a

INNER JOIN public."Class" AS c
    ON a."Class_ID" = c."Class_ID"

INNER JOIN public."ClassMembership" AS cm
    ON cm."Class_ID" = a."Class_ID"
    AND cm."StartDate" <= a."AssessmentDate"
    AND (
        cm."EndDate" IS NULL
        OR cm."EndDate" > a."AssessmentDate"
    )

INNER JOIN public."Student" AS st
    ON cm."Student_ID" = st."Student_ID"

LEFT JOIN public."Attempt" AS att
    ON att."Assessment_ID" = a."Assessment_ID"
    AND att."Student_ID" = st."Student_ID"

LEFT JOIN public."ImportBatch" AS ib
    ON att."ImportBatch_ID" = ib."ImportBatch_ID"

WHERE
    a."AssessmentName" = 'HMD - 9A1 - Assessment 02'

ORDER BY
    CASE WHEN att."Attempt_ID" IS NULL
        THEN 0 ELSE 1 END,
    st."StudentCode";

