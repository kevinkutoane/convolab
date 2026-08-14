using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ConvoLab.Api.Operations;
using ConvoLab.Api.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ConvoLab.Api.IntegrationTests;

public sealed class ProductionReadinessValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"convolab-readiness-{Guid.NewGuid():N}");
    private readonly Dictionary<string, string?> _valid;

    public ProductionReadinessValidatorTests()
    {
        Directory.CreateDirectory(_root);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=ConvoLab Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificatePath = Path.Combine(_root, "certificate.pem");
        var keyPath = Path.Combine(_root, "private-key.pem");
        File.WriteAllText(certificatePath, certificate.ExportCertificatePem());
        File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        _valid = new()
        {
            ["Database:Provider"] = "PostgreSql",
            ["ConnectionStrings:DefaultConnection"] = "Host=postgres;Database=convolab;Username=convolab;Password=real-test-value",
            ["Database:ApplyMigrationsOnStartup"] = "false",
            ["AllowedHosts"] = "api.convolab.test",
            ["Http:UseHttpsRedirection"] = "true",
            ["Authentication:Mode"] = "Local",
            ["Authentication:Local:ProductionAllowed"] = "true",
            ["Proxy:Enabled"] = "false",
            ["DataProtection:Provider"] = "SharedFileSystem",
            ["DataProtection:KeyRingPath"] = _root,
            ["DataProtection:CertificatePemPath"] = certificatePath,
            ["DataProtection:PrivateKeyPemPath"] = keyPath,
            ["SafeMode:BlockAnalyticsExports"] = "true",
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "https://otel.convolab.test"
        };
    }

    [Fact]
    public void Valid_external_production_configuration_has_no_static_findings()
    {
        var findings = Evaluate(_valid);
        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("Database:Provider", "SQLite", "production.database.postgresql_required")]
    [InlineData("ConnectionStrings:DefaultConnection", "", "production.database.connection_required")]
    [InlineData("ConnectionStrings:DefaultConnection", "Host=db;Database=x;Password=change-me", "production.database.placeholder_credential")]
    [InlineData("Database:ApplyMigrationsOnStartup", "true", "production.database.automatic_migrations_forbidden")]
    [InlineData("AllowedHosts", "*", "production.http.hosts_required")]
    [InlineData("Http:UseHttpsRedirection", "false", "production.http.https_required")]
    [InlineData("Authentication:Mode", "Unsupported", "production.authentication.mode_unsupported")]
    [InlineData("Authentication:Local:ProductionAllowed", "false", "production.authentication.local_unacknowledged")]
    [InlineData("DataProtection:Provider", "LocalFileSystem", "production.data_protection.shared_storage_required")]
    [InlineData("SafeMode:BlockAnalyticsExports", null, "production.safe_mode.analytics_export_decision_required")]
    [InlineData("OTEL_EXPORTER_OTLP_ENDPOINT", "not-a-uri", "production.telemetry.otlp_endpoint_invalid")]
    [InlineData("OTEL_EXPORTER_OTLP_PROTOCOL", "http/json", "production.telemetry.otlp_protocol_invalid")]
    [InlineData("Authentication:Local:LoginRateLimitPerMinute", "0", "production.authentication.local_rate_limit_invalid")]
    [InlineData("Authentication:Local:LoginRateLimitPerMinute", "101", "production.authentication.local_rate_limit_invalid")]
    [InlineData("Authentication:Local:BreakGlass:MaximumAttempts", "2", "production.authentication.break_glass_attempts_invalid")]
    [InlineData("Authentication:Local:BreakGlass:MaximumAttempts", "11", "production.authentication.break_glass_attempts_invalid")]
    [InlineData("Authentication:Local:BreakGlass:LockoutMinutes", "0", "production.authentication.break_glass_lockout_invalid")]
    [InlineData("Authentication:Local:BreakGlass:LockoutMinutes", "1441", "production.authentication.break_glass_lockout_invalid")]
    [InlineData("Authentication:Local:BreakGlass:RateLimitPerMinute", "0", "production.authentication.break_glass_rate_limit_invalid")]
    [InlineData("Authentication:Local:BreakGlass:RateLimitPerMinute", "61", "production.authentication.break_glass_rate_limit_invalid")]
    public void Unsafe_static_condition_is_rejected(string key, string? value, string expectedCode)
    {
        var values = new Dictionary<string, string?>(_valid) { [key] = value };
        Assert.Contains(Evaluate(values), finding => finding.Code == expectedCode);
    }

    [Fact]
    public void Enabled_proxy_requires_an_explicit_trust_boundary()
    {
        var values = new Dictionary<string, string?>(_valid)
        {
            ["Proxy:Enabled"] = "true",
            ["Proxy:ForwardLimit"] = "1"
        };
        Assert.Contains(Evaluate(values), finding => finding.Code == "production.proxy.trust_boundary_required");
    }

    [Fact]
    public void Valid_entra_only_configuration_has_no_authentication_findings()
    {
        var tenant = "11111111-1111-1111-1111-111111111111";
        var values = new Dictionary<string, string?>(_valid)
        {
            ["Authentication:Mode"] = "Entra",
            ["Authentication:Local:Enabled"] = "false",
            ["Authentication:Entra:Enabled"] = "true",
            ["Authentication:Entra:TenantId"] = tenant,
            ["Authentication:Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["Authentication:Entra:Authority"] = $"https://login.microsoftonline.com/{tenant}/v2.0",
            ["Authentication:Entra:ClientSecretReference"] = "env:CONVOLAB_ENTRA_CLIENT_SECRET",
            ["Authentication:Entra:PublicOrigin"] = "https://api.convolab.test",
            ["Authentication:Entra:CallbackPath"] = "/signin-oidc",
            ["Authentication:Entra:SignedOutCallbackPath"] = "/signout-callback-oidc",
            ["Authentication:Entra:InvitationExpiryHours"] = "24"
        };
        Assert.DoesNotContain(Evaluate(values), finding => finding.Code.StartsWith("production.authentication", StringComparison.Ordinal));
    }

    [Fact]
    public void Entra_configuration_rejects_cross_tenant_authority_and_plaintext_secret()
    {
        var values = new Dictionary<string, string?>(_valid)
        {
            ["Authentication:Mode"] = "Entra",
            ["Authentication:Local:Enabled"] = "false",
            ["Authentication:Entra:Enabled"] = "true",
            ["Authentication:Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Authentication:Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["Authentication:Entra:Authority"] = "https://login.microsoftonline.com/33333333-3333-3333-3333-333333333333/v2.0",
            ["Authentication:Entra:ClientSecretReference"] = "plaintext-secret",
            ["Authentication:Entra:PublicOrigin"] = "https://api.convolab.test",
            ["Authentication:Entra:CallbackPath"] = "/signin-oidc",
            ["Authentication:Entra:SignedOutCallbackPath"] = "/signout-callback-oidc",
            ["Authentication:Entra:InvitationExpiryHours"] = "24"
        };
        var findings = Evaluate(values);
        Assert.Contains(findings, finding => finding.Code == "production.authentication.entra_authority_invalid");
        Assert.Contains(findings, finding => finding.Code == "production.authentication.entra_client_secret_reference_invalid");
    }

    [Fact]
    public void Uat_requires_writable_shared_data_protection_storage()
    {
        var values = new Dictionary<string, string?>
        {
            ["DataProtection:Provider"] = "LocalFileSystem",
            ["DataProtection:KeyRingPath"] = "relative-keys"
        };
        var findings = ProductionReadinessValidator.Evaluate(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new TestEnvironment("UAT"));

        Assert.Contains(findings, item => item.Code == "uat.data_protection.shared_storage_required");
        Assert.Contains(findings, item => item.Code == "uat.data_protection.key_ring_path_invalid");
    }

    [Fact]
    public void Shared_x509_key_ring_interoperates_across_instances_and_restart()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(_valid).Build();
        var firstServices = new ServiceCollection();
        firstServices.AddLogging();
        firstServices.AddConvoLabDataProtection(configuration, new TestEnvironment("Production"));
        using var first = firstServices.BuildServiceProvider();
        var protectedValue = first.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("operational-test")
            .Protect("restart-sentinel");

        var secondServices = new ServiceCollection();
        secondServices.AddLogging();
        secondServices.AddConvoLabDataProtection(configuration, new TestEnvironment("Production"));
        using var second = secondServices.BuildServiceProvider();
        var restored = second.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("operational-test")
            .Unprotect(protectedValue);

        Assert.Equal("restart-sentinel", restored);
    }

    private static IReadOnlyList<ConvoLab.Application.Operations.ProductionReadinessFinding> Evaluate(
        Dictionary<string, string?> values) =>
        ProductionReadinessValidator.Evaluate(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            new TestEnvironment("Production"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "ConvoLab.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
