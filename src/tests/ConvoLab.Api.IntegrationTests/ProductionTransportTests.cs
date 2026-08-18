using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ConvoLab.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConvoLab.Api.IntegrationTests;

[Collection("Production transport")]
public sealed class ProductionTransportTests : IClassFixture<ProductionTransportFactory>
{
    private readonly ProductionTransportFactory _factory;

    public ProductionTransportTests(ProductionTransportFactory factory) => _factory = factory;

    [Fact]
    public async Task Production_hsts_secure_antiforgery_cookie_and_minimal_health_are_enforced()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.convolab.test"),
            AllowAutoRedirect = false
        });
        var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.True(health.Headers.Contains("Strict-Transport-Security"));
        Assert.False(health.Headers.Contains("Server"));
        var healthBody = await health.Content.ReadAsStringAsync();
        Assert.DoesNotContain("checks", healthBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1.0.0-alpha.15", healthBody, StringComparison.Ordinal);

        var antiforgery = await client.GetAsync("/api/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, antiforgery.StatusCode);
        Assert.True(antiforgery.Headers.CacheControl?.NoStore);
        Assert.Contains(antiforgery.Headers.GetValues("Set-Cookie"), value =>
            value.Contains(ConvoLabAuthentication.AntiforgeryCookie, StringComparison.Ordinal)
            && value.Contains("secure", StringComparison.OrdinalIgnoreCase)
            && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
            && value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Untrusted_forwarded_https_header_does_not_bypass_redirection()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://api.convolab.test"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }

    [Fact]
    public async Task Trusted_symmetric_forwarded_headers_are_applied_before_https_redirection()
    {
        using var trustedFactory = new ProductionTransportFactory(trustLoopback: true);
        var client = trustedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://api.convolab.test"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-For", "198.51.100.25");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "api.convolab.test");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }
}

public sealed class ProductionTransportFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"convolab-production-{Guid.NewGuid():N}");
    private readonly bool _trustLoopback;

    public ProductionTransportFactory() : this(false) { }

    internal ProductionTransportFactory(bool trustLoopback)
    {
        _trustLoopback = trustLoopback;
        Directory.CreateDirectory(_root);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=ConvoLab Transport Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllText(Path.Combine(_root, "certificate.pem"), certificate.ExportCertificatePem());
        var privateKey = Path.Combine(_root, "private-key.pem");
        File.WriteAllText(privateKey, rsa.ExportPkcs8PrivateKeyPem());
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(privateKey, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "PostgreSql",
                ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=convolab;Username=convolab;Password=transport-test-value;Timeout=1",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["AllowedHosts"] = "api.convolab.test",
                ["Authentication:Mode"] = "Local",
                ["Authentication:Local:ProductionAllowed"] = "true",
                ["Http:UseHttpsRedirection"] = "true",
                ["Http:HttpsPort"] = "443",
                ["Proxy:Enabled"] = "true",
                ["Proxy:ForwardLimit"] = "1",
                ["Proxy:KnownProxies:0"] = "127.0.0.1",
                ["DataProtection:Provider"] = "SharedFileSystem",
                ["DataProtection:KeyRingPath"] = _root,
                ["DataProtection:CertificatePemPath"] = Path.Combine(_root, "certificate.pem"),
                ["DataProtection:PrivateKeyPemPath"] = Path.Combine(_root, "private-key.pem"),
                ["SafeMode:BlockAnalyticsExports"] = "true"
            }));
        if (_trustLoopback)
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, LoopbackConnectionStartupFilter>());
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var values = ProductionEnvironment();
        var previous = values.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (key, value) in values) Environment.SetEnvironmentVariable(key, value);
            return base.CreateHost(builder);
        }
        finally
        {
            foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key, value);
        }
    }

    private Dictionary<string, string> ProductionEnvironment() => new()
    {
        ["Database__Provider"] = "PostgreSql",
        ["ConnectionStrings__DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=convolab;Username=convolab;Password=transport-test-value;Timeout=1",
        ["Database__ApplyMigrationsOnStartup"] = "false",
        ["AllowedHosts"] = "api.convolab.test",
        ["Authentication__Mode"] = "Local",
        ["Authentication__Local__ProductionAllowed"] = "true",
        ["Http__UseHttpsRedirection"] = "true",
        ["Http__HttpsPort"] = "443",
        ["Proxy__Enabled"] = "true",
        ["Proxy__ForwardLimit"] = "1",
        ["Proxy__KnownProxies__0"] = "127.0.0.1",
        ["DataProtection__Provider"] = "SharedFileSystem",
        ["DataProtection__KeyRingPath"] = _root,
        ["DataProtection__CertificatePemPath"] = Path.Combine(_root, "certificate.pem"),
        ["DataProtection__PrivateKeyPemPath"] = Path.Combine(_root, "private-key.pem"),
        ["SafeMode__BlockAnalyticsExports"] = "true"
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}

internal sealed class LoopbackConnectionStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, continuation) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            await continuation();
        });
        next(app);
    };
}

[CollectionDefinition("Production transport", DisableParallelization = true)]
public sealed class ProductionTransportCollection;
