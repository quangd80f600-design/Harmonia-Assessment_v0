namespace Harmonia.Web.Models;

public static class QuestionPurposes
{
    public const string ForTest = "ForTest";
    public const string ForIntervention = "ForIntervention";
    public static bool IsValid(string? value) => value is ForTest or ForIntervention;
    public static string Label(string value) => value switch
    {
        ForTest => "Dùng trong đề kiểm tra",
        ForIntervention => "Luyện tập can thiệp",
        _ => "Không hợp lệ"
    };
}
