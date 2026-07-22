using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Application.IntelligenceStudio;

/// <summary>
/// Initializes the runtime Intelligence Engine catalogue once per API process
/// from the same canonical provider definitions used by Intelligence Center.
/// </summary>
public sealed class IntelligenceCatalogueBootstrapper(
    IIntelligenceEngine intelligence,
    IIntelligenceStudioConfiguration configuration) : IIntelligenceCatalogueBootstrapper
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_ready) return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_ready) return;

            foreach (var providerDefinition in configuration.GetProviders().Where(item => item.IsConfigured))
            {
                var providerKind = ResolveProviderKind(providerDefinition.Key);
                var providerId = await intelligence.RegisterProviderAsync(
                    providerDefinition.DisplayName,
                    providerKind,
                    RateLimitWindow.Unlimited(),
                    cancellationToken);

                await intelligence.ReportProviderHealthAsync(
                    providerId,
                    ProviderHealthSnapshot.Create(
                        ProviderAvailability.Available,
                        TimeSpan.FromMilliseconds(providerDefinition.Models.FirstOrDefault()?.TypicalLatencyMs ?? 500),
                        errorRate: 0,
                        capacityUtilisation: 0.05),
                    cancellationToken);

                foreach (var model in providerDefinition.Models)
                {
                    var capabilities = model.Capabilities
                        .Select(value => Enum.TryParse<IntelligenceCapability>(value, true, out var parsed) ? parsed : (IntelligenceCapability?)null)
                        .Where(value => value.HasValue)
                        .Select(value => value!.Value)
                        .ToArray();

                    await intelligence.RegisterModelAsync(
                        providerId,
                        model.DisplayName,
                        capabilities.Length == 0 ? CapabilitySet.Of(IntelligenceCapability.Chat) : CapabilitySet.Of(capabilities),
                        ModelPricing.Create(model.InputPricePer1K ?? 0m, model.OutputPricePer1K ?? 0m, currency: model.Currency),
                        model.MaxContextTokens,
                        model.MaxOutputTokens,
                        TimeSpan.FromMilliseconds(model.TypicalLatencyMs),
                        cancellationToken);
                }
            }

            _ready = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ProviderKind ResolveProviderKind(string key)
        => key.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
            ? ProviderKind.Gemini
            : key.Equals("Deterministic", StringComparison.OrdinalIgnoreCase)
                ? ProviderKind.InternalModel
                : ProviderKind.Custom;
}
