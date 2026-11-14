using Harmonia.Web.Services.Security;

using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Models.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Harmonia.Web.Services;
using Harmonia.Web.ViewModels;
using Harmonia.Web.Services.Import;

namespace Harmonia.Web.Controllers
{
    public class AssessmentsController : Controller
    {
        private readonly HarmoniaDbContext _db;
        private readonly AssessmentAnalysisService _analysis;
        private readonly AssessmentParticipationService _participation;
        private readonly IndividualDiagnosisService _diagnosis;
        private readonly InterventionManagementService? _interventions;
        private readonly StudentReportAccess? _reports;

        public AssessmentsController(HarmoniaDbContext db, AssessmentAnalysisService analysis,
            AssessmentParticipationService participation, IndividualDiagnosisService diagnosis,
            InterventionManagementService? interventions = null, StudentReportAccess? reports = null)
        {
            _db = db;
            _analysis = analysis;
            _participation = participation;
            _diagnosis = diagnosis;
            _interventions = interventions;
            _reports = reports;
        }

        public async Task<IActionResult> Index([FromServices] AssessmentImportService imports,
            [FromServices] BusinessAccess access, [FromServices] AuthoringService authoring,
            [FromServices] AuthoringAccess authoringAccess, CancellationToken cancellationToken)
        {
            if (authoringAccess.CanAccess(HttpContext))
            {
                var form = await authoring.GetAssessmentFormAsync(new(), cancellationToken);
                ViewData["AssessmentCreation"] = AssessmentCreationGuidanceViewModel.From(
                    form, User.IsInRole(HarmoniaRoles.Manager));
            }

            var assessments = await _db.Set<Assessment>()
                .AsNoTracking().Where(access.AssessmentScope())
                .OrderByDescending(a => a.AssessmentDate)
                .ToListAsync(cancellationToken);

            // Reuse the workflow's eligibility result instead of duplicating import rules in Razor.
            var rows = new List<AssessmentListItemViewModel>();
            foreach (var assessment in assessments)
            {
                var availability = await imports.GetPageAsync(assessment.AssessmentId, cancellationToken);
                rows.Add(new(assessment.AssessmentId, assessment.AssessmentName,
                    assessment.AssessmentDate, assessment.Status, availability));
            }
            return View(rows);
        }

        public async Task<IActionResult> SubskillSummary(long id, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid || id <= 0) return BadRequest();
            var model = await _analysis.GetSummaryAsync(id, cancellationToken);
            if (model is null) return NotFound();
            await SetNextStepsAsync(id, cancellationToken);
            return View(model);
        }

        public async Task<IActionResult> Overview(long id, ParticipationStatus status = ParticipationStatus.All,
            CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid || id <= 0 || !Enum.IsDefined(status)) return BadRequest();
            var model = await _participation.GetOverviewAsync(id, status, cancellationToken);
            if (model is null) return NotFound();
            await SetNextStepsAsync(id, cancellationToken);
            ViewData["StudentReportAllowed"] = _reports?.CanRead(HttpContext) == true;
            var plans = HttpContext.RequestServices.GetService<AssessmentFollowupPlanService>();
            if (plans is not null) model = model with { FollowupPlan = (await plans.GetAsync(id, cancellationToken))?.Summary };
            return View(model);
        }

        [HttpGet("/Assessments/{assessmentId:long}/Students/{studentId:long}", Name = "IndividualDiagnosis")]
        public async Task<IActionResult> IndividualDiagnosis(long assessmentId, long studentId,
            CancellationToken cancellationToken = default)
        {
            if (!ModelState.IsValid || assessmentId <= 0 || studentId <= 0) return BadRequest();
            var model = await _diagnosis.GetDiagnosisAsync(assessmentId, studentId, cancellationToken);
            if (model is null) return NotFound();
            ViewData["StudentReportAllowed"] = HttpContext?.RequestServices.GetService<StudentReportAccess>()?.CanRead(HttpContext) == true;
            await SetNextStepsAsync(assessmentId, cancellationToken);
            return View(model);
        }

        public async Task<IActionResult> QuestionDetail(long id, long subskillId, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid || id <= 0 || subskillId <= 0) return BadRequest();
            var model = await _analysis.GetQuestionsAsync(id, subskillId, cancellationToken);
            if (model is null) return NotFound();
            return View(model);
        }

        private async Task SetNextStepsAsync(long assessmentId, CancellationToken ct)
        {
            if (_interventions?.CanWrite != true) return;
            // The existing creation choices already enforce source status and Teacher write scope.
            var form = await _interventions.GetCreateAsync(new() { AssessmentId = assessmentId }, ct);
            if (form.Assessments.Any(a => a.AssessmentId == assessmentId))
                ViewData["InterventionAssessmentId"] = assessmentId;
        }
    }
}
