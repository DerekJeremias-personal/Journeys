using System.Text.RegularExpressions;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Responses;

namespace Journeys.Core.Utility;

public static class CampaignValidationErrorMapper
{
    private static readonly Regex ViolationCodeRegex = new(
        @"\[violation=([^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PathRegex = new(
        @"\bpath=([^\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void AddFromException(
        APIErrorsException ex,
        ICollection<CampaignValidationFindingDto> findings)
    {
        if (ex.Errors == null)
            return;

        foreach (var (field, message) in ex.Errors)
            findings.Add(MapEntry(field, message));
    }

    public static CampaignValidationFindingDto MapEntry(string field, string message)
    {
        var code = ExtractViolationCode(message) ?? DeriveCodeFromField(field);
        return new CampaignValidationFindingDto
        {
            Code = code,
            Field = field,
            Path = ExtractPath(message),
            Message = message,
            Severity = "error"
        };
    }

    private static string? ExtractViolationCode(string message)
    {
        var match = ViolationCodeRegex.Match(message);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractPath(string message)
    {
        var match = PathRegex.Match(message);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string DeriveCodeFromField(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
            return "VALIDATION_ERROR";

        var dot = field.IndexOf('.');
        return dot > 0 ? field[..dot].ToUpperInvariant() : field.ToUpperInvariant();
    }
}
