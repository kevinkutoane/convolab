using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ConvoLab.Application.Operations;

namespace ConvoLab.Api.Security;

public static class SecurityRegistration
{
    public static IServiceCollection AddConvoLabSecurity(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var configured = configuration.GetSection("Authentication").Get<ConvoLab.Application.Operations.AuthenticationOptions>() ?? new();
        var entra = configured.Entra;
        var authority = string.IsNullOrWhiteSpace(entra.Authority)
            ? "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0"
            : entra.Authority.TrimEnd('/');
        var clientId = string.IsNullOrWhiteSpace(entra.ClientId) ? "not-configured" : entra.ClientId;
        services.AddScoped<ConvoLabOpenIdConnectEvents>();
        services.AddSingleton<EntraDependencyEvidence>();
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ConvoLabAuthentication.Scheme;
                options.DefaultChallengeScheme = ConvoLabAuthentication.Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, ConvoLabAuthenticationHandler>(ConvoLabAuthentication.Scheme, _ => { })
            .AddCookie(EntraAuthentication.ExternalCookieScheme, options =>
            {
                options.Cookie.Name = "convolab_external";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.None;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddOpenIdConnect(EntraAuthentication.Scheme, options =>
            {
                options.Authority = authority;
                options.ClientId = clientId;
                options.CallbackPath = entra.CallbackPath;
                options.SignedOutCallbackPath = entra.SignedOutCallbackPath;
                options.SignedOutRedirectUri = EntraAuthentication.SafeReturnUrl(entra.PostLogoutRedirectUri);
                options.SignInScheme = EntraAuthentication.ExternalCookieScheme;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = false;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = true;
                options.Scope.Clear();
                options.Scope.Add("openid"); options.Scope.Add("profile"); options.Scope.Add("email");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = clientId,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    NameClaimType = "name"
                };
                options.NonceCookie.HttpOnly = true;
                options.NonceCookie.SameSite = SameSiteMode.None;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.CorrelationCookie.HttpOnly = true;
                options.CorrelationCookie.SameSite = SameSiteMode.None;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.EventsType = typeof(ConvoLabOpenIdConnectEvents);
            });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder(ConvoLabAuthentication.Scheme).RequireAuthenticatedUser().Build();
            foreach (var permission in typeof(WorkspacePermissions).GetFields().Where(field => field.IsLiteral).Select(field => field.GetRawConstantValue()).OfType<string>())
                options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
            options.AddPolicy("PlatformAdministrator", policy => policy.RequireClaim("platform_administrator", "true"));
        });
        services.AddScoped<IPasswordHasher<IdentityUserRecord>, PasswordHasher<IdentityUserRecord>>();
        services.AddSingleton<SessionCookieService>();
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = ConvoLabAuthentication.AntiforgeryCookie;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsProduction()
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.Cookie.IsEssential = true;
            options.HeaderName = ConvoLabAuthentication.AntiforgeryHeader;
        });
        return services;
    }
}
