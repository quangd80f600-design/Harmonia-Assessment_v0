using Harmonia.Web.Services;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Harmonia.Web.Controllers;

[Route("Worksheets")]
public sealed class WorksheetsController(WorksheetService service,WorksheetSubmissionStore tickets,CurrentActor actor,WorksheetGroupStore groups):Controller
{
    [HttpGet("Groups/{assessmentId:long}")]
    public async Task<IActionResult> ExactGroups([FromRoute]long assessmentId,CancellationToken ct)
    {
        if(!ModelState.IsValid || assessmentId<=0)return BadRequest();
        var page=await service.GetExactGroupsAsync(assessmentId,ct);
        return page is null?NotFound():View("ExactGroups",page);
    }
    [HttpGet("Group/{assessmentId:long}/{subskillId:long}")]
    public async Task<IActionResult> Group([FromRoute]long assessmentId,[FromRoute]long subskillId,[FromQuery]long[]? subskillIds,[FromQuery]string match="Any",CancellationToken ct=default)
    {
        if(!ModelState.IsValid||assessmentId<=0||subskillId<=0)return BadRequest();
        if(match is not ("Any" or "All" or "Exact") || subskillIds is {Length:>200})return BadRequest("Bộ lọc nhóm không hợp lệ.");
        var page=await service.GetGroupAsync(assessmentId,subskillId,new(){SubskillIds=subskillIds is {Length:>0}?subskillIds:[subskillId],MatchMode=match},ct);
        return page is null?NotFound():GroupView(page);
    }
    [HttpPost("Group/{assessmentId:long}/{subskillId:long}"),ValidateAntiForgeryToken,RequestFormLimits(ValueCountLimit=2200)]
    public async Task<IActionResult> Group([FromRoute]long assessmentId,[FromRoute]long subskillId,string? submissionToken,[Bind(Prefix="Input")]WorksheetGroupInput input,CancellationToken ct)
    {
        if(assessmentId<=0||subskillId<=0)return BadRequest();
        if(!service.CanWrite)return NotFound();
        if(ModelState.IsValid)
        {
            if(!tickets.Take(submissionToken,actor.PreviewOwner("worksheet"),$"group:{assessmentId}:{subskillId}"))return Conflict("Phiên lưu đã hết hạn hoặc đã sử dụng. Hãy kiểm tra danh sách gói trước khi mở lại trang soạn.");
            try
            {
                var r=await service.SaveGroupAsync(assessmentId,subskillId,input,ct);
                if(r.Result.Succeeded&&r.Draft is not null){groups.Save(actor.PreviewOwner("worksheet"),r.Draft);return RedirectToAction(nameof(Review),new{id=r.Draft.PackageId});}
                ModelState.AddModelError("",r.Result.Message);
            }
            catch(Exception ex)when(ex is NpgsqlException or DbUpdateException){ModelState.AddModelError("","Chưa xác nhận được kết quả lưu. Hãy kiểm tra danh sách gói trước khi thử lại.");}
        }
        var page=await service.GetGroupAsync(assessmentId,subskillId,input,ct);
        return page is null?NotFound():GroupView(page);
    }
    private IActionResult GroupView(WorksheetGroupPage page)
    {
        ViewData["SubmissionToken"]=tickets.Issue(actor.PreviewOwner("worksheet"),$"group:{page.Content.AssessmentId}:{page.Content.Skills[0].Id}");
        return View("Group",page);
    }
    [HttpGet("{id:long}/Review")]
    public async Task<IActionResult> Review([FromRoute]long id,CancellationToken ct)
    {
        if(!ModelState.IsValid||id<=0)return BadRequest();
        if(!service.CanWrite)return NotFound();
        var pending=groups.Get(actor.PreviewOwner("worksheet"),id);
        if(pending is null)return StatusCode(410,"Phiên chọn nhóm đã hết hạn hoặc đã sử dụng. Phiếu nháp vẫn được giữ; mở gói ở mục Gói can thiệp để kiểm tra và chọn lại người nhận.");
        var page=await service.GetGroupReviewAsync(pending.Value.Draft,ct);
        if(page is null)return NotFound();ViewData["GroupToken"]=pending.Value.Token;return View(page);
    }
    [HttpPost("{id:long}/Review"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Review([FromRoute]long id,[FromForm]string? groupToken,[FromForm]bool confirmed,CancellationToken ct)
    {
        if(!ModelState.IsValid||id<=0||!confirmed)return BadRequest("Hãy xác nhận đã kiểm tra nội dung và người nhận.");
        if(!service.CanWrite)return NotFound();
        var pending=groups.Take(actor.PreviewOwner("worksheet"),id,groupToken);
        if(pending is null)return Conflict("Phiên giao đã hết hạn hoặc đã sử dụng. Hãy kiểm tra gói trước khi thử lại.");
        try
        {
            var result=await service.ConfirmGroupAsync(pending,ct);TempData[result.Succeeded?"InterventionMessage":"WorksheetGroupError"]=result.Message;
            if(result.NotFound)return NotFound();
        }
        catch(Exception ex)when(ex is NpgsqlException or DbUpdateException){TempData["WorksheetGroupError"]="Chưa xác nhận được kết quả giao. Hãy kiểm tra danh sách học sinh trong gói; không tự gửi lại.";}
        return RedirectToAction("Details","Interventions",new{id});
    }
    [HttpGet("Theories")]
    public async Task<IActionResult> Theories(long? theoryId,[FromQuery]TheoryBrowseFilter filter,bool create=false,CancellationToken ct=default)
    {
        if(!ModelState.IsValid||theoryId is <=0)return BadRequest();
        var page=await service.GetTheoriesAsync(theoryId,null,ct);
        if(page is null)return NotFound();
        page=TheoryBrowsing.Apply(page,filter);
        if(theoryId is null && page.Filter.SubskillId is long selected)page.Input.SubskillId=selected;
        ViewData["OpenTheoryForm"]=create || theoryId.HasValue;
        return View(page);
    }
    [HttpPost("Theories"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Theories(long? theoryId,[Bind(Prefix="Input")]TheoryInput input,CancellationToken ct)
    {
        if(theoryId is <=0)return BadRequest();
        if(!service.Enabled)return NotFound();
        if(ModelState.IsValid)
        {
            try{var r=await service.SaveTheoryAsync(theoryId,input,ct);if(r.Succeeded){TempData["WorksheetMessage"]=r.Message;return RedirectToAction(nameof(Theories));}ModelState.AddModelError("",r.Message);}
            catch(Exception ex)when(ex is NpgsqlException or DbUpdateException){ModelState.AddModelError("","Chưa xác nhận được kết quả lưu. Hãy kiểm tra kho lý thuyết trước khi thử lại.");}
        }
        var page=await service.GetTheoriesAsync(theoryId,input,ct);return page is null?NotFound():View(page);
    }
    [HttpGet("Compose/{assessmentId:long}/{studentId:long}")]
    public async Task<IActionResult> Compose(long assessmentId,long studentId,long? packageId,CancellationToken ct)
    {
        if(!ModelState.IsValid||assessmentId<=0||studentId<=0||packageId is <=0)return BadRequest();
        var page=await service.GetComposerAsync(assessmentId,studentId,packageId,null,ct);
        return page is null?NotFound():ComposerView(page);
    }
    [HttpPost("Compose/{assessmentId:long}/{studentId:long}"),ValidateAntiForgeryToken,RequestFormLimits(ValueCountLimit=2000)]
    public async Task<IActionResult> Compose(long assessmentId,long studentId,long? packageId,string? submissionToken,[Bind(Prefix="Input")]WorksheetInput input,CancellationToken ct)
    {
        if(assessmentId<=0||studentId<=0||packageId is <=0)return BadRequest();
        if(!service.CanWrite)return NotFound();
        if(ModelState.IsValid)
        {
            if(!tickets.Take(submissionToken,actor.PreviewOwner("worksheet"),Context(assessmentId,studentId,packageId)))return Conflict("Phiên lưu đã hết hạn hoặc đã sử dụng. Hãy kiểm tra danh sách gói rồi mở lại trang soạn phiếu.");
            try
            {
                var r=await service.SaveAsync(assessmentId,studentId,packageId,input,ct);
                if(r.Succeeded){TempData["InterventionMessage"]=r.Message;TempData["WorksheetStudentId"]=studentId.ToString(System.Globalization.CultureInfo.InvariantCulture);return RedirectToAction("Details","Interventions",new{id=r.Id});}
                ModelState.AddModelError("",r.Message);
            }
            catch(Exception ex)when(ex is NpgsqlException or DbUpdateException){ModelState.AddModelError("","Chưa xác nhận được kết quả lưu phiếu. Hãy kiểm tra danh sách gói trước khi thử lại.");}
        }
        var page=await service.GetComposerAsync(assessmentId,studentId,packageId,input,ct);
        return page is null?NotFound():ComposerView(page);
    }
    private IActionResult ComposerView(WorksheetComposer page)
    {
        ViewData["SubmissionToken"]=tickets.Issue(actor.PreviewOwner("worksheet"),Context(page.AssessmentId,page.StudentId,page.PackageId));
        return View("Compose",page);
    }
    private static string Context(long a,long s,long? p)=>$"{a}:{s}:{p}";
    [HttpGet("{id:long}/Edit")]
    public async Task<IActionResult> Edit(long id,long studentId,CancellationToken ct)
    {
        if(!ModelState.IsValid||id<=0||studentId<=0)return BadRequest();
        var assessmentId=await service.GetEditAssessmentAsync(id,ct);
        return assessmentId is null?NotFound():RedirectToAction(nameof(Compose),new{assessmentId,studentId,packageId=id});
    }
    [HttpGet("{id:long}/Print")]
    public async Task<IActionResult> Print(long id,long? studentId,CancellationToken ct)
    {
        if(!ModelState.IsValid||id<=0||studentId is <=0)return BadRequest();
        var page=await service.GetPrintAsync(id,studentId,ct);return page is null?NotFound():View(page);
    }
    [HttpGet("{id:long}/Answers")]
    public async Task<IActionResult> Answers(long id,CancellationToken ct)
    {
        if(!ModelState.IsValid||id<=0)return BadRequest();
        var page=await service.GetAnswersAsync(id,ct);return page is null?NotFound():View(page);
    }
}
