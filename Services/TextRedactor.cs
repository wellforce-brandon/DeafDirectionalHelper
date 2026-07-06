using System.Text.RegularExpressions;

namespace DeafDirectionalHelper.Services;

/// <summary>
/// Strips obvious secret/PII shapes out of arbitrary text before it goes into
/// a feedback report. Ported from the BP feedback-widget template's
/// telemetry-redact.ts - framework-agnostic, so the regex set carries over
/// directly. Applied here because reports are posted to a public GitHub repo
/// (this app has no server to keep a private triage destination behind).
/// </summary>
public static class TextRedactor
{
    private static readonly Regex Jwt = new(@"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", RegexOptions.Compiled);
    private static readonly Regex Bearer = new(@"\b[Bb]earer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex LongHex = new(@"\b[A-Fa-f0-9]{32,}\b", RegexOptions.Compiled);
    private static readonly Regex LongToken = new(@"\b[A-Za-z0-9_-]{40,}\b", RegexOptions.Compiled);

    public static string Redact(string value)
    {
        value = Jwt.Replace(value, "[redacted-jwt]");
        value = Bearer.Replace(value, "Bearer [redacted]");
        value = Email.Replace(value, "[redacted-email]");
        value = LongHex.Replace(value, "[redacted-hex]");
        value = LongToken.Replace(value, "[redacted-token]");
        return value;
    }

    public static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }
}
