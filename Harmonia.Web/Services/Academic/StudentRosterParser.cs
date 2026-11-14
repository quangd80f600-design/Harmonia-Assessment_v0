using System.Globalization;
using System.Text;
using Harmonia.Web.ViewModels;
using Microsoft.VisualBasic.FileIO;

namespace Harmonia.Web.Services.Academic;

public sealed record RosterCsvRow(int Line, string StudentCode, string FullName,
    DateOnly DateOfBirth, string Gender, DateOnly StartDate, DateOnly? EndDate);
public sealed record ParsedRoster(IReadOnlyList<RosterCsvRow> Rows, IReadOnlyList<ImportError> Errors);

public sealed class StudentRosterParser
{
    public const string Header = "StudentCode,FullName,DateOfBirth,Gender,StartDate,EndDate";
    public const int MaxBytes = 1024 * 1024;
    public const int MaxStudents = 200;
    internal static bool Overlaps(DateOnly start, DateOnly? end, DateOnly otherStart, DateOnly? otherEnd) =>
        (!end.HasValue || otherStart < end.Value) && (!otherEnd.HasValue || start < otherEnd.Value);
    public ParsedRoster Parse(byte[] bytes)
    {
        var errors = new List<ImportError>();
        var rows = new List<RosterCsvRow>();
        if (bytes.Length is 0 or > MaxBytes)
            return new(rows, new[] { new ImportError(null, "File", "Chọn CSV UTF-8 từ 1 byte đến 1 MB.") });
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            using var parser = new TextFieldParser(new StringReader(text))
            { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
            parser.SetDelimiters(",");
            if (parser.ReadFields() is not { } header || !header.SequenceEqual(Header.Split(',')))
                return new(rows, new[] { new ImportError(1, "Header", "Cột không đúng mẫu. Tải lại CSV StudentCode,FullName,DateOfBirth,Gender,StartDate,EndDate.") });
            var codes = new HashSet<string>(StringComparer.Ordinal);
            var count = 0;
            while (!parser.EndOfData && errors.Count < 100)
            {
                var line = (int)parser.LineNumber;
                var fields = parser.ReadFields();
                if (fields is null) break;
                if (++count > MaxStudents) { errors.Add(new(line, "File", "Tối đa 200 học sinh mỗi file.")); break; }
                if (fields.Length != 6) { errors.Add(new(line, "Columns", "Cần đủ 6 cột, kể cả EndDate để trống.")); continue; }
                var code = fields[0].Trim(); var name = fields[1].Trim();
                if (code.Length is 0 or > 50 || code.Any(char.IsControl)) errors.Add(new(line, "StudentCode", "Mã học sinh bắt buộc, tối đa 50 ký tự, không chứa ký tự điều khiển."));
                else if (!codes.Add(code)) errors.Add(new(line, "StudentCode", "Mã học sinh lặp trong file; không nhập một phần."));
                if (name.Length is 0 or > 150 || name.Any(char.IsControl)) errors.Add(new(line, "FullName", "Họ tên bắt buộc, tối đa 150 ký tự, không chứa ký tự điều khiển."));
                var gender = fields[3].Trim().ToUpperInvariant() switch { "NAM" or "MALE" => "Male", "NỮ" or "FEMALE" => "Female", _ => "" };
                if (gender.Length == 0) errors.Add(new(line, "Gender", "Giới tính phải là Nam/Nữ hoặc Male/Female theo schema hiện tại."));
                bool Date(string value, out DateOnly date) => DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
                var validBirth = Date(fields[2], out var birth);
                var validStart = Date(fields[4], out var start);
                if (!validBirth || birth > AcademicManagementService.Today) errors.Add(new(line, "DateOfBirth", "Ngày sinh phải đúng yyyy-MM-dd và không ở tương lai."));
                if (!validStart) errors.Add(new(line, "StartDate", "Ngày vào lớp bắt buộc, định dạng yyyy-MM-dd."));
                DateOnly? end = null;
                if (fields[5].Trim().Length > 0)
                {
                    if (!Date(fields[5], out var last)) errors.Add(new(line, "EndDate", "Ngày kết thúc phải đúng yyyy-MM-dd hoặc để trống."));
                    else { end = last; if (validStart && end <= start) errors.Add(new(line, "EndDate", "Ngày kết thúc phải sau ngày vào lớp. Ngày kết thúc không được tính là ngày học sinh còn thuộc lớp.")); }
                }
                rows.Add(new(line, code, name, birth, gender, start, end));
            }
            if (count == 0) errors.Add(new(null, "File", "File chưa có dòng học sinh."));
        }
        catch (DecoderFallbackException) { errors.Add(new(null, "File", "File phải dùng mã hóa UTF-8.")); }
        catch (MalformedLineException) { errors.Add(new(null, "File", "CSV sai cú pháp dấu ngoặc kép.")); }
        return new(rows, errors.Take(100).ToArray());
    }
}
