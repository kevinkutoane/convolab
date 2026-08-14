using System.Data.Common;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using ConvoLab.Application.Operations;

namespace ConvoLab.Api.Operations;

public sealed class ProductionReadinessValidator(
    IConfiguration configuration,
    IHostEnvironment environment) : IProductionReadinessValidator
{
    public Task<ProductionReadinessResult> ValidateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var findings = Evaluate(configuration, environment);
        return Task.FromResult(new ProductionReadinessResult(findings.Count == 0, findings));
    }

    public static void ValidateStaticOrThrow(IConfiguration configuration, IHostEnvironment environment)
    {
        var findings = Evaluate(configuration, environment);
        if (findings.Count == 0) return;

        throw new InvalidOperationException(
            "Production readiness validation failed: " +
            string.Join("; ", findings.Select(item => $"{item.Code} ({item.ConfigurationKey})")));
    }

    public static IReadOnlyList<ProductionReadinessFinding> Evaluate(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsEnvironment("UAT"))
            return EvaluateUatDataProtection(configuration);
        if (!environment.IsProduction()) return [];
        var findings = new List<ProductionReadinessFinding>();
        void Reject(bool condition, string code, string key, string message)
        {
            if (condition) findings.Add(new(code, "Error", key, message));
        }

        var provider = configuration["Database:Provider"]?.Trim();
        Reject(!string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(provider, "Npgsql", StringComparison.OrdinalIgnoreCase),
            "production.database.postgresql_required", "Database:Provider",
            "Production requires PostgreSQL.");
        var connection = configuration.GetConnectionString("DefaultConnection");
        Reject(string.IsNullOrWhiteSpace(connection),
            "production.database.connection_required", "ConnectionStrings:DefaultConnection",
            "A PostgreSQL connection must be supplied externally.");
        if (!string.IsNullOrWhiteSpace(connection))
        {
            var validConnection = TryParseConnection(connection, out var values)
                                  && HasValue(values, "Host", "Server")
                                  && HasValue(values, "Database", "Initial Catalog")
                                  && HasValue(values, "Username", "User ID", "UserId")
                                  && HasValue(values, "Password");
            Reject(!validConnection, "production.database.connection_invalid",
                "ConnectionStrings:DefaultConnection", "The PostgreSQL connection is invalid.");
            Reject(IsPlaceholder(connection), "production.database.placeholder_credential",
                "ConnectionStrings:DefaultConnection", "Placeholder database credentials are prohibited.");
        }

        Reject(configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"),
            "production.database.automatic_migrations_forbidden", "Database:ApplyMigrationsOnStartup",
            "Production migrations must be performed as an explicit deployment step.");
        var allowedHosts = configuration["AllowedHosts"];
        Reject(string.IsNullOrWhiteSpace(allowedHosts)
               || allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Any(host => host.Contains('*') || host.Contains('+')),
            "production.http.hosts_required", "AllowedHosts",
            "Production requires an explicit host allowlist.");
        Reject(!configuration.GetValue("Http:UseHttpsRedirection", true),
            "production.http.https_required", "Http:UseHttpsRedirection",
            "HTTPS redirection cannot be disabled in Production.");

        var authMode = configuration["Authentication:Mode"]?.Trim();
        var localMode = string.Equals(authMode, "Local", StringComparison.OrdinalIgnoreCase);
        var entraMode = string.Equals(authMode, "Entra", StringComparison.OrdinalIgnoreCase);
        var hybridMode = string.Equals(authMode, "Hybrid", StringComparison.OrdinalIgnoreCase);
        Reject(!localMode && !entraMode && !hybridMode,
            "production.authentication.mode_unsupported", "Authentication:Mode",
            "Authentication mode must be Local, Entra, or Hybrid.");
        Reject(localMode && !configuration.GetValue<bool>("Authentication:Local:ProductionAllowed"),
            "production.authentication.local_unacknowledged", "Authentication:Local:ProductionAllowed",
            "Local authentication must be explicitly acknowledged for Production.");
        var entraSelected = entraMode || hybridMode;
        Reject(entraSelected && !configuration.GetValue<bool>("Authentication:Entra:Enabled"),
            "production.authentication.entra_disabled", "Authentication:Entra:Enabled",
            "The selected authentication mode requires Entra authentication.");
        var tenantId = configuration["Authentication:Entra:TenantId"]?.Trim();
        var clientId = configuration["Authentication:Entra:ClientId"]?.Trim();
        Reject(entraSelected && !Guid.TryParse(tenantId, out _),
            "production.authentication.entra_tenant_required", "Authentication:Entra:TenantId",
            "A specific Microsoft Entra tenant id is required.");
        Reject(entraSelected && !Guid.TryParse(clientId, out _),
            "production.authentication.entra_client_required", "Authentication:Entra:ClientId",
            "A Microsoft Entra application client id is required.");
        var authorityValue = configuration["Authentication:Entra:Authority"]?.Trim();
        var authorityValid = Uri.TryCreate(authorityValue, UriKind.Absolute, out var authority)
                             && authority.Scheme == Uri.UriSchemeHttps
                             && authority.Host.Equals("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase)
                             && authority.AbsolutePath.Trim('/').Split('/').FirstOrDefault()
                                 ?.Equals(tenantId, StringComparison.OrdinalIgnoreCase) == true
                             && authority.AbsolutePath.Trim('/').EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase);
        Reject(entraSelected && !authorityValid,
            "production.authentication.entra_authority_invalid", "Authentication:Entra:Authority",
            "Authority must be the configured tenant's HTTPS v2.0 Microsoft authority.");
        var callbackPath = configuration["Authentication:Entra:CallbackPath"];
        var signedOutPath = configuration["Authentication:Entra:SignedOutCallbackPath"];
        Reject(entraSelected && !IsLocalPath(callbackPath),
            "production.authentication.entra_callback_invalid", "Authentication:Entra:CallbackPath",
            "The OIDC callback must be a local application path.");
        Reject(entraSelected && !IsLocalPath(signedOutPath),
            "production.authentication.entra_signed_out_callback_invalid", "Authentication:Entra:SignedOutCallbackPath",
            "The signed-out callback must be a local application path.");
        var secretReference = configuration["Authentication:Entra:ClientSecretReference"]?.Trim();
        Reject(entraSelected && !IsSupportedSecretReference(secretReference),
            "production.authentication.entra_client_secret_reference_invalid", "Authentication:Entra:ClientSecretReference",
            "Client authentication requires an env, docker-secret, or azure-key-vault reference.");
        Reject(entraMode && configuration.GetValue("Authentication:Local:Enabled", true),
            "production.authentication.local_enabled_in_entra_mode", "Authentication:Local:Enabled",
            "Ordinary local authentication must be disabled in Entra-only mode.");
        Reject(hybridMode && configuration.GetValue("Authentication:Local:Enabled", true)
                          && !configuration.GetValue<bool>("Authentication:Local:HybridAccessAcknowledged"),
            "production.authentication.hybrid_local_unacknowledged", "Authentication:Local:HybridAccessAcknowledged",
            "Hybrid local access requires explicit operational acknowledgement.");
        var breakGlass = configuration.GetValue<bool>("Authentication:Local:BreakGlassEnabled");
        var localLoginRate = configuration.GetValue("Authentication:Local:LoginRateLimitPerMinute", 10);
        var breakGlassAttempts = configuration.GetValue("Authentication:Local:BreakGlass:MaximumAttempts", 5);
        var breakGlassLockout = configuration.GetValue("Authentication:Local:BreakGlass:LockoutMinutes", 15);
        var breakGlassRate = configuration.GetValue("Authentication:Local:BreakGlass:RateLimitPerMinute", 3);
        Reject(localLoginRate is < 1 or > 100,
            "production.authentication.local_rate_limit_invalid", "Authentication:Local:LoginRateLimitPerMinute",
            "The ordinary login rate limit must be between 1 and 100 attempts per minute.");
        Reject(breakGlassAttempts is < 3 or > 10,
            "production.authentication.break_glass_attempts_invalid", "Authentication:Local:BreakGlass:MaximumAttempts",
            "Break-glass maximum attempts must be between 3 and 10.");
        Reject(breakGlassLockout is < 1 or > 1440,
            "production.authentication.break_glass_lockout_invalid", "Authentication:Local:BreakGlass:LockoutMinutes",
            "Break-glass lockout must be between 1 and 1440 minutes.");
        Reject(breakGlassRate is < 1 or > 60,
            "production.authentication.break_glass_rate_limit_invalid", "Authentication:Local:BreakGlass:RateLimitPerMinute",
            "The break-glass rate limit must be between 1 and 60 attempts per minute.");
        Reject(breakGlass && !entraMode && !hybridMode,
            "production.authentication.break_glass_mode_invalid", "Authentication:Local:BreakGlassEnabled",
            "Break-glass is permitted only in Entra or Hybrid mode.");
        Reject(breakGlass && !configuration.GetValue<bool>("Authentication:Local:BreakGlassAccountConfigured"),
            "production.authentication.break_glass_account_required", "Authentication:Local:BreakGlassAccountConfigured",
            "Break-glass requires an explicitly provisioned Platform Administrator account.");
        Reject(entraSelected && configuration.GetValue("Authentication:Entra:AllowInvitationLinking", true)
                             && configuration.GetValue<int>("Authentication:Entra:InvitationExpiryHours") is < 1 or > 168,
            "production.authentication.invitation_expiry_invalid", "Authentication:Entra:InvitationExpiryHours",
            "Invitation expiry must be between one hour and seven days.");
        var publicOriginValue = configuration["Authentication:Entra:PublicOrigin"];
        var publicOriginValid = Uri.TryCreate(publicOriginValue, UriKind.Absolute, out var publicOrigin)
                                && publicOrigin.Scheme == Uri.UriSchemeHttps
                                && string.IsNullOrEmpty(publicOrigin.AbsolutePath.Trim('/'));
        Reject(entraSelected && !publicOriginValid,
            "production.authentication.public_origin_invalid", "Authentication:Entra:PublicOrigin",
            "A host-only HTTPS public origin is required for OIDC callbacks.");
        if (entraSelected && publicOriginValid)
        {
            var hosts = allowedHosts?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            Reject(!hosts.Contains(publicOrigin!.Host, StringComparer.OrdinalIgnoreCase),
                "production.authentication.callback_host_not_allowed", "AllowedHosts",
                "AllowedHosts must contain the public OIDC callback host.");
        }

        if (configuration.GetValue<bool>("Proxy:Enabled"))
        {
            var proxies = configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [];
            var networks = configuration.GetSection("Proxy:KnownNetworks").Get<string[]>() ?? [];
            Reject(proxies.Length == 0 && networks.Length == 0,
                "production.proxy.trust_boundary_required", "Proxy:KnownProxies",
                "An enabled proxy boundary requires explicit proxy or network allowlists.");
            var forwardLimit = configuration.GetValue<int?>("Proxy:ForwardLimit");
            Reject(forwardLimit is null or < 1 or > 5,
                "production.proxy.forward_limit_invalid", "Proxy:ForwardLimit",
                "The proxy forward limit must be between one and five.");
            Reject(proxies.Any(value => !IPAddress.TryParse(value, out var address)
                                        || address.Equals(IPAddress.Any)
                                        || address.Equals(IPAddress.IPv6Any)),
                "production.proxy.address_invalid", "Proxy:KnownProxies",
                "Every trusted proxy must be an IP address.");
            Reject(networks.Any(value => !IsNetwork(value)),
                "production.proxy.network_invalid", "Proxy:KnownNetworks",
                "Every trusted network must use an IP/CIDR prefix.");
        }

        var keyPath = configuration["DataProtection:KeyRingPath"];
        var certificatePath = configuration["DataProtection:CertificatePemPath"];
        var privateKeyPath = configuration["DataProtection:PrivateKeyPemPath"];
        Reject(!string.Equals(configuration["DataProtection:Provider"], "SharedFileSystem", StringComparison.OrdinalIgnoreCase),
            "production.data_protection.shared_storage_required", "DataProtection:Provider",
            "Production requires the SharedFileSystem data-protection provider.");
        Reject(!IsAbsolute(keyPath), "production.data_protection.key_ring_path_invalid",
            "DataProtection:KeyRingPath", "The key-ring path must be absolute.");
        Reject(IsAbsolute(keyPath) && !CanWriteDirectory(keyPath!),
            "production.data_protection.key_ring_unwritable", "DataProtection:KeyRingPath",
            "The shared key-ring directory must exist and be writable.");
        Reject(!IsReadableFile(certificatePath), "production.data_protection.certificate_unavailable",
            "DataProtection:CertificatePemPath", "The mounted certificate PEM is unavailable.");
        Reject(!IsReadableFile(privateKeyPath), "production.data_protection.private_key_unavailable",
            "DataProtection:PrivateKeyPemPath", "The mounted private-key PEM is unavailable.");
        Reject(IsReadableFile(privateKeyPath) && HasUnsafeUnixPermissions(privateKeyPath!),
            "production.data_protection.private_key_permissions_unsafe", "DataProtection:PrivateKeyPemPath",
            "The mounted private key cannot be writable by group or other users.");
        Reject(IsReadableFile(certificatePath) && IsReadableFile(privateKeyPath)
               && !CertificatePairIsValid(certificatePath!, privateKeyPath!),
            "production.data_protection.certificate_pair_invalid", "DataProtection:CertificatePemPath",
            "The certificate and private-key PEM pair is invalid.");

        Reject(configuration.GetValue<bool?>("SafeMode:BlockAnalyticsExports") is null,
            "production.safe_mode.analytics_export_decision_required", "SafeMode:BlockAnalyticsExports",
            "Production must explicitly decide whether safe mode blocks Analytics exports.");

        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        Reject(!string.IsNullOrWhiteSpace(otlpEndpoint)
               && (!Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlp)
                   || otlp.Scheme is not ("http" or "https")
                   || !string.IsNullOrWhiteSpace(otlp.UserInfo)),
            "production.telemetry.otlp_endpoint_invalid", "OTEL_EXPORTER_OTLP_ENDPOINT",
            "The OTLP endpoint must be an absolute HTTP or HTTPS URI.");
        Reject(!IsExporterSettingValid(configuration["OTEL_TRACES_EXPORTER"]
                                       ?? Environment.GetEnvironmentVariable("OTEL_TRACES_EXPORTER")),
            "production.telemetry.trace_exporter_invalid", "OTEL_TRACES_EXPORTER",
            "The trace exporter must be otlp, none, or unspecified.");
        Reject(!IsExporterSettingValid(configuration["OTEL_METRICS_EXPORTER"]
                                       ?? Environment.GetEnvironmentVariable("OTEL_METRICS_EXPORTER")),
            "production.telemetry.metric_exporter_invalid", "OTEL_METRICS_EXPORTER",
            "The metric exporter must be otlp, none, or unspecified.");
        var protocol = configuration["OTEL_EXPORTER_OTLP_PROTOCOL"]
                       ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        Reject(!string.IsNullOrWhiteSpace(protocol)
               && protocol is not ("grpc" or "http/protobuf"),
            "production.telemetry.otlp_protocol_invalid", "OTEL_EXPORTER_OTLP_PROTOCOL",
            "The OTLP protocol must be grpc or http/protobuf.");

        return findings;
    }

    private static IReadOnlyList<ProductionReadinessFinding> EvaluateUatDataProtection(
        IConfiguration configuration)
    {
        var findings = new List<ProductionReadinessFinding>();
        if (!string.Equals(
                configuration["DataProtection:Provider"],
                "SharedFileSystem",
                StringComparison.OrdinalIgnoreCase))
            findings.Add(new(
                "uat.data_protection.shared_storage_required", "Error",
                "DataProtection:Provider",
                "UAT requires the SharedFileSystem data-protection provider."));

        var keyPath = configuration["DataProtection:KeyRingPath"];
        if (!IsAbsolute(keyPath))
            findings.Add(new(
                "uat.data_protection.key_ring_path_invalid", "Error",
                "DataProtection:KeyRingPath", "The UAT key-ring path must be absolute."));
        else if (!CanWriteDirectory(keyPath!))
            findings.Add(new(
                "uat.data_protection.key_ring_unwritable", "Error",
                "DataProtection:KeyRingPath", "The UAT shared key-ring directory must be writable."));

        return findings;
    }

    private static bool TryParseConnection(string connection, out DbConnectionStringBuilder values)
    {
        values = new DbConnectionStringBuilder();
        try
        {
            values.ConnectionString = connection;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsPlaceholder(string value) =>
        new[] { "change-me", "changeme", "placeholder", "your-", "convolab_password", "password=admin", "password=postgres" }
            .Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static bool IsLocalPath(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.StartsWith("/", StringComparison.Ordinal)
        && !value.StartsWith("//", StringComparison.Ordinal) && !value.Contains('\\');

    private static bool IsSupportedSecretReference(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && new[] { "env:", "docker-secret:", "azure-key-vault:" }
            .Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                           && value.Length > prefix.Length);

    private static bool IsExporterSettingValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var exporters = value.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return exporters.Length == 1
               && exporters[0] is "otlp" or "none";
    }

    private static bool HasValue(DbConnectionStringBuilder values, params string[] keys) =>
        keys.Any(key => values.TryGetValue(key, out var value)
                        && !string.IsNullOrWhiteSpace(Convert.ToString(value)));

    private static bool IsAbsolute(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path);

    private static bool IsReadableFile(string? path)
    {
        if (!IsAbsolute(path) || !File.Exists(path)) return false;
        try
        {
            using var stream = File.Open(path!, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.CanRead;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static bool CanWriteDirectory(string path)
    {
        if (!Directory.Exists(path)) return false;
        var probe = Path.Combine(path, $".convolab-write-probe-{Guid.NewGuid():N}");
        try
        {
            using (var stream = new FileStream(
                       probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       1, FileOptions.DeleteOnClose))
            {
                stream.WriteByte(0);
            }
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        finally
        {
            if (File.Exists(probe)) File.Delete(probe);
        }
    }

    private static bool HasUnsafeUnixPermissions(string path)
    {
        if (OperatingSystem.IsWindows()) return false;
        var mode = File.GetUnixFileMode(path);
        return mode.HasFlag(UnixFileMode.GroupWrite)
               || mode.HasFlag(UnixFileMode.GroupRead)
               || mode.HasFlag(UnixFileMode.OtherWrite)
               || mode.HasFlag(UnixFileMode.OtherRead);
    }

    private static bool IsNetwork(string value)
    {
        var parts = value.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address)
            || !int.TryParse(parts[1], out var prefix)) return false;
        var max = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        return prefix is > 0 && prefix <= max;
    }

    private static bool CertificatePairIsValid(string certificatePath, string privateKeyPath)
    {
        try
        {
            using var certificate = X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);
            return certificate.HasPrivateKey;
        }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }
}
