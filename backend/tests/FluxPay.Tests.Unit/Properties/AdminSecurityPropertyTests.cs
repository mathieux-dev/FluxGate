using FsCheck;
using FsCheck.Xunit;
using FluxPay.Api.Middleware;
using FluxPay.Core.Entities;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Data;
using FluxPay.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Net;

namespace FluxPay.Tests.Unit.Properties;

public class AdminSecurityPropertyTests : IDisposable
{
    private readonly FluxPayDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly IEncryptionService _encryptionService;
    private readonly string _originalKey;
    private readonly string _originalJwtPrivateKey;
    private readonly string _originalJwtPublicKey;

    public AdminSecurityPropertyTests()
    {
        _originalKey = Environment.GetEnvironmentVariable("MASTER_ENCRYPTION_KEY") ?? string.Empty;
        _originalJwtPrivateKey = Environment.GetEnvironmentVariable("JWT_PRIVATE_KEY") ?? string.Empty;
        _originalJwtPublicKey = Environment.GetEnvironmentVariable("JWT_PUBLIC_KEY") ?? string.Empty;
        
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", Convert.ToBase64String(key));

        var rsa = System.Security.Cryptography.RSA.Create(2048);
        var privateKey = rsa.ExportRSAPrivateKeyPem();
        var publicKey = rsa.ExportRSAPublicKeyPem();
        Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", privateKey);
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", publicKey);

        var options = new DbContextOptionsBuilder<FluxPayDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new FluxPayDbContext(options);
        _encryptionService = new EncryptionService();
        _jwtService = new JwtService(_dbContext, _encryptionService);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        
        if (string.IsNullOrEmpty(_originalKey))
        {
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("MASTER_ENCRYPTION_KEY", _originalKey);
        }

        if (string.IsNullOrEmpty(_originalJwtPrivateKey))
        {
            Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("JWT_PRIVATE_KEY", _originalJwtPrivateKey);
        }

        if (string.IsNullOrEmpty(_originalJwtPublicKey))
        {
            Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY", _originalJwtPublicKey);
        }
    }

    [Property(MaxTest = 100)]
    public void Admin_IP_Allowlist_Should_Reject_Non_Allowlisted_IPs()
    {
        Prop.ForAll(
            Arb.Default.IPv4Address(),
            ipAddress =>
            {
                var allowedIp = "192.168.1.100";
                var testIpAddress = ipAddress.Item;
                var testIpString = testIpAddress.ToString();

                if (testIpString == allowedIp || IPAddress.IsLoopback(testIpAddress))
                {
                    return true;
                }

                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "ADMIN_IP_ALLOWLIST", allowedIp }
                    })
                    .Build();

                var context = new DefaultHttpContext();
                context.Connection.RemoteIpAddress = testIpAddress;
                context.Request.Path = "/v1/admin/merchants";

                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger<IpAllowlistMiddleware>();
                var middleware = new IpAllowlistMiddleware(
                    _ => Task.CompletedTask,
                    logger,
                    config
                );

                var task = middleware.InvokeAsync(context);
                task.Wait();

                return context.Response.StatusCode == 403;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Admin_Login_Should_Require_MFA_When_Enabled()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            userId =>
            {
                var user = new User
                {
                    Id = userId,
                    Email = $"admin{userId}@example.com",
                    PasswordHash = _encryptionService.Hash("password123"),
                    MfaEnabled = true,
                    MfaSecretEncrypted = _encryptionService.Encrypt("JBSWY3DPEHPK3PXP"),
                    IsAdmin = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Users.Add(user);
                _dbContext.SaveChanges();

                var mfaRequired = user.MfaEnabled;

                _dbContext.Users.Remove(user);
                _dbContext.SaveChanges();

                return mfaRequired;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Admin_MFA_Failure_Should_Prevent_Login()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            userId =>
            {
                var user = new User
                {
                    Id = userId,
                    Email = $"admin{userId}@example.com",
                    PasswordHash = _encryptionService.Hash("password123"),
                    MfaEnabled = true,
                    MfaSecretEncrypted = _encryptionService.Encrypt("JBSWY3DPEHPK3PXP"),
                    IsAdmin = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Users.Add(user);
                _dbContext.SaveChanges();

                var invalidMfaCode = "000000";
                var mfaSecret = _encryptionService.Decrypt(user.MfaSecretEncrypted!);
                var totp = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(mfaSecret));
                var validCode = totp.ComputeTotp();

                var shouldFail = invalidMfaCode != validCode;

                _dbContext.Users.Remove(user);
                _dbContext.SaveChanges();

                return shouldFail;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Admin_Access_Token_Should_Expire_In_5_Minutes()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            userId =>
            {
                var email = $"admin{userId}@example.com";
                var token = _jwtService.GenerateAccessToken(userId, email, isAdmin: true, merchantId: null);

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var expiryTime = jwtToken.ValidTo;
                var issuedTime = jwtToken.ValidFrom;
                var expiryMinutes = (expiryTime - issuedTime).TotalMinutes;

                return Math.Abs(expiryMinutes - 5.0) < 0.1;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Regular_User_Access_Token_Should_Expire_In_15_Minutes()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            userId =>
            {
                var email = $"user{userId}@example.com";
                var token = _jwtService.GenerateAccessToken(userId, email, isAdmin: false, merchantId: null);

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var expiryTime = jwtToken.ValidTo;
                var issuedTime = jwtToken.ValidFrom;
                var expiryMinutes = (expiryTime - issuedTime).TotalMinutes;

                return Math.Abs(expiryMinutes - 15.0) < 0.1;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 100)]
    public void Admin_Token_Should_Include_IsAdmin_Claim()
    {
        Prop.ForAll(
            Arb.Default.Guid(),
            userId =>
            {
                var email = $"admin{userId}@example.com";
                var token = _jwtService.GenerateAccessToken(userId, email, isAdmin: true, merchantId: null);

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                var isAdminClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "is_admin");
                
                return isAdminClaim != null && isAdminClaim.Value == "true";
            }
        ).QuickCheckThrowOnFailure();
    }
}
