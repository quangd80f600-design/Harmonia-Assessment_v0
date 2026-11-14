using System.ComponentModel.DataAnnotations;
using System.Text;
using Harmonia.Web.ViewModels;
using Microsoft.VisualBasic.FileIO;

namespace Harmonia.Web.Services.Import;

public sealed class QuestionCsvParser
{
    public const string Header = "QuestionCode,SubskillCode,QuestionText,OptionA,OptionB,OptionC,OptionD,AnswerKey";
    public const int MaxBytes = 1024 * 1024;
    public const int MaxRows = 200;
    public static byte[] Template() => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Header + "\r\n")).ToArray();

    public ParsedQuestions Parse(byte[] bytes)
    {
        var rows = new List<QuestionCsvRow>();
        var errors = new List<ImportError>();
        if (bytes.Length is 0 or > MaxBytes)
            return new(rows, [new(null, "File", "Chọn CSV UTF-8 từ 1 byte đến 1 MB.")]);
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            // PostgreSQL text cannot store NUL; fail before preview rather than at INSERT.
            if (text.Contains('\0')) return new(rows, [new(null, "File", "File chứa ký tự NUL không hợp lệ.")]);
            using var parser = new TextFieldParser(new StringReader(text))
            { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
            parser.SetDelimiters(",");
            if (parser.ReadFields() is not { } header || !header.SequenceEqual(Header.Split(',')))
                return new(rows, [new(1, "Header", "Cột không đúng mẫu. Hãy tải lại mẫu CSV câu hỏi (8 cột, phân cách bằng dấu phẩy).")]);
            var codes = new HashSet<string>(StringComparer.Ordinal);
            var count = 0;
            while (!parser.EndOfData && errors.Count < 100)
            {
                var line = (int)parser.LineNumber;
                var fields = parser.ReadFields();
                if (fields is null) break;
                if (++count > MaxRows) { errors.Add(new(line, "File", "Tối đa 200 câu hỏi mỗi file.")); break; }
                if (fields.Length != 8) { errors.Add(new(line, "Columns", "Mỗi dòng cần đủ 8 cột theo mẫu.")); continue; }
                var row = new QuestionCsvRow(line, fields[0].Trim(), fields[1].Trim(), fields[2].Trim(),
                    fields[3].Trim(), fields[4].Trim(), fields[5].Trim(), fields[6].Trim(), fields[7].Trim().ToUpperInvariant());
                if (row.SubskillCode.Length is 0 or > 50 || row.SubskillCode.Any(char.IsControl))
                    errors.Add(new(line, "SubskillCode", "Mã kỹ năng bắt buộc, tối đa 50 ký tự, không có ký tự điều khiển."));
                if (!codes.Add(row.QuestionCode)) errors.Add(new(line, "QuestionCode", "Mã câu hỏi lặp trong file; không nhập một phần."));
                var input = row.ToInput(1);
                var validation = new List<ValidationResult>();
                Validator.TryValidateObject(input, new ValidationContext(input), validation, true);
                foreach (var issue in validation)
                    errors.Add(new(line, string.Join(", ", issue.MemberNames), issue.ErrorMessage ?? "Giá trị không hợp lệ."));
                // Reuse authoring rules, including distinct choices and control-character rejection.
                if (validation.Count == 0 && AuthoringService.ValidateQuestion(input) is { } error)
                    errors.Add(new(line, "Question", error));
                rows.Add(row);
            }
            if (count == 0) errors.Add(new(null, "File", "File chưa có dòng câu hỏi."));
        }
        catch (DecoderFallbackException) { errors.Add(new(null, "File", "File phải dùng mã hóa UTF-8; hãy lưu CSV UTF-8 trong Excel.")); }
        catch (MalformedLineException ex) { errors.Add(new((int)ex.LineNumber, "File", "CSV sai cú pháp dấu ngoặc kép.")); }
        return new(rows.AsReadOnly(), errors.Take(100).ToArray());
    }
}
