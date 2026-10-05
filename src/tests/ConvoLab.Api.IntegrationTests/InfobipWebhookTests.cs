using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConvoLab.Api.Security;
using Microsoft.AspNetCore.Http;
using Xunit.Abstractions;

namespace ConvoLab.Api.IntegrationTests;

public sealed class InfobipWebhookTests : IClassFixture<ConvoLabApiFactory>
{
    private const string TestWebhookSecret = "test-infobip-secret";
    private const string TestConfiguredRecipient = "27829999999";

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
    public async Task Webhook_Without_Authentication_Returns_Unauthorized()
    {
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId = "wa-msg-unauth",
                    from = "27821234567",
                    to = TestConfiguredRecipient,
                    message = new { text = "Unauthenticated probe" }
                }
            }
        };

        var request = CreateWebhookRequest(payload);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Webhook_Security_Rejects_When_Expected_Secret_Is_Missing()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Callback-Secret"] = TestWebhookSecret;
        Assert.False(InfobipWebhookSecurity.VerifyWebhookRequest(context.Request, "{}", string.Empty));
    }

    [Fact]
    public async Task Webhook_With_Invalid_Signature_Returns_Unauthorized()
    {
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId = "wa-msg-bad-sig",
                    from = "27821234567",
                    to = TestConfiguredRecipient,
                    message = new { text = "Forged message probe" }
                }
            }
        };

        var request = CreateWebhookRequest(payload, signature: "bad-hmac-signature-deadbeef");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_With_Invalid_Shared_Secret_Returns_Unauthorized()
    {
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId = "wa-msg-bad-secret",
                    from = "27821234567",
                    to = TestConfiguredRecipient,
                    message = new { text = "Bad shared secret probe" }
                }
            }
        };

        var request = CreateWebhookRequest(payload, secretHeader: "wrong-secret-token");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inbound_WhatsApp_Message_Is_Handled_With_Valid_Hmac_Signature()
    {
        var messageId = "wa-msg-hmac-" + Guid.NewGuid().ToString("N")[..8];
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId,
                    from = "27821234567",
                    to = TestConfiguredRecipient,
                    message = new
                    {
                        text = "Hello, what are your business hours?"
                    }
                }
            }
        };

        var request = CreateSignedWebhookRequest(payload);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"Response ({response.StatusCode}): {body}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("PROCESSED", json.GetProperty("status").GetString());
        Assert.Equal(1, json.GetProperty("count").GetInt32());

        var result = json.GetProperty("results")[0];
        Assert.Equal(messageId, result.GetProperty("messageId").GetString());
        Assert.True(result.GetProperty("handled").GetBoolean());
        Assert.False(result.GetProperty("duplicate").GetBoolean());
        Assert.False(result.GetProperty("escalatedToHuman").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("reply").GetString()));
    }

    [Fact]
    public async Task Inbound_WhatsApp_Message_Is_Handled_With_Shared_Secret_Header()
    {
        var messageId = "wa-msg-secret-" + Guid.NewGuid().ToString("N")[..8];
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId,
                    from = "27821234567",
                    to = TestConfiguredRecipient,
                    message = new
                    {
                        text = "Need information on claims process."
                    }
                }
            }
        };

        var request = CreateWebhookRequest(payload, secretHeader: TestWebhookSecret);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = json.GetProperty("results")[0];
        Assert.Equal(messageId, result.GetProperty("messageId").GetString());
        Assert.True(result.GetProperty("handled").GetBoolean());
    }

    [Fact]
    public async Task Inbound_WhatsApp_Message_Is_Handled_With_Bearer_Token()
    {
        var messageId = "wa-msg-bearer-" + Guid.NewGuid().ToString("N")[..8];
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId,
                    from = "27821234567",
                    to = TestConfiguredRecipient,
                    message = new
                    {
                        text = "What is the policy renewal date?"
                    }
                }
            }
        };

        var request = CreateWebhookRequest(payload, bearerToken: TestWebhookSecret);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = json.GetProperty("results")[0];
        Assert.Equal(messageId, result.GetProperty("messageId").GetString());
        Assert.True(result.GetProperty("handled").GetBoolean());
    }

    [Fact]
    public async Task Inbound_Message_With_Escalation_Triggers_Human_Handoff()
    {
        var messageId = "wa-escalate-" + Guid.NewGuid().ToString("N")[..8];
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId,
                    from = "27829876543",
                    to = TestConfiguredRecipient,
                    message = new
                    {
                        text = "I demand to speak to a human representative immediately!"
                    }
                }
            }
        };

        var request = CreateSignedWebhookRequest(payload);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var result = json.GetProperty("results")[0];
        Assert.True(result.GetProperty("escalatedToHuman").GetBoolean());
        Assert.Contains("live consultant", result.GetProperty("reply").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(result.GetProperty("quickReplies").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Webhook_With_Unmapped_Recipient_Returns_BadRequest_And_Preserves_Tenant_Isolation()
    {
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId = "wa-msg-unmapped-" + Guid.NewGuid().ToString("N")[..8],
                    from = "27821234567",
                    to = "27820000000", // Not mapped to any workspace connector
                    message = new { text = "This should fail tenant resolution." }
                }
            }
        };

        var request = CreateSignedWebhookRequest(payload);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Tenant resolution failed", json.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_Delivery_Deduplication_Ignores_Duplicate_MessageId()
    {
        var uniqueMessageId = "wa-dedup-" + Guid.NewGuid().ToString("N");
        var payload = new
        {
            results = new[]
            {
                new
                {
                    messageId = uniqueMessageId,
                    from = "27821234567",
                    to = TestConfiguredRecipient,
                    message = new { text = "First delivery of important request." }
                }
            }
        };

        // 1. Initial delivery: must be processed normally
        var firstRequest = CreateSignedWebhookRequest(payload);
        var firstResponse = await _client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstJson = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstResult = firstJson.GetProperty("results")[0];
        Assert.Equal(uniqueMessageId, firstResult.GetProperty("messageId").GetString());
        Assert.True(firstResult.GetProperty("handled").GetBoolean());
        Assert.False(firstResult.GetProperty("duplicate").GetBoolean());
        Assert.Equal("PROCESSED", firstResult.GetProperty("status").GetString());

        // 2. Duplicate delivery retry: must be safely ignored and return DUPLICATE_IGNORED
        var duplicateRequest = CreateSignedWebhookRequest(payload);
        var duplicateResponse = await _client.SendAsync(duplicateRequest);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);

        var duplicateJson = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var duplicateResult = duplicateJson.GetProperty("results")[0];
        Assert.Equal(uniqueMessageId, duplicateResult.GetProperty("messageId").GetString());
        Assert.True(duplicateResult.GetProperty("handled").GetBoolean());
        Assert.True(duplicateResult.GetProperty("duplicate").GetBoolean());
        Assert.Equal("DUPLICATE_IGNORED", duplicateResult.GetProperty("status").GetString());
    }

    private static HttpRequestMessage CreateWebhookRequest(
        object payload,
        string? signature = null,
        string? secretHeader = null,
        string? bearerToken = null)
    {
        var json = JsonSerializer.Serialize(payload);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/connectors/infobip/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (signature is not null)
        {
            request.Headers.Add("X-Infobip-Signature", signature);
        }

        if (secretHeader is not null)
        {
            request.Headers.Add("X-Callback-Secret", secretHeader);
        }

        if (bearerToken is not null)
        {
            request.Headers.Add("Authorization", $"Bearer {bearerToken}");
        }

        return request;
    }

    private static HttpRequestMessage CreateSignedWebhookRequest(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var signature = InfobipWebhookSecurity.ComputeHmacSha256Hex(json, TestWebhookSecret);
        return CreateWebhookRequest(payload, signature: signature);
    }
}
