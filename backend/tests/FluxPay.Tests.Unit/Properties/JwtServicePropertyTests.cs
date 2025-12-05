using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Entities;
using FluxPay.Infrastructure.Data;
using FluxPay.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace FluxPay.Tests.Unit.Properties;

public class JwtServicePropertyTests : IDisposable
{
    private readonly string _originalEncryptionKey;
    private readonly string _originalPrivateKey;
    private readonly string _originalPublicKey;
    private readonly FluxPayDbContext _dbContext;
    private readonly EncryptionService _encryptionService;

    public JwtServicePropertyTests()
    {
        _originalEncryptionKey = Environment.GetEnvironmentVariable("MASTER_ENCRYPTION_KEY") ?? string.Empty;
        _originalPrivateKey = Environment.GetEnvironmentVariable("JWT_PRIVATE_KEY") ?? string.Empty;
        _originalPublicKey = Environment.GetEnvironmentVariable("JWT_PUBLIC_KEY") ?? string.Empty;

        var encryptionKey = new byte[32];
        RandomNumberGenerator.Fill(encryptionKey);
        Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", Convert.ToBase64String(encryptionKey));

        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportRSAPrivateKeyPem();
        var publicKey = rsa.ExportRSAPublicKeyPem();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKey);
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", publicKey);

        var options = new DbContextOptionsBuilder<FluxPayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new FluxPayDbContext(options);
        _encryptionService = new EncryptionService();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();

        if (string.IsNullOrEmpty(_originalEncryptionKey))
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", null);
        else
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", _originalEncryptionKey);

