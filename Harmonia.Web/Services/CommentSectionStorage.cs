using Harmonia.Web.Data;
using Microsoft.EntityFrameworkCore;
namespace Harmonia.Web.Services;

// Unmapped, parameterized projections keep flag-OFF hosts compatible with the old schema.
// Called only inside the same scoped transaction as the main comment read/write.
internal sealed record StoredCommentSections(long Id,string? ProcessCommentText,string? AiProcessDraftText);
internal static class CommentSectionStorage
{
    public static Task<List<StoredCommentSections>> ReadAsync(HarmoniaDbContext db,long assessmentId,long studentId,CancellationToken ct) =>
        db.Database.SqlQuery<StoredCommentSections>($"""
            SELECT "StudentReportComment_ID" AS "Id","ProcessCommentText","AiProcessDraftText"
            FROM public."StudentReportComment" WHERE "Assessment_ID"={assessmentId} AND "Student_ID"={studentId}
            """).ToListAsync(ct);
    public static Task<string?> ReadPublicAsync(HarmoniaDbContext db,long id,CancellationToken ct) =>
        db.Database.SqlQuery<string>($"""
            SELECT "ProcessCommentText" AS "Value" FROM public."StudentReportComment" WHERE "StudentReportComment_ID"={id}
            """).SingleOrDefaultAsync(ct);
    public static async Task WriteAsync(HarmoniaDbContext db,long id,long version,string process,string? aiProcess,CancellationToken ct)
    {
        var count=await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public."StudentReportComment" SET "ProcessCommentText"={process},"AiProcessDraftText"={aiProcess}
            WHERE "StudentReportComment_ID"={id} AND "RowVersion"={version} AND "Status"='Draft'
            """,ct);
        if(count!=1)throw new DbUpdateConcurrencyException("Narrative section changed");
    }
}
