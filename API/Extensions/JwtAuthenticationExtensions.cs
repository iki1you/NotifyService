using Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace API.Extensions
{
    public static class JwtAuthenticationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtIssuer = configuration["Jwt:Issuer"] ?? "NotifyService";
            var jwtAudience = configuration["Jwt:Audience"] ?? "NotifyServiceClients";
            var jwtSigningKey = configuration["Jwt:SigningKey"]
                ?? throw new InvalidOperationException("Jwt:SigningKey is not configured");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateLifetime = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async context =>
                        {
                            var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                            var projectIdValue = context.Principal?.FindFirst("ProjectId")?.Value;

                            if (string.IsNullOrWhiteSpace(jti) || !long.TryParse(projectIdValue, out var projectId))
                            {
                                context.Fail("Invalid token claims.");
                                return;
                            }

                            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                            var tokenEntity = await dbContext.ApiTokens
                                .AsNoTracking()
                                .FirstOrDefaultAsync(x => x.TokenId == jti && x.ProjectId == projectId);

                            if (tokenEntity == null || tokenEntity.IsRevoked || tokenEntity.ExpiresAt <= DateTime.UtcNow)
                            {
                                context.Fail("Token is revoked or expired.");
                            }
                        }
                    };
                });

            services.AddAuthorization();

            return services;
        }
    }
}
