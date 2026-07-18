using Application.Datasets;

namespace Application.Tests;

public class FixtureRedactorTests
{
    private readonly FixtureRedactor _redactor = new();

    [Fact]
    public void Redacts_email_addresses()
    {
        Assert.Equal("contact [REDACTED-EMAIL] please", _redactor.Redact("contact a.b@example.com please"));
    }

    [Theory]
    [InlineData("call +1 415 555 1234 now")]
    [InlineData("call 415-555-1234 now")]
    [InlineData("call 4155551234 now")]
    public void Redacts_phone_numbers(string text)
    {
        Assert.Contains("[REDACTED-PHONE]", _redactor.Redact(text));
    }

    [Theory]
    [InlineData("RECENT ROUNDS on 2026-07-12 were strong")]
    [InlineData("dates 2026-07-12 and 2025-01-03 stay intact")]
    [InlineData("timestamp 2026-07-12T10:30:00Z")]
    public void Leaves_iso_dates_untouched(string text)
    {
        // B7: the phone matcher must not treat date-shaped strings as phone numbers.
        Assert.DoesNotContain("[REDACTED-PHONE]", _redactor.Redact(text));
        Assert.Contains("2026-07-12", _redactor.Redact(text)!);
    }

    [Fact]
    public void Null_passes_through()
    {
        Assert.Null(_redactor.Redact(null));
    }
}
