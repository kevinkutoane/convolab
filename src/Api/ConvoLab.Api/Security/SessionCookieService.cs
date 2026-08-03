namespace ConvoLab.Api.Security;

public sealed class SessionCookieService(IHostEnvironment environment)
{
    public void Write(HttpResponse response, string token, DateTimeOffset expires) =>
        response.Cookies.Append(ConvoLabAuthentication.SessionCookie, token, Options(expires));

    public void Delete(HttpResponse response) =>
        response.Cookies.Delete(ConvoLabAuthentication.SessionCookie, Options(null));

    private CookieOptions Options(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = environment.IsProduction(),
        SameSite = SameSiteMode.Strict,
        Expires = expires,
        Path = "/",
        IsEssential = true
    };
}
