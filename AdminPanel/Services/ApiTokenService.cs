using Data;
using Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AdminPanel.Services
{
    public class ApiTokenService
    {
        private static readonly DateTime PermanentTokenExpiryUtc = new(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly string _signingKey;

        public ApiTokenService(
            IDbContextFactory<AppDbContext> contextFactory,
            IConfiguration configuration)
        {
            _contextFactory = contextFactory;
            _issuer = configuration["Jwt:Issuer"] ?? "NotifyService";
            _audience = configuration["Jwt:Audience"] ?? "NotifyServiceClients";
            _signingKey = configuration["Jwt:SigningKey"]
                ?? throw new InvalidOperationException("Jwt:SigningKey is not configured");
        }

        public async Task<List<ApiToken>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ApiTokens
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<IssuedTokenResult> IssueTokenAsync(long projectId, string? description)
        {
            var tokenId = Guid.NewGuid().ToString("N");
            var expiresAt = PermanentTokenExpiryUtc;
            var jwt = CreateJwt(projectId, tokenId, expiresAt);

            var tokenEntity = new ApiToken
            {
                ProjectId = projectId,
                TokenId = tokenId,
                Description = description,
                ExpiresAt = expiresAt,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            await using var context = await _contextFactory.CreateDbContextAsync();
            context.ApiTokens.Add(tokenEntity);
            await context.SaveChangesAsync();

            return new IssuedTokenResult(tokenEntity, jwt);
        }

        private string CreateJwt(long projectId, string tokenId, DateTime expiresAt)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var exp = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();

            var headerJson = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
            var payloadJson = JsonSerializer.Serialize(new
            {
                sub = $"project:{projectId}",
                jti = tokenId,
                iss = _issuer,
                aud = _audience,
                ProjectId = projectId,
                iat = now,
                exp
            });

            var header = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingKey));
            var signatureBytes = hmac.ComputeHash(Encoding.ASCII.GetBytes($"{header}.{payload}"));
            var signature = Base64UrlEncode(signatureBytes);

            return $"{header}.{payload}.{signature}";
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public async Task RevokeTokenAsync(long tokenId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var token = await context.ApiTokens.FindAsync(tokenId);
            if (token == null || token.IsRevoked)
            {
                return;
            }

            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public record IssuedTokenResult(ApiToken Token, string Jwt);
}
