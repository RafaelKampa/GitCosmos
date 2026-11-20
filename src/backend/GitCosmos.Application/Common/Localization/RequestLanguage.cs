namespace GitCosmos.Application.Common.Localization;

public static class RequestLanguage
{
    private static readonly AsyncLocal<string?> CurrentLanguage = new();

    public static string Current
    {
        get => Normalize(CurrentLanguage.Value);
        set => CurrentLanguage.Value = Normalize(value);
    }

    public static string Normalize(string? language)
    {
        var first = (language ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";
        if (first.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return "es";
        if (first.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en";
        return "pt-BR";
    }
}
