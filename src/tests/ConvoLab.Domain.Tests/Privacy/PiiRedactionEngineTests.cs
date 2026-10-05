using ConvoLab.Domain.Privacy;

namespace ConvoLab.Domain.Tests.Privacy;

public class PiiRedactionEngineTests
{
    private readonly RegexPiiRedactionEngine _engine = new();

    [Fact]
    public void Redacts_email_and_restores_it()
    {
        var result = _engine.Redact("Contact jane.doe@example.com today.");
        Assert.DoesNotContain("jane.doe@example.com", result.RedactedText);
        Assert.Contains("[EMAIL_1]", result.RedactedText);
        Assert.Equal("Reply to jane.doe@example.com",
            _engine.Restore("Reply to [EMAIL_1]", result.Mappings));
    }

    [Fact]
    public void Same_value_reuses_one_token()
    {
        var result = _engine.Redact("a@b.co then a@b.co again, and c@d.co");
        Assert.Equal(2, result.RedactionCount);
        Assert.Equal("[EMAIL_1] then [EMAIL_1] again, and [EMAIL_2]", result.RedactedText);
    }

    [Fact]
    public void Redacts_valid_card_but_not_random_digits()
    {
        var card = _engine.Redact("Card 4111 1111 1111 1111 please");
        Assert.Contains("[CARD_1]", card.RedactedText);
        var notCard = _engine.Redact("Order 1234567890123456");
        Assert.DoesNotContain("[CARD_", notCard.RedactedText);
    }

    [Fact]
    public void Redacts_valid_south_african_id()
    {
        // 8001015009087 is a widely used Luhn-valid sample ID.
        var result = _engine.Redact("ID 8001015009087");
        Assert.Contains("[SA_ID_1]", result.RedactedText);
        Assert.DoesNotContain("8001015009087", result.RedactedText);
    }

    [Fact]
    public void Redacts_ip_address()
    {
        var result = _engine.Redact("from 192.168.1.34 now");
        Assert.Contains("[IP_1]", result.RedactedText);
    }

    [Fact]
    public void Leaves_plain_text_and_empty_input_untouched()
    {
        Assert.Equal("Hello there", _engine.Redact("Hello there").RedactedText);
        Assert.Equal(string.Empty, _engine.Redact(string.Empty).RedactedText);
        Assert.Equal(0, _engine.Redact("Hello there").RedactionCount);
    }

    [Fact]
    public void Counts_by_type_expose_no_values()
    {
        var result = _engine.Redact("x@y.co and 10.0.0.1");
        Assert.Equal(1, result.CountsByType[PiiEntityType.Email]);
        Assert.Equal(1, result.CountsByType[PiiEntityType.IpAddress]);
    }
}
