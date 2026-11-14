using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Services.Security;
using Harmonia.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Harmonia.Web.Services;

public sealed class LearningObservationOptions { public bool Enabled { get; set; } }

public sealed class LearningObservationService(IDbContextFactory<HarmoniaDbContext> factory,
    BusinessAccess access, CurrentActor actor, IOptions<LearningObservationOptions> options)
{
    public bool Enabled => options.Value.Enabled && actor.Id is not null;
    public static string? ValidatePeriod(DateOnly? start, DateOnly? end) => !start.HasValue || !end.HasValue
        ? "Chọn ngày bắt đầu và kết thúc ghi nhận."
        : start > end ? "Ngày kết thúc phải từ ngày bắt đầu trở đi."
        : end > AcademicManagementService.Today ? "Chỉ ghi nhận đến ngày hôm nay; không chọn ngày trong tương lai." : null;

    public async Task<LearningObservationPage?> GetAsync(long? classId, DateOnly start, DateOnly end,
        string? authorId = null, int page = 1, CancellationToken ct = default)
    {
        if (!Enabled || classId is <= 0 || page <= 0) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var classes = await db.Classes.AsNoTracking().Where(access.ClassScope()).OrderBy(c => c.ClassName).ThenBy(c => c.ClassId)
            .Select(c => new ObservationClass(c.ClassId, c.ClassName)).ToListAsync(ct);
        if (classId.HasValue && !classes.Any(c => c.Id == classId)) return null;
        var periodError = ValidatePeriod(start, end);
        LearningObservationPage Empty(IReadOnlyList<string> errors) => new(classes, classId, start, end, [], actor.RequiredId, [], page, 0, false, errors);
        if (!classId.HasValue || periodError is not null) return Empty(periodError is null ? [] : [periodError]);
        var id = classId.Value;
        var authors = await db.Database.SqlQuery<ObservationAuthor>($"""
            SELECT u."Id" AS "Id",COALESCE(t."FullName",u."UserName",'Người ghi nhận') AS "Name"
            FROM auth."AspNetUsers" u
            LEFT JOIN auth."TeacherUserLinks" l ON l."User_ID"=u."Id"
            LEFT JOIN public."Teacher" t ON t."Teacher_ID"=l."Teacher_ID"
            WHERE u."Id"={actor.RequiredId} OR u."Id" IN
              (SELECT o."CreatedByUser_ID" FROM public."LearningObservation" o WHERE o."Class_ID"={id})
            """).ToListAsync(ct);
        authorId = string.IsNullOrEmpty(authorId) ? actor.RequiredId : authorId;
        var notices = new List<string>();
        if (!authors.Any(a => a.Id == authorId))
        { authorId = actor.RequiredId; notices.Add("Người ghi đã chọn không có dữ liệu trong lớp này; đang hiển thị ghi nhận của bạn."); }
        var assignmentCovers = actor.IsManager || ObservationRubric.Covers(start, end,
            await db.TeacherClassAssignments.AsNoTracking().Where(a => a.ClassId == id && a.TeacherId == actor.TeacherId)
                .Select(a => new ObservationInterval(a.StartDate, a.EndDate)).ToListAsync(ct));
        var eligible = db.Students.AsNoTracking().Where(s => s.ClassMemberships.Any(m => m.ClassId == id
            && m.StartDate <= end && (m.EndDate == null || m.EndDate > start)));
        var count = await eligible.CountAsync(ct); var offset = ((long)page - 1) * LearningObservationPage.PageSize;
        var students = offset >= count ? [] : await eligible.OrderBy(s => s.StudentCode).ThenBy(s => s.StudentId)
            .Skip((int)offset).Take(LearningObservationPage.PageSize).Select(s => new { s.StudentId, s.StudentCode, s.FullName }).ToListAsync(ct);
        var ids = students.Select(s => s.StudentId).ToArray();
        var memberships = await db.ClassMemberships.AsNoTracking().Where(m => m.ClassId == id && ids.Contains(m.StudentId))
            .Select(m => new { m.StudentId, m.StartDate, m.EndDate }).ToListAsync(ct);
        var saved = await db.LearningObservations.AsNoTracking().Where(o => o.ClassId == id && ids.Contains(o.StudentId)
            && o.PeriodStart == start && o.PeriodEnd == end && o.CreatedByUserId == authorId).ToListAsync(ct);
        var rows = students.Select(s =>
        {
            var o = saved.SingleOrDefault(o => o.StudentId == s.StudentId);
            var full = ObservationRubric.Covers(start, end, memberships.Where(m => m.StudentId == s.StudentId).Select(m => new ObservationInterval(m.StartDate, m.EndDate)));
            var edit = full && assignmentCovers && (o is not null ? o.CreatedByUserId == actor.Id || actor.IsManager : authorId == actor.Id);
            return new ObservationStudent(s.StudentId, s.StudentCode, s.FullName, full, edit,
                new() { StudentId = s.StudentId, ObservationId = o?.LearningObservationId, ExpectedVersion = o?.RowVersion,
                    TaskCompletion = o?.TaskCompletion, Participation = o?.Participation, Preparation = o?.Preparation, Cooperation = o?.Cooperation }, o?.UpdatedAt ?? o?.CreatedAt);
        }).ToArray();
        return new(classes, id, start, end, authors.OrderBy(a => a.Name).ToArray(), authorId, rows, page, count, assignmentCovers, notices);
    }

    public async Task<ObservationSaveResult> SaveAsync(long classId, ObservationSaveInput input, CancellationToken ct = default)
    {
        if (!Enabled || classId <= 0) return new(false, "Dữ liệu không tồn tại hoặc ngoài phạm vi quyền.", NotFound: true);
        if (ValidatePeriod(input.PeriodStart, input.PeriodEnd) is string error) return new(false, error);
        if (input.Rows.Count > LearningObservationPage.PageSize || input.Page <= 0) return new(false, "Mỗi lần lưu tối đa 40 học sinh trên trang đang xem.");
        var rows = input.Rows.Where(r => r.Selected).ToArray();
        if (rows.Length == 0) return new(false, "Chọn ít nhất một dòng học sinh cần lưu.");
        if (rows.Any(r => r.StudentId <= 0 || r.ObservationId is <= 0) || rows.Select(r => r.StudentId).Distinct().Count() != rows.Length)
            return new(false, "Danh sách học sinh không hợp lệ hoặc có dòng bị lặp. Tải lại trang.");
        foreach (var r in rows)
        {
            r.TaskCompletion = ObservationRubric.Normalize(r.TaskCompletion); r.Participation = ObservationRubric.Normalize(r.Participation);
            r.Preparation = ObservationRubric.Normalize(r.Preparation); r.Cooperation = ObservationRubric.Normalize(r.Cooperation);
            if (!ObservationRubric.Valid(r.TaskCompletion, ObservationRubric.Task) || !ObservationRubric.Valid(r.Participation, ObservationRubric.Participation)
                || !ObservationRubric.Valid(r.Preparation, ObservationRubric.Preparation) || !ObservationRubric.Valid(r.Cooperation, ObservationRubric.Cooperation))
                return new(false, "Có lựa chọn ghi nhận không hợp lệ. Chọn lại theo các mục trong danh sách.");
        }
        var start = input.PeriodStart!.Value; var end = input.PeriodEnd!.Value;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        if (!await db.Classes.Where(access.ClassScope()).AnyAsync(c => c.ClassId == classId, ct))
            return new(false, "Dữ liệu không tồn tại hoặc ngoài phạm vi quyền.", NotFound: true);
        // Same Class lock as roster import/class deletion; share-lock temporal rows against date edits.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"Class\" WHERE \"Class_ID\"={classId} FOR UPDATE", ct);
        if (!actor.IsManager)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"TeacherClassAssignment\" WHERE \"Class_ID\"={classId} AND \"Teacher_ID\"={actor.TeacherId} ORDER BY \"TeacherClassAssignment_ID\" FOR SHARE", ct);
        if (!await db.Classes.Where(access.ClassScope()).AnyAsync(c => c.ClassId == classId, ct))
            return new(false, "Dữ liệu không tồn tại hoặc ngoài phạm vi quyền.", NotFound: true);
        if (!actor.IsManager && !ObservationRubric.Covers(start, end,
            await db.TeacherClassAssignments.Where(a => a.ClassId == classId && a.TeacherId == actor.TeacherId)
                .Select(a => new ObservationInterval(a.StartDate, a.EndDate)).ToListAsync(ct)))
            return new(false, "Giai đoạn ghi nhận phải nằm trong thời gian bạn được phân công phụ trách lớp.");
        var ids = rows.Select(r => r.StudentId).ToArray();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"ClassMembership\" WHERE \"Class_ID\"={classId} AND \"Student_ID\"=ANY({ids}) ORDER BY \"ClassMembership_ID\" FOR SHARE", ct);
        var memberships = await db.ClassMemberships.Where(m => m.ClassId == classId && ids.Contains(m.StudentId))
            .Select(m => new { m.StudentId, m.StartDate, m.EndDate }).ToListAsync(ct);
        var records = await db.LearningObservations.Where(o => o.ClassId == classId && ids.Contains(o.StudentId) && o.PeriodStart == start && o.PeriodEnd == end).ToListAsync(ct);
        var actions = new List<(ObservationInputRow Input, LearningObservation? Existing)>();
        foreach (var r in rows)
        {
            if (!ObservationRubric.Covers(start, end, memberships.Where(m => m.StudentId == r.StudentId).Select(m => new ObservationInterval(m.StartDate, m.EndDate))))
                return new(false, "Có học sinh không thuộc lớp trong toàn bộ giai đoạn. Chọn lại giai đoạn phù hợp; chưa lưu dòng nào.");
            var o = r.ObservationId.HasValue ? records.SingleOrDefault(o => o.LearningObservationId == r.ObservationId) : null;
            if (r.ObservationId.HasValue)
            {
                if (o is null || o.StudentId != r.StudentId) return new(false, "Ghi nhận không khớp học sinh hoặc giai đoạn. Tải lại trang.", Conflict: true);
                if (o.CreatedByUserId != actor.Id && !actor.IsManager) return new(false, "Bạn chỉ được sửa ghi nhận do mình tạo.");
                if (o.RowVersion != r.ExpectedVersion || o.RubricVersion != 1) return new(false, "Ghi nhận đã thay đổi ở phiên làm việc khác. Tải lại trang trước khi lưu.", Conflict: true);
            }
            else if ((!string.IsNullOrEmpty(input.AuthorId) && input.AuthorId != actor.Id) || r.ExpectedVersion.HasValue)
                return new(false, "Chọn ghi nhận của bạn để tạo mới; không tạo thay tên người khác.");
            else if (records.Any(o => o.StudentId == r.StudentId && o.CreatedByUserId == actor.Id))
                return new(false, "Ghi nhận đã được tạo ở phiên khác. Tải lại trang; không tạo thêm bản trùng.", Conflict: true);
            actions.Add((r, o));
        }
        var created = 0; var updated = 0; var unchanged = 0;
        foreach (var (r, old) in actions)
        {
            if (old is not null && old.TaskCompletion == r.TaskCompletion && old.Participation == r.Participation && old.Preparation == r.Preparation && old.Cooperation == r.Cooperation)
            { unchanged++; continue; }
            var o = old ?? new LearningObservation { ClassId = classId, StudentId = r.StudentId, PeriodStart = start, PeriodEnd = end, CreatedByUserId = actor.RequiredId };
            o.TaskCompletion = r.TaskCompletion; o.Participation = r.Participation; o.Preparation = r.Preparation; o.Cooperation = r.Cooperation;
            if (old is null) { db.LearningObservations.Add(o); created++; }
            else { o.RowVersion++; o.UpdatedAt = DateTime.UtcNow; o.UpdatedByUserId = actor.RequiredId; updated++; }
        }
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { return new(false, "Ghi nhận đã thay đổi. Tải lại trang trước khi lưu.", Conflict: true); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { return new(false, "Ghi nhận đã tồn tại. Tải lại trang để xem bản hiện tại.", Conflict: true); }
        return new(true, $"Đã tạo {created}, cập nhật {updated} ghi nhận; {unchanged} dòng giữ nguyên.", Created: created, Updated: updated, Unchanged: unchanged);
    }
}
