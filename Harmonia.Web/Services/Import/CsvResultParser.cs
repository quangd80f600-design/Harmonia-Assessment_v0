using System.Globalization;
using System.Text;
using Harmonia.Web.ViewModels;
using Microsoft.VisualBasic.FileIO;

namespace Harmonia.Web.Services.Import;

public sealed record CsvAnswerRow(int Line, string StudentCode, string[] Answers);
public sealed record ParsedCsv(short[] QuestionNumbers, IReadOnlyList<CsvAnswerRow> Rows,
    IReadOnlyList<ImportError> Errors);

public sealed class CsvResultParser
{
    public const int MaxBytes = 1024 * 1024;
    public const int MaxStudents = 200;
    public const int MaxQuestions = 200;

    public ParsedCsv Parse(byte[] bytes)
    {
        var errors = new List<ImportError>();
        var rows = new List<CsvAnswerRow>();
        var numbers = Array.Empty<short>();
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
            return new(numbers, rows, new[] { new ImportError(null, "File", "Chọn file CSV từ 1 byte đến 1 MB.") });
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            using var parser = new TextFieldParser(new StringReader(text))
            { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
            parser.SetDelimiters(",");
            var header = parser.ReadFields();
            if (header is null || header.Length < 2 || header.Length > MaxQuestions + 1
                || header[0] != "StudentCode" || header.Any(h => h.Equals("AssessmentId", StringComparison.OrdinalIgnoreCase)))
                return new(numbers, rows, new[] { new ImportError(1, "Header", "Định dạng CSV mới là StudentCode,Q1,Q2,...; không có cột AssessmentId. Hãy tải lại mẫu; tối đa 200 câu.") });
            var questionNumbers = new List<short>();
            foreach (var name in header.Skip(1))
            {
                if (name.Length < 2 || name[0] != 'Q' || !short.TryParse(name.AsSpan(1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var n) || n <= 0 || name != $"Q{n}")
                    errors.Add(new(1, "Header", "Tên cột câu hỏi phải có dạng Q1, Q2... theo mẫu của đề."));
                else questionNumbers.Add(n);
            }
            if (questionNumbers.Distinct().Count() != questionNumbers.Count)
                errors.Add(new(1, "Header", "Cột câu hỏi bị trùng."));
            if (errors.Count > 0) return new(numbers, rows, errors);
            numbers = questionNumbers.ToArray();
            var codes = new HashSet<string>(StringComparer.Ordinal);
            while (!parser.EndOfData && errors.Count < 100)
            {
                var line = (int)parser.LineNumber;
                var fields = parser.ReadFields();
                if (fields is null) break;
                if (rows.Count >= MaxStudents) { errors.Add(new(line, "File", "Tối đa 200 học sinh mỗi file.")); break; }
                if (fields.Length != header.Length)
                {
                    errors.Add(new(line, "Columns", "Số cột không khớp header. Ô bỏ trống vẫn phải có dấu phân cách."));
                    continue;
                }
                var code = fields[0].Trim();
                if (code.Length is 0 or > 50 || code.Any(char.IsControl)) errors.Add(new(line, "StudentCode", "Mã học sinh không hợp lệ."));
                else if (!codes.Add(code)) errors.Add(new(line, "StudentCode", "Mã học sinh lặp trong file; toàn bộ file chưa được nhập."));
                var answers = fields.Skip(1).Select(s => s.Trim().ToUpperInvariant()).ToArray();
                for (var i = 0; i < answers.Length && errors.Count < 100; i++)
                    if (answers[i] != "" && answers[i] is not ("A" or "B" or "C" or "D"))
                        errors.Add(new(line, $"Q{numbers[i]}", "Chỉ nhận A, B, C, D hoặc ô trống."));
                rows.Add(new(line, code, answers));
            }
            if (rows.Count == 0 && errors.Count == 0) errors.Add(new(null, "File", "File chưa có dòng học sinh."));
        }
        catch (DecoderFallbackException) { errors.Add(new(null, "File", "File phải dùng mã hóa UTF-8.")); }
        catch (MalformedLineException) { errors.Add(new(null, "File", "CSV sai cú pháp dấu ngoặc kép hoặc dòng dữ liệu.")); }
        return new(numbers, rows, errors.Take(100).ToArray());
    }
}