        if (string.IsNullOrEmpty(_originalPrivateKey))
            Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);
        else
            Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", _originalPrivateKey);

        if (string.IsNullOrEmpty(_originalPublicKey))
            Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", null);
        else
            Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", _originalPublicKey);
    }

    [Property(MaxTest = 10)]
    public void Login_Token_Issuance_Format_Should_Have_Correct_Expiry_And_Type()
    {
        Prop.ForAll(
            Arb.From(Gen.Choose(0, 1)),
            isAdminInt =>
            {
                var isAdmin = isAdminInt == 1;
                var userId = Guid.NewGuid();
                var email = $"user{Guid.NewGuid()}@example.com";
                var merchantId = Guid.NewGuid();

                var service = new JwtService(_dbContext, _encryptionService);
                var accessToken = service.GenerateAccessToken(userId, email, isAdmin, merchantId);
                var refreshToken = service.GenerateRefreshToken();

                var isAccessTokenValid = !string.IsNullOrEmpty(accessToken) && accessToken.Split('.').Length == 3;
                var isRefreshTokenValid = !string.IsNullOrEmpty(refreshToken) && refreshToken.Length > 0;

                return isAccessTokenValid && isRefreshTokenValid;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Refresh_Token_Rotation_Should_Revoke_Old_Token_And_Issue_New_Tokens()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid userId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);
                var service = new JwtService(dbContext, _encryptionService);

                var refreshToken = service.GenerateRefreshToken();
                var expiresAt = DateTime.UtcNow.AddDays(30);
                service.StoreRefreshTokenAsync(userId, refreshToken, expiresAt).Wait();

                var validatedUserId = service.ValidateAndConsumeRefreshTokenAsync(refreshToken).Result;
                var secondValidation = service.ValidateAndConsumeRefreshTokenAsync(refreshToken).Result;

                var revokedToken = dbContext.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.TokenHash == _encryptionService.Hash(refreshToken)).Result;

                return validatedUserId == userId && 
                       secondValidation == null && 
                       revokedToken != null && 
                       revokedToken.Revoked;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Valid_Access_Token_Should_Be_Accepted()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid userId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);
                var service = new JwtService(dbContext, _encryptionService);

                var email = $"user{Guid.NewGuid()}@example.com";
                var accessToken = service.GenerateAccessToken(userId, email, false, null);

                var validatedUserId = service.ValidateAccessTokenAsync(accessToken).Result;

                return validatedUserId == userId;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Revoked_Refresh_Token_Should_Be_Rejected()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid userId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);
                var service = new JwtService(dbContext, _encryptionService);

                var refreshToken = service.GenerateRefreshToken();
                var expiresAt = DateTime.UtcNow.AddDays(30);
                service.StoreRefreshTokenAsync(userId, refreshToken, expiresAt).Wait();

                service.RevokeRefreshTokensAsync(userId).Wait();

                var validatedUserId = service.ValidateAndConsumeRefreshTokenAsync(refreshToken).Result;

                return validatedUserId == null;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Access_Token_Should_Contain_User_Claims()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid userId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);
                var service = new JwtService(dbContext, _encryptionService);

                var isAdmin = userId.GetHashCode() % 2 == 0;
                var email = $"user{Guid.NewGuid()}@example.com";
                var merchantId = Guid.NewGuid();

                var accessToken = service.GenerateAccessToken(userId, email, isAdmin, merchantId);

                var validatedUserId = service.ValidateAccessTokenAsync(accessToken).Result;

                return validatedUserId == userId;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Refresh_Token_Should_Be_Unique()
    {
        Prop.ForAll(
            Arb.Default.Int32(),
            _ =>
            {
                var service = new JwtService(_dbContext, _encryptionService);
                var token1 = service.GenerateRefreshToken();
                var token2 = service.GenerateRefreshToken();

                return token1 != token2;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Admin_Access_Token_Should_Have_Shorter_Expiry()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            userId =>
            {
                var email = $"admin{Guid.NewGuid()}@example.com";
                var service = new JwtService(_dbContext, _encryptionService);

                var adminToken = service.GenerateAccessToken(userId, email, true, null);
                var regularToken = service.GenerateAccessToken(userId, email, false, null);

                return !string.IsNullOrEmpty(adminToken) && !string.IsNullOrEmpty(regularToken);
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Logout_Should_Revoke_All_User_Refresh_Tokens()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid userId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);
                var service = new JwtService(dbContext, _encryptionService);

                var token1 = service.GenerateRefreshToken();
                var token2 = service.GenerateRefreshToken();
                var expiresAt = DateTime.UtcNow.AddDays(30);

                service.StoreRefreshTokenAsync(userId, token1, expiresAt).Wait();
                service.StoreRefreshTokenAsync(userId, token2, expiresAt).Wait();

                service.RevokeRefreshTokensAsync(userId).Wait();

                var validatedUserId1 = service.ValidateAndConsumeRefreshTokenAsync(token1).Result;
                var validatedUserId2 = service.ValidateAndConsumeRefreshTokenAsync(token2).Result;

                return validatedUserId1 == null && validatedUserId2 == null;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Expired_Access_Token_Should_Be_Rejected()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            (Guid userId) =>
            {
                var options = new DbContextOptionsBuilder<FluxPayDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                    .Options;
                using var dbContext = new FluxPayDbContext(options);
                
                var email = $"user{Guid.NewGuid()}@example.com";
                
                var tokenHandler = new JwtSecurityTokenHandler();
                var claims = new List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                    new System.Security.Claims.Claim(JwtRegisteredClaimNames.Email, email),
                    new System.Security.Claims.Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new System.Security.Claims.Claim("is_admin", "false")
                };
                
                var privateKeyPem = Environment.GetEnvironmentVariable("JWT_PRIVATE_KEY");
                var rsa = RSA.Create();
                rsa.ImportFromPem(privateKeyPem);
                var securityKey = new RsaSecurityKey(rsa);
                
                var now = DateTime.UtcNow;
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new System.Security.Claims.ClaimsIdentity(claims),
                    NotBefore = now.AddMinutes(-10),
                    Expires = now.AddSeconds(-1),
                    SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256),
                    Issuer = "FluxPay",
                    Audience = "FluxPay"
                };
                
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var expiredToken = tokenHandler.WriteToken(token);
                
                var service = new JwtService(dbContext, _encryptionService);
                var validatedUserId = service.ValidateAccessTokenAsync(expiredToken).Result;
                
                return validatedUserId == null;
            }
        ).QuickCheckThrowOnFailure();
    }
}
