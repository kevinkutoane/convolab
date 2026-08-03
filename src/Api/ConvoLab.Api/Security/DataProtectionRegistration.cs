using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace ConvoLab.Api.Security;

public static class DataProtectionRegistration
{
    public static IServiceCollection AddConvoLabDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName("ConvoLab");
        if (environment.IsEnvironment("Testing"))
        {
            dataProtection.UseEphemeralDataProtectionProvider();
            return services;
        }

        if (environment.IsDevelopment())
        {
            var configuredPath = configuration["DataProtection:KeyRingPath"];
            var path = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(environment.ContentRootPath, ".data-protection")
                : configuredPath;
            Directory.CreateDirectory(path);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(path));
            return services;
        }

        var keyRingPath = configuration["DataProtection:KeyRingPath"]!;
        Directory.CreateDirectory(keyRingPath);
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        if (environment.IsProduction())
        {
            var certificate = X509Certificate2.CreateFromPemFile(
                configuration["DataProtection:CertificatePemPath"]!,
                configuration["DataProtection:PrivateKeyPemPath"]!);
            dataProtection.ProtectKeysWithCertificate(certificate);
        }

        return services;
    }
}
