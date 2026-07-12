using System.Text.RegularExpressions;

namespace Application.Datasets;

/// <summary>
/// Scrubs obvious PII from captured fixture text at ingest, before it is persisted. Pure and
/// dependency-free (no EF/HTTP), so it is a concrete Application service rather than a port —
/// per <i>No premature abstractions</i>, it becomes an <c>IRedactor</c> seam only when a second
/// redaction strategy actually appears. Deliberately conservative: it targets the common
/// identifiers (email, phone) that leak from real app traffic; it is not a full anonymizer.
/// </summary>
public sealed partial class FixtureRedactor
{
    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    // Loose international/NANP phone match: optional +, groups of digits with space/dot/hyphen
    // separators, 9+ digits total. Kept conservative to avoid mangling ordinary numbers.
    [GeneratedRegex(@"\+?\d[\d\-\.\s]{7,}\d", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    /// <summary>Returns the text with recognised PII replaced by placeholders; null passes through.</summary>
    public string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = EmailRegex().Replace(text, "[REDACTED-EMAIL]");
        text = PhoneRegex().Replace(text, "[REDACTED-PHONE]");
        return text;
    }
}
