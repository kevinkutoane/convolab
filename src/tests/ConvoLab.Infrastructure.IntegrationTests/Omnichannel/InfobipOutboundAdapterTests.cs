using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConvoLab.Application.Omnichannel;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Omnichannel;
using ConvoLab.Infrastructure.Omnichannel;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConvoLab.Infrastructure.IntegrationTests.Omnichannel;

public sealed class InfobipOutboundAdapterTests
{
    private const string BaseUrl = "https://test.infobip.com";
    private const string ApiKey = "test-api-key-123";
    private const string SenderId = "27820000000";

    [Fact]
    public async Task SendWhatsAppTextMessageAsync_SendsCorrectPayloadAndEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { messageId = "msg-text-123" })
            };
        });

        var adapter = CreateAdapter(handler);
        var envelope = new OutboundMessageEnvelope(
            Channel: "WhatsApp",
            RecipientId: "27821234567",
            Text: "Hello customer",
            MessageType: OmnichannelMessageType.Text);

        var result = await adapter.SendWhatsAppTextMessageAsync(envelope);

        Assert.True(result.Success);
        Assert.Equal("msg-text-123", result.MessageId);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(capturedRequest);
        Assert.Equal("/whatsapp/1/message/text", capturedRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("App", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal(ApiKey, capturedRequest.Headers.Authorization?.Parameter);

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal(SenderId, root.GetProperty("from").GetString());
        Assert.Equal("27821234567", root.GetProperty("to").GetString());
        Assert.Equal("Hello customer", root.GetProperty("content").GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendWhatsAppButtonsMessageAsync_SendsInteractiveButtonsPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { messageId = "msg-buttons-123" })
            };
        });

        var adapter = CreateAdapter(handler);
        var envelope = new OutboundMessageEnvelope(
            Channel: "WhatsApp",
            RecipientId: "27821234567",
            Text: "Please choose your vehicle type:",
            MessageType: OmnichannelMessageType.InteractiveButton,
            InteractiveOptions: ["Sedan", "SUV", "Truck"]);

        var result = await adapter.SendWhatsAppButtonsMessageAsync(envelope);

        Assert.True(result.Success);
        Assert.Equal("msg-buttons-123", result.MessageId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("/whatsapp/1/message/interactive/buttons", capturedRequest!.RequestUri!.AbsolutePath);

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal(SenderId, root.GetProperty("from").GetString());
        Assert.Equal("27821234567", root.GetProperty("to").GetString());
        Assert.Equal("Please choose your vehicle type:", root.GetProperty("content").GetProperty("body").GetProperty("text").GetString());

        var buttons = root.GetProperty("content").GetProperty("action").GetProperty("buttons");
        Assert.Equal(3, buttons.GetArrayLength());
        Assert.Equal("REPLY", buttons[0].GetProperty("type").GetString());
        Assert.Equal("Sedan", buttons[0].GetProperty("title").GetString());
        Assert.Equal("SUV", buttons[1].GetProperty("title").GetString());
        Assert.Equal("Truck", buttons[2].GetProperty("title").GetString());
    }

    [Fact]
    public async Task SendWhatsAppListMessageAsync_SendsInteractiveListPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { messageId = "msg-list-123" })
            };
        });

        var adapter = CreateAdapter(handler);
        var envelope = new OutboundMessageEnvelope(
            Channel: "WhatsApp",
            RecipientId: "27821234567",
            Text: "Select your policy branch:",
            MessageType: OmnichannelMessageType.Text,
            InteractiveOptions: ["Gauteng", "Western Cape", "KwaZulu-Natal", "Eastern Cape"],
            Metadata: new Dictionary<string, string>
            {
                ["actionTitle"] = "View Branches",
                ["sectionTitle"] = "South Africa"
            });

        var result = await adapter.SendWhatsAppListMessageAsync(envelope);

        Assert.True(result.Success);
        Assert.Equal("msg-list-123", result.MessageId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("/whatsapp/1/message/interactive/list", capturedRequest!.RequestUri!.AbsolutePath);

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("Select your policy branch:", root.GetProperty("content").GetProperty("body").GetProperty("text").GetString());

        var action = root.GetProperty("content").GetProperty("action");
        Assert.Equal("View Branches", action.GetProperty("title").GetString());

        var sections = action.GetProperty("sections");
        Assert.Equal(1, sections.GetArrayLength());
        Assert.Equal("South Africa", sections[0].GetProperty("title").GetString());

        var rows = sections[0].GetProperty("rows");
        Assert.Equal(4, rows.GetArrayLength());
        Assert.Equal("Gauteng", rows[0].GetProperty("title").GetString());
        Assert.Equal("Western Cape", rows[1].GetProperty("title").GetString());
    }

    [Fact]
    public async Task SendWhatsAppDocumentMessageAsync_SendsDocumentPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { messageId = "msg-doc-123" })
            };
        });

        var adapter = CreateAdapter(handler);
        var envelope = new OutboundMessageEnvelope(
            Channel: "WhatsApp",
            RecipientId: "27821234567",
            Text: "Here is your policy schedule",
            MessageType: OmnichannelMessageType.Document,
            AttachmentUrl: "https://convolab.test/documents/policy-schedule.pdf",
            Metadata: new Dictionary<string, string>
            {
                ["filename"] = "PolicySchedule.pdf"
            });

        var result = await adapter.SendWhatsAppDocumentMessageAsync(envelope);

        Assert.True(result.Success);
        Assert.Equal("msg-doc-123", result.MessageId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("/whatsapp/1/message/document", capturedRequest!.RequestUri!.AbsolutePath);

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        var content = root.GetProperty("content");
        Assert.Equal("https://convolab.test/documents/policy-schedule.pdf", content.GetProperty("mediaUrl").GetString());
        Assert.Equal("Here is your policy schedule", content.GetProperty("caption").GetString());
        Assert.Equal("PolicySchedule.pdf", content.GetProperty("filename").GetString());
    }

    [Fact]
    public async Task SendWhatsAppDocumentMessageAsync_ReturnsErrorWhenAttachmentUrlMissing()
    {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = CreateAdapter(handler);
        var envelope = new OutboundMessageEnvelope(
            Channel: "WhatsApp",
            RecipientId: "27821234567",
            Text: "Missing document",
            MessageType: OmnichannelMessageType.Document,
            AttachmentUrl: null);

        var result = await adapter.SendWhatsAppDocumentMessageAsync(envelope);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("AttachmentUrl is required", result.ErrorMessage);
    }

    [Fact]
    public async Task SendWhatsAppTextMessageAsync_HandlesApiRejection()
    {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Invalid recipient\"}")
        }));

        var adapter = CreateAdapter(handler);
        var envelope = new OutboundMessageEnvelope(
            Channel: "WhatsApp",
            RecipientId: "invalid-number",
            Text: "Test",
            MessageType: OmnichannelMessageType.Text);

        var result = await adapter.SendWhatsAppTextMessageAsync(envelope);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("The Infobip provider rejected the outbound message.", result.ErrorMessage);
    }

    [Fact]
    public async Task SendWhatsAppTextMessageAsync_ReturnsErrorWhenSecretsMissing()
    {
        var handler = new MockHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var emptySecretStore = new MockSecretStore(new Dictionary<string, string>());
        var clientFactory = new MockHttpClientFactory(handler);
        var adapter = new InfobipOutboundAdapter(clientFactory, emptySecretStore, NullLogger<InfobipOutboundAdapter>.Instance);

        var envelope = new OutboundMessageEnvelope(
            Channel: "WhatsApp",
            RecipientId: "27821234567",
            Text: "Test",
            MessageType: OmnichannelMessageType.Text);

        var result = await adapter.SendWhatsAppTextMessageAsync(envelope);

        Assert.False(result.Success);
        Assert.Contains("missing", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static InfobipOutboundAdapter CreateAdapter(HttpMessageHandler handler)
    {
        var secrets = new Dictionary<string, string>
        {
            ["channel.infobip.api_key"] = ApiKey,
            ["channel.infobip.base_url"] = BaseUrl,
            ["channel.infobip.sender_id"] = SenderId
        };
        var secretStore = new MockSecretStore(secrets);
        var clientFactory = new MockHttpClientFactory(handler);
        return new InfobipOutboundAdapter(clientFactory, secretStore, NullLogger<InfobipOutboundAdapter>.Instance);
    }

    private sealed class MockHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public MockHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false);
        }
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _responder(request, cancellationToken);
    }

    private sealed class MockSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets;

        public MockSecretStore(Dictionary<string, string> secrets)
        {
            _secrets = secrets;
        }

        public Task<SecretResolutionResult> ResolveAsync(string reference, CancellationToken ct = default)
        {
            if (_secrets.TryGetValue(reference, out var value))
            {
                return Task.FromResult(SecretResolutionResult.Resolved("mock", value));
            }
            return Task.FromResult(SecretResolutionResult.Failed("mock", SecretResolutionStatus.Missing, "secret.not_found"));
        }

        public Task<SecretValidationResult> ValidateAsync(string reference, CancellationToken ct = default)
        {
            var isResolved = _secrets.ContainsKey(reference);
            return Task.FromResult(new SecretValidationResult(
                "mock",
                isResolved ? SecretResolutionStatus.Resolved : SecretResolutionStatus.Missing));
        }

        public void Invalidate(string reference) => _secrets.Remove(reference);

        public void Clear() => _secrets.Clear();
    }
}
