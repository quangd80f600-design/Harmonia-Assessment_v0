using Harmonia.Web.Services;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Harmonia.Web.Controllers;

[Route("Authoring")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthoringController(AuthoringService service, AuthoringAccess access, TestWorkspaceService workspace) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers["Cache-Control"] = "no-store, private";
        if (!access.CanAccess(HttpContext)) context.Result = StatusCode(403, "Chức năng soạn đánh giá chưa bật hoặc tài khoản không có quyền.");
        base.OnActionExecuting(context);
    }
    [HttpGet("")]
    public IActionResult Index() => View();
    [HttpGet("Workspace/Taxonomy")]
    public async Task<IActionResult> WorkspaceTaxonomy([FromQuery] WorkspaceTreeFilter filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Bộ lọc kiến thức chưa hợp lệ.");
        var result = await workspace.NodesAsync(filter, ct);
        return result is null ? BadRequest("Hãy chọn lại nhánh kiến thức phù hợp.") : Json(result);
    }
    [HttpGet("Workspace/Skills")]
    public async Task<IActionResult> WorkspaceSkills([FromQuery] WorkspaceFilter filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Thông tin tìm kỹ năng chưa hợp lệ.");
        var result = await workspace.SearchSkillsAsync(filter, ct);
        return result is null ? BadRequest("Hãy chọn lại khối và từ khóa.") : Json(result);
    }
    [HttpGet("Workspace/Questions")]
    public async Task<IActionResult> WorkspaceQuestions([FromQuery] WorkspaceFilter filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("Bộ lọc câu hỏi chưa hợp lệ.");
        var result = await workspace.QuestionsAsync(filter, ct);
        return result is null ? BadRequest("Nhánh kiến thức không còn phù hợp. Hãy chọn lại bộ lọc; các câu đã chọn vẫn được giữ.") : Json(result);
    }
    [HttpGet("Taxonomy")]
    public async Task<IActionResult> Taxonomy([FromQuery] TaxonomyFilter filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var model = await service.GetTaxonomyAsync(filter, ct); return model is null ? BadRequest() : View(model);
    }
    [HttpGet("Questions")]
    public async Task<IActionResult> Questions([FromQuery] QuestionSearch filter, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (Request.Query.Count == 0) filter.Grade = 9;
        var model = await service.GetQuestionsAsync(filter, ct); return model is null ? BadRequest() : View(model);
    }
    [HttpGet("Questions/Create")]
    public async Task<IActionResult> CreateQuestion([FromQuery] long? subskillId, [FromQuery] string? usagePurpose, CancellationToken ct)
    {
        if (!ModelState.IsValid || subskillId is <= 0 || usagePurpose is not (null or "ForTest" or "ForIntervention")) return BadRequest();
        var model = await service.GetQuestionFormAsync(new() { SubskillId = subskillId ?? 0, UsagePurpose = usagePurpose ?? "ForTest" }, ct);
        return subskillId.HasValue && !model.Subskills.Any(s => s.Id == subskillId) ? NotFound() : View(model);
    }
    [HttpPost("Questions/Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuestion([Bind(Prefix = "Input")] QuestionInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await Save(() => service.CreateQuestionAsync(input, ct));
            if (result.Succeeded) { TempData["AuthoringMessage"] = result.Message; return RedirectToAction(nameof(Question), new { id = result.Id }); }
            ModelState.AddModelError("", result.Message);
        }
        return View(await service.GetQuestionFormAsync(input, ct));
    }
    [HttpGet("Questions/{id:long}")]
    public async Task<IActionResult> Question([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest(); var model = await service.GetQuestionAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }
    [HttpGet("Questions/{id:long}/Edit")]
    public async Task<IActionResult> EditQuestion([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var page = await service.GetQuestionEditAsync(id, ct);
        return page is null ? Conflict("Không thể mở biểu mẫu sửa câu hỏi. Câu hỏi có thể không còn tồn tại, đã thuộc một đề kiểm tra hoặc nằm ngoài quyền sửa của bạn. Hãy quay lại ngân hàng câu hỏi để kiểm tra.") : View(page);
    }
    [HttpGet("Questions/{id:long}/Copy")]
    public async Task<IActionResult> CopyQuestion([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var source = await service.GetQuestionAsync(id, ct);
        if (source is null) return NotFound();
        // This is a new Question in the shared bank, not a change to the source or its snapshots.
        var input = source.Question;
        ViewData["CopiedQuestionCode"] = source.Question.QuestionCode;
        input.QuestionCode = "";
        return View("CreateQuestion", await service.GetQuestionFormAsync(input, ct));
    }
    [HttpPost("Questions/{id:long}/Edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditQuestion([FromRoute] long id, [Bind(Prefix = "Input")] QuestionInput input, [FromForm] string? version, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();
        if (ModelState.IsValid)
        {
            var result = await Save(() => service.EditQuestionAsync(id, input, version, ct));
            if (result.Succeeded) { TempData["AuthoringMessage"] = result.Message; return RedirectToAction(nameof(Question), new { id }); }
            ModelState.AddModelError("", result.Message);
        }
        var form = await service.GetQuestionFormAsync(input, ct);
        return View(new QuestionEditPage(id, input, form.Subskills, version ?? ""));
    }
    [HttpGet("Tests")]
    public async Task<IActionResult> Tests(CancellationToken ct) => View(await service.GetTestsAsync(ct));
    [HttpGet("Tests/Create")]
    public async Task<IActionResult> CreateTest([FromQuery] short grade = 9, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || grade is < 6 or > 12 || search?.Length > 100) return BadRequest();
        return View(await service.GetTestFormAsync(new() { Grade = grade }, search, ct));
    }
    [HttpPost("Tests/Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTest([Bind(Prefix = "Input")] TestInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await Save(() => service.CreateTestAsync(input, ct));
            if (result.Succeeded) { TempData["AuthoringMessage"] = result.Message; return RedirectToAction(nameof(Test), new { id = result.Id }); }
            ModelState.AddModelError("", result.Message);
        }
        return await Compose(input, null, ct);
    }
    [HttpPost("Tests/Create/Filter"), HttpPost("Tests/{id:long}/Edit/Filter"), ValidateAntiForgeryToken]
    public async Task<IActionResult> FilterTest([FromRoute] long? id, [Bind(Prefix = "Input")] TestInput input, [FromForm] long? removeId, CancellationToken ct)
    {
        if (id is <= 0 || input.Grade is < 6 or > 12 || input.Search?.Length > 100 || input.Picks is null || input.Picks.Count > 500
            || input.Picks.Any(p => p is null || p.QuestionId <= 0 || p.Number is < 1 or > 200)) return BadRequest();
        if (ModelState.Any(p => (p.Key.StartsWith("Input.Picks") || p.Key is "Input.Grade" or "Input.Search" or "id" or "removeId") && p.Value!.Errors.Count != 0)) return BadRequest();
        if (id.HasValue && await service.GetTestEditAsync(id.Value, ct) is null) return Conflict("Đề không còn là Draft chưa dùng; không thể tiếp tục sửa.");
        if (removeId.HasValue)
        {
            input.Picks.RemoveAll(p => p.QuestionId == removeId.Value);
            short number = 0;
            foreach (var pick in input.Picks.Where(p => p.Number.HasValue).OrderBy(p => p.Number)) pick.Number = ++number;
        }
        // Filtering is not saving: an untitled/partially entered form must remain usable.
        ModelState.Clear();
        return await Compose(input, id, ct);
    }
    [HttpGet("Tests/{id:long}/Edit")]
    public async Task<IActionResult> EditTest([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var page = await service.GetTestEditAsync(id, ct);
        if (page is not null) ViewData["RetainedQuestionIds"] = await workspace.RetainedIdsAsync(id, ct);
        return page is null ? Conflict("Chỉ sửa được đề nháp chưa được sử dụng trong đợt đánh giá và chưa có dữ liệu trả lời.") : View("CreateTest", page);
    }
    [HttpPost("Tests/{id:long}/Edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTest([FromRoute] long id, [Bind(Prefix = "Input")] TestInput input, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();
        if (ModelState.IsValid)
        {
            var result = await Save(() => service.EditTestAsync(id, input, ct));
            if (result.Succeeded) { TempData["AuthoringMessage"] = result.Message; return RedirectToAction(nameof(Test), new { id }); }
            ModelState.AddModelError("", result.Message);
        }
        return await Compose(input, id, ct);
    }
    private async Task<IActionResult> Compose(TestInput input, long? id, CancellationToken ct)
    {
        // Rebuilding the candidate list changes numeric form indices; stale ModelState values must not overwrite the new rows.
        var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Có giá trị nhập không hợp lệ." : e.ErrorMessage).ToArray();
        ModelState.Clear(); foreach (var error in errors) ModelState.AddModelError("", error);
        if (id.HasValue) ViewData["RetainedQuestionIds"] = await workspace.RetainedIdsAsync(id.Value, ct);
        return View("CreateTest", (await service.GetTestFormAsync(input, ct: ct, editId: id)) with { EditId = id });
    }
    [HttpGet("Tests/{id:long}")]
    public async Task<IActionResult> Test([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest(); var model = await service.GetTestAsync(id, ct);
        return model is null ? NotFound() : View(model);
    }
    [HttpPost("Tests/{id:long}/Publish"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest();
        var result = await Save(() => service.PublishTestAsync(id, ct));
        if (result.Succeeded) { TempData["AuthoringMessage"] = result.Message; return RedirectToAction(nameof(Test), new { id }); }
        var model = await service.GetTestAsync(id, ct); if (model is null) return NotFound();
        ModelState.AddModelError("", result.Message); Response.StatusCode = 400; return View("Test", model);
    }
    [HttpGet("Tests/{id:long}/Copy")]
    public async Task<IActionResult> CopyTest([FromRoute] long id, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0) return BadRequest(); var source = await service.GetTestAsync(id, ct);
        return source is null ? NotFound() : View(new TestCopyPage(id, source.Name, new() { TestName = "Bản sao — " + source.Name[..Math.Min(source.Name.Length, 235)], Description = source.Description }));
    }
    [HttpPost("Tests/{id:long}/Copy"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyTest([FromRoute] long id, [Bind(Prefix = "Input")] CopyTestInput input, CancellationToken ct)
    {
        if (id <= 0) return BadRequest();
        if (ModelState.IsValid)
        {
            var result = await Save(() => service.CopyTestAsync(id, input, ct));
            if (result.Succeeded) { TempData["AuthoringMessage"] = result.Message; return RedirectToAction(nameof(Test), new { id = result.Id }); }
            ModelState.AddModelError("", result.Message);
        }
        var source = await service.GetTestAsync(id, ct);
        return source is null ? NotFound() : View(new TestCopyPage(id, source.Name, input));
    }
    [HttpGet("Assessments/Create")]
    public async Task<IActionResult> CreateAssessment([FromQuery] long? testId, CancellationToken ct, [FromQuery] long? classId = null)
    {
        if (!ModelState.IsValid || testId is <= 0 || classId is <= 0) return BadRequest();
        var page = await service.GetAssessmentFormAsync(new() {
            TestId = testId ?? 0, ClassId = classId ?? 0, AssessmentDate = AcademicManagementService.Today
        }, ct);
        if (classId.HasValue && !page.Classes.Any(c => c.Id == classId)) return NotFound();
        return View(page);
    }
    [HttpPost("Assessments/Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAssessment([Bind(Prefix = "Input")] AssessmentCreateInput input, CancellationToken ct)
    {
        if (ModelState.TryGetValue("Input.PlannedFollowupDate", out var invalidDate) && invalidDate.Errors.Count > 0)
        {
            invalidDate.Errors.Clear();
            invalidDate.Errors.Add("Ngày dự kiến đánh giá lại chưa hợp lệ. Hãy chọn ngày theo biểu mẫu.");
        }

        if (ModelState.IsValid)
        {
            var result = await Save(() => service.CreateAssessmentAsync(input, ct));
            if (result.Succeeded) { TempData["AuthoringMessage"] = result.Message; return RedirectToAction("Index", "AssessmentImports", new { id = result.Id }); }
            ModelState.AddModelError("", result.Message);
        }
        return View(await service.GetAssessmentFormAsync(input, ct));
    }
    private static async Task<AuthoringWriteResult> Save(Func<Task<AuthoringWriteResult>> action)
    {
        try { return await action(); }
        catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
        { return new(false, "Chưa xác nhận được kết quả lưu. Kiểm tra danh sách trước khi thử lại."); }
    }
}
