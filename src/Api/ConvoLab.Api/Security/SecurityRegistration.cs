using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace ConvoLab.Api.Security;

public static class SecurityRegistration
{
    public static IServiceCollection AddConvoLabSecurity(this IServiceCollection services)
    {
        services.AddAuthentication(ConvoLabAuthentication.Scheme)
            .AddScheme<AuthenticationSchemeOptions, ConvoLabAuthenticationHandler>(ConvoLabAuthentication.Scheme, _ => { });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder(ConvoLabAuthentication.Scheme).RequireAuthenticatedUser().Build();
            foreach (var permission in typeof(WorkspacePermissions).GetFields().Where(field => field.IsLiteral).Select(field => field.GetRawConstantValue()).OfType<string>())
                options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
            options.AddPolicy("PlatformAdministrator", policy => policy.RequireClaim("platform_administrator", "true"));
        });
        services.AddScoped<IPasswordHasher<IdentityUserRecord>, PasswordHasher<IdentityUserRecord>>();
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = ConvoLabAuthentication.AntiforgeryCookie;
            options.Cookie.HttpOnly = false;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.HeaderName = ConvoLabAuthentication.AntiforgeryHeader;
        });
        return services;
    }
}
