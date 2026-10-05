# Omnichannel Connectors & Human Handoff Protocol

ConvoLab provides enterprise omnichannel integration capabilities, enabling conversational AI agents, simulator personas, and workflow automations to connect directly to external customer channels with interactive messaging and human agent escalation.

---

## 1. Infobip & WhatsApp Business API Connector

The native Infobip connector allows ConvoLab conversational workflows and simulator agents to handle real-world WhatsApp and SMS traffic securely via inbound webhooks.

### Webhook Endpoint

- **Route:** `POST /api/webhooks/infobip/whatsapp`
- **Security:** HMAC-SHA256 signature verification via the `X-Infobip-Signature` header using the configured webhook secret (`Infobip:WebhookSecret`). Unsigned or forged requests are rejected with `401 Unauthorized`.
- **Controller:** [`InfobipWebhookController`](file:///c:/Users/W1022804/convolab-main/src/Api/ConvoLab.Api/Controllers/InfobipWebhookController.cs)

### Message Formats Supported

The connector normalizes inbound payloads into unified domain models ([`OmnichannelModels.cs`](file:///c:/Users/W1022804/convolab-main/src/Domain/ConvoLab.Domain/Omnichannel/OmnichannelModels.cs)):

1. **Text Messages:** Standard conversational messages.
2. **Interactive Quick Replies & Buttons:** Button tap responses containing payload identifiers.
3. **List Messages:** Menu and list item selection callbacks.
4. **Media & Documents:** Inbound document and image references.

---

## 2. Human Handoff & Escalation Protocol

Enterprises require seamless escalation paths when conversational AI encounters customer frustration, regulatory escalations, or explicit requests for live human assistance.

### Escalation Detection Engine ([`HumanHandoffEvaluator.cs`](file:///c:/Users/W1022804/convolab-main/src/Domain/ConvoLab.Domain/Omnichannel/HumanHandoffEvaluator.cs))

The evaluation engine checks incoming messages against three automated escalation criteria:

1. **Explicit Keyword Escalation:**
   - Detects phrases such as `agent`, `human`, `representative`, `operator`, `consultant`, `support person`, `speak to someone`, `talk to a person`.
2. **Sentiment & Frustration Indicators:**
   - Detects extreme negative sentiment or frustration triggers: `angry`, `furious`, `terrible service`, `unacceptable`, `lawyer`, `ombudsman`, `sue`.
3. **Model / Workflow Confidence Trigger:**
   - Workflows can trigger handoff when confidence falls below the defined threshold or when unhandled intent loop count exceeds 3.

### Handoff Payload & Status

When triggered, the evaluator produces a `HumanHandoffDecision`:

- `RequiresHandoff`: `true` / `false`
- `HandoffReason`: `ExplicitRequest`, `NegativeSentiment`, or `LowConfidence`
- `TargetQueue`: Target department queue (e.g., `GeneralSupport`, `LegalEscalations`, `BillingPriority`)
- `ContextSummary`: Transcript summary provided to the human contact center agent (e.g., Genesys, Zendesk, Salesforce Omnichannel).

---

## 3. Configuration

```json
{
  "Infobip": {
    "BaseUrl": "https://api.infobip.com",
    "ApiKey": "env:INFOBIP_API_KEY",
    "WebhookSecret": "env:INFOBIP_WEBHOOK_SECRET",
    "DefaultSenderNumber": "27820000000"
  }
}
```
