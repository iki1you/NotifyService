using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AdminPanel.Controllers
{
    public static class AuthEndpoints
    {
        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/auth/login", async (HttpContext httpContext, IConfiguration configuration) =>
            {
                var form = await httpContext.Request.ReadFormAsync();
                var token = form["token"].ToString();
                var returnUrl = SanitizeReturnUrl(form["returnUrl"].ToString());
                var configuredToken = configuration["AdminAuth:Token"];

                if (string.IsNullOrWhiteSpace(configuredToken) || !IsValidToken(token, configuredToken))
                {
                    var failedReturnUrl = Uri.EscapeDataString(returnUrl);
                    return Results.Redirect($"/login?error=1&returnUrl={failedReturnUrl}");
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "admin"),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await httpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    });

                return Results.Redirect(returnUrl);
            })
            .AllowAnonymous()
            .DisableAntiforgery();

            endpoints.MapPost("/auth/logout", async (HttpContext httpContext) =>
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Redirect("/login");
            })
            .RequireAuthorization()
            .DisableAntiforgery();

            return endpoints;
        }

        private static bool IsValidToken(string provided, string expected)
        {
            if (string.IsNullOrWhiteSpace(provided))
            {
                return false;
            }

            var providedBytes = Encoding.UTF8.GetBytes(provided);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);

            return providedBytes.Length == expectedBytes.Length
                && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }

        private static string SanitizeReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return "/";
            }

            if (!returnUrl.StartsWith('/'))
            {
                return "/";
            }

            if (returnUrl.StartsWith("//") || returnUrl.StartsWith("/\\"))
            {
                return "/";
            }

            return returnUrl;
        }
    }
}
