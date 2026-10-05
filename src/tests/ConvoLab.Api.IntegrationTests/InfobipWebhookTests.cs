using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace ConvoLab.Api.IntegrationTests;

public sealed class InfobipWebhookTests : IClassFixture<ConvoLabApiFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public InfobipWebhookTests(ConvoLabApiFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task Webhook_Health_Returns_Healthy()
    {
        var response = await _client.GetAsync("/api/connectors/infobip/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", json.GetProperty("status").GetString());
        Assert.Equal("Infobip WhatsApp", json.GetProperty("connector").GetString());
    }

    [Fact]
    public async Task Inbound_WhatsApp_Message_Is_Handled_By_Simulation()
    {
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId = "wa-msg-12345",
                    from = "27821234567",
                    to = "27829999999",
                    message = new
                    {
                        text = "Hello, what are your business hours?"
                    }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/connectors/infobip/webhook", payload);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Response ({response.StatusCode}): {body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("PROCESSED", json.GetProperty("status").GetString());
        Assert.Equal(1, json.GetProperty("count").GetInt32());

        var result = json.GetProperty("results")[0];
        Assert.Equal("wa-msg-12345", result.GetProperty("messageId").GetString());
        Assert.True(result.GetProperty("handled").GetBoolean());
        Assert.False(result.GetProperty("escalatedToHuman").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("reply").GetString()));
    }

    [Fact]
    public async Task Inbound_Message_With_Escalation_Triggers_Human_Handoff()
    {
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId = "wa-escalate-999",
                    from = "27829876543",
                    to = "27829999999",
                    message = new
                    {
                        text = "I demand to speak to a human representative immediately!"
                    }
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/connectors/infobip/webhook", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = json.GetProperty("results")[0];
        Assert.True(result.GetProperty("escalatedToHuman").GetBoolean());
        Assert.Contains("live consultant", result.GetProperty("reply").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(result.GetProperty("quickReplies").GetArrayLength() > 0);
    }
}
