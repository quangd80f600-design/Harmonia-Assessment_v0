using Harmonia.Web.Services;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Harmonia.Web.Controllers;

[Route("Assessments/{assessmentId:long}/Students/{studentId:long}/Comment")]
public sealed class ParentCommentsController(ParentCommentService comments) : Controller
{
    private void PrivateResponse() => Response.OnStarting(() =>
    { Response.Headers.CacheControl = "no-store, no-cache, private"; return Task.CompletedTask; });

    [HttpGet("", Name = "StudentComment")]
    public async Task<IActionResult> Index(long assessmentId, long studentId, CancellationToken ct)
    {
        PrivateResponse();
        if (!comments.Enabled) return NotFound();
        if (!ModelState.IsValid || assessmentId <= 0 || studentId <= 0) return BadRequest();
        var model = await comments.GetAsync(assessmentId, studentId, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("Save"), ValidateAntiForgeryToken, RequestSizeLimit(131072)]
    public Task<IActionResult> Save(long assessmentId, long studentId, SaveParentCommentInput input, CancellationToken ct) =>
        Write(assessmentId, studentId, () => comments.SaveAsync(assessmentId, studentId, input, ct), input, ct);

    [HttpPost("Generate"), ValidateAntiForgeryToken, RequestSizeLimit(8192)]
    public Task<IActionResult> Generate(long assessmentId, long studentId, ParentCommentActionInput input,
        [FromServices] Harmonia.Web.Services.Gemini.GeminiCommentClient client,
        [FromServices] Microsoft.Extensions.Options.IOptions<Harmonia.Web.Services.Gemini.GeminiOptions> options,
        CancellationToken ct)
    {
        if (!options.Value.Enabled) return Task.FromResult<IActionResult>(NotFound());
        return Write(assessmentId, studentId, () => comments.GenerateAsync(assessmentId, studentId, input, client, options.Value.Model, ct), null, ct);
    }

    [HttpPost("AddObservation"),ValidateAntiForgeryToken,RequestSizeLimit(131072)]
    public async Task<IActionResult> AddObservation(long assessmentId,long studentId,InlineObservationInput input,
        [FromServices]LearningObservationService observations,CancellationToken ct)
    {
        PrivateResponse();
        if(!comments.Enabled||!observations.Enabled)return NotFound();
        if(assessmentId<=0||studentId<=0)return BadRequest();
        if(!ModelState.IsValid)
        {
            var page=await comments.GetAsync(assessmentId,studentId,ct);
            if(page is null)return NotFound();
            ViewData["CommentHasUnsavedChanges"]=true;ViewData["InlineObservationInput"]=input;
            Response.StatusCode=400;
            return View("Index",page with{Input=input.Draft,Errors=["Ngày hoặc nội dung ghi nhận không hợp lệ. Kiểm tra lại; nội dung nhận xét đang soạn được giữ nguyên."]});
        }
        try
        {
            var result=await comments.AddObservationAsync(assessmentId,studentId,input,observations,ct);
            if(result.Result.NotFound||result.Editor is null)return NotFound();
            ViewData["CommentHasUnsavedChanges"]=true;ViewData["InlineObservationInput"]=input;
            Response.StatusCode=result.Result.Conflict?409:result.Result.Succeeded?200:400;
            if(result.Result.Succeeded)ViewData["InlineObservationMessage"]=result.Result.Message;
            return View("Index",result.Editor with{Errors=result.Result.Succeeded?[]:[result.Result.Message]});
        }
        catch(Exception ex)when(ex is NpgsqlException or DbUpdateException)
        {return StatusCode(503,"Chưa xác nhận được kết quả lưu ghi nhận. Kiểm tra danh sách trước khi thử lại.");}
    }

    [HttpPost("Approve"), ValidateAntiForgeryToken, RequestSizeLimit(8192)]
    public Task<IActionResult> Approve(long assessmentId, long studentId, ParentCommentActionInput input, CancellationToken ct) =>
        Write(assessmentId, studentId, () => comments.ApproveAsync(assessmentId, studentId, input, ct), null, ct);

    [HttpPost("Discard"), ValidateAntiForgeryToken, RequestSizeLimit(8192)]
    public Task<IActionResult> Discard(long assessmentId, long studentId, ParentCommentActionInput input, CancellationToken ct) =>
        Write(assessmentId, studentId, () => comments.DiscardAsync(assessmentId, studentId, input, ct), null, ct);

    private async Task<IActionResult> Write(long assessmentId, long studentId, Func<Task<ParentCommentWriteResult>> write,
        SaveParentCommentInput? posted, CancellationToken ct)
    {
        PrivateResponse();
        if (!comments.Enabled) return NotFound();
        if (!ModelState.IsValid || assessmentId <= 0 || studentId <= 0) return BadRequest("Dữ liệu không hợp lệ. Tải lại trang rồi thử lại.");
        try
        {
            var result = await write();
            if (result.NotFound) return NotFound();
            if (result.Succeeded)
            { TempData["CommentMessage"] = result.Message; return RedirectToAction(nameof(Index), new { assessmentId, studentId }); }
            var model = await comments.GetAsync(assessmentId, studentId, ct);
            if (model is null) return NotFound();
            // Preserve typed text on validation errors; stale responses require a fresh GET before resubmission.
            model = model with { Errors = [result.Message], Input = posted ?? model.Input };
            Response.StatusCode = result.Conflict ? 409 : 400;
            return View("Index", model);
        }
        catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
        { return StatusCode(503, "Chưa xác nhận được kết quả lưu. Tải lại trang để kiểm tra trước khi thử lại."); }
    }
}
