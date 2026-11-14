using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace Harmonia.Web.Services;

public sealed partial class ParentCommentService
{
    public async Task<InlineObservationResult> AddObservationAsync(long assessmentId,long studentId,InlineObservationInput input,
        LearningObservationService observations,CancellationToken ct=default)
    {
        var page=await GetAsync(assessmentId,studentId,ct);
        if(page is null||!page.CanWrite||page.Draft is {CanEdit:false})return new(null,Missing());
        if(input.Draft.Text?.Length>10000 || input.Draft.ProcessText?.Length>10000)return new(page with{Input=input.Draft},new(false,"Nhận xét tối đa 10.000 ký tự. Chưa lưu ghi nhận."));
        if(input.Draft.DraftId!=page.Draft?.Id||input.Draft.ExpectedVersion!=page.Draft?.Version
            ||input.Draft.ExpectedApprovedId!=page.Approved?.Id)return new(page,Conflict());
        await using var db=await factory.CreateDbContextAsync(ct);
        var classId=await db.Assessments.Where(access.AssessmentWriteScope()).Where(a=>a.AssessmentId==assessmentId).Select(a=>(long?)a.ClassId).SingleOrDefaultAsync(ct);
        if(classId is null)return new(null,Missing());
        var result=await observations.SaveAsync(classId.Value,new(){PeriodStart=input.PeriodStart,PeriodEnd=input.PeriodEnd,
            Rows=[new(){Selected=true,StudentId=studentId,TaskCompletion=input.TaskCompletion,Participation=input.Participation,
                Preparation=input.Preparation,Cooperation=input.Cooperation}]},ct);
        var current=await GetAsync(assessmentId,studentId,ct);
        if(current is null)return new(null,Missing());
        var draft=input.Draft;
        if(result.Saved)
        {
            var selected=await db.LearningObservations.AsNoTracking().Where(o=>o.ClassId==classId&&o.StudentId==studentId
                &&o.CreatedByUserId==actor.RequiredId&&o.PeriodStart==input.PeriodStart&&o.PeriodEnd==input.PeriodEnd)
                .Select(o=>(long?)o.LearningObservationId).SingleOrDefaultAsync(ct);
            if(selected.HasValue&&current.Observations.Any(o=>o.Id==selected))draft.ObservationId=selected;
        }
        // Observation is independently saved through its existing service. Narrative stays unsaved until explicit Save.
        return new(current with{Input=draft},new(result.Saved,result.Saved
            ?"Đã thêm ghi nhận và chọn vào bản soạn. Kiểm tra nội dung rồi lưu bản nháp trước khi tạo gợi ý hoặc duyệt."
            :result.Message,result.NotFound,result.Conflict));
    }
}
