using FsCheck;
using FsCheck.Xunit;
using FluxPay.Core.Services;
using FluxPay.Infrastructure.Redis;
using FluxPay.Infrastructure.Services;
using NSubstitute;
using StackExchange.Redis;

namespace FluxPay.Tests.Unit.Properties;

public class AntifraudServicePropertyTests
{
    [Property(MaxTest = 10)]
    public void IP_Velocity_Limit_Enforcement_Should_Reject_Excessive_Requests(NonEmptyString ipAddress)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(ipAddress.Get)),
            ip =>
            {
                var mockDb = Substitute.For<IDatabase>();
                var mockRedisFactory = new RedisConnectionFactory(mockDb);
                
                mockDb.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(false));
                
                var mockAuditService = Substitute.For<IAuditService>();
                var mockRateLimiter = Substitute.For<IRateLimiter>();

                var requestCount = 0;
                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
                    .Returns(_ =>
                    {
                        requestCount++;
                        var isAllowed = requestCount <= 10;
                        return Task.FromResult(new RateLimitResult
                        {
                            IsAllowed = isAllowed,
                            RemainingRequests = Math.Max(0, 10 - requestCount),
                            ResetTime = DateTime.UtcNow.AddMinutes(1)
                        });
                    });

                var service = new AntifraudService(mockRedisFactory, mockAuditService, mockRateLimiter);

                AntifraudResult? lastResult = null;
                for (int i = 0; i < 11; i++)
                {
                    lastResult = service.CheckPaymentAsync(ip, null, null, 10000).Result;
                }

                return lastResult != null && 
                       !lastResult.IsAllowed && 
                       lastResult.TriggeredRule == AntifraudRuleType.IpVelocity;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Blacklist_Enforcement_Should_Reject_Blacklisted_CPF(PositiveInt amount)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements("00000000000", "11111111111", "99999999999")),
            cpf =>
            {
                var mockDb = Substitute.For<IDatabase>();
                var mockRedisFactory = new RedisConnectionFactory(mockDb);
                
                mockDb.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(false));
                
                var mockAuditService = Substitute.For<IAuditService>();
                var mockRateLimiter = Substitute.For<IRateLimiter>();

                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
                    .Returns(Task.FromResult(new RateLimitResult
                    {
                        IsAllowed = true,
                        RemainingRequests = 10,
                        ResetTime = DateTime.UtcNow.AddMinutes(1)
                    }));

                var service = new AntifraudService(mockRedisFactory, mockAuditService, mockRateLimiter);
                var result = service.CheckPaymentAsync("192.168.1.1", cpf, null, amount.Get).Result;

                return !result.IsAllowed && result.TriggeredRule == AntifraudRuleType.CpfBlacklist;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Blacklist_Enforcement_Should_Reject_Blacklisted_BIN(PositiveInt amount)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements("000000", "111111", "999999")),
            bin =>
            {
                var mockDb = Substitute.For<IDatabase>();
                var mockRedisFactory = new RedisConnectionFactory(mockDb);
                
                mockDb.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(false));
                
                var mockAuditService = Substitute.For<IAuditService>();
                var mockRateLimiter = Substitute.For<IRateLimiter>();

                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
                    .Returns(Task.FromResult(new RateLimitResult
                    {
                        IsAllowed = true,
                        RemainingRequests = 10,
                        ResetTime = DateTime.UtcNow.AddMinutes(1)
                    }));

                var service = new AntifraudService(mockRedisFactory, mockAuditService, mockRateLimiter);
                var result = service.CheckPaymentAsync("192.168.1.1", null, bin, amount.Get).Result;

                return !result.IsAllowed && result.TriggeredRule == AntifraudRuleType.BinBlacklist;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Adaptive_IP_Blocking_Should_Block_After_Multiple_Failures(NonEmptyString ipAddress)
    {
        Prop.ForAll(
            Arb.From(Gen.Elements(ipAddress.Get)),
            ip =>
            {
                var mockDb = Substitute.For<IDatabase>();
                var mockRedisFactory = new RedisConnectionFactory(mockDb);
                var mockAuditService = Substitute.For<IAuditService>();
                var mockRateLimiter = Substitute.For<IRateLimiter>();

                var failedAttempts = 0L;
                mockDb.SortedSetLengthAsync(Arg.Any<RedisKey>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<Exclude>(), Arg.Any<CommandFlags>())
                    .Returns(_ =>
                    {
                        failedAttempts++;
                        return Task.FromResult(failedAttempts);
                    });

                mockDb.KeyExistsAsync(Arg.Is<RedisKey>(k => k.ToString().Contains("blocked")), Arg.Any<CommandFlags>())
                    .Returns(_ => Task.FromResult(failedAttempts >= 3));

                var mockTransaction = Substitute.For<ITransaction>();
                mockDb.CreateTransaction().Returns(mockTransaction);
                mockTransaction.ExecuteAsync(Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));
                mockTransaction.SortedSetRemoveRangeByScoreAsync(Arg.Any<RedisKey>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<Exclude>(), Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(0L));
                mockTransaction.SortedSetAddAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<double>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(true));
                mockTransaction.SortedSetLengthAsync(Arg.Any<RedisKey>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<Exclude>(), Arg.Any<CommandFlags>())
                    .Returns(_ =>
                    {
                        failedAttempts++;
                        return Task.FromResult(failedAttempts);
                    });
                mockTransaction.KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(true));
                
                mockDb.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(true));

                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
                    .Returns(Task.FromResult(new RateLimitResult
                    {
                        IsAllowed = true,
                        RemainingRequests = 10,
                        ResetTime = DateTime.UtcNow.AddMinutes(1)
                    }));

                var service = new AntifraudService(mockRedisFactory, mockAuditService, mockRateLimiter);

                for (int i = 0; i < 3; i++)
                {
                    service.RecordFailedAttemptAsync(ip).Wait();
                }

                var isBlocked = service.IsIpBlockedAsync(ip).Result;
                var result = service.CheckPaymentAsync(ip, null, null, 10000).Result;

                return isBlocked && !result.IsAllowed && result.TriggeredRule == AntifraudRuleType.AdaptiveIpBlock;
            }
        ).QuickCheckThrowOnFailure();
    }

    [Property(MaxTest = 10)]
    public void Antifraud_Audit_Logging_Should_Log_All_Triggered_Rules(NonEmptyString ipAddress, PositiveInt amount)
    {
        Prop.ForAll(
            Arb.From(Gen.Zip(Gen.Elements(ipAddress.Get), Gen.Elements("00000000000", "11111111111"))),
            data =>
            {
                var (ip, cpf) = data;
                var mockDb = Substitute.For<IDatabase>();
                var mockRedisFactory = new RedisConnectionFactory(mockDb);
                
                mockDb.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
                    .Returns(Task.FromResult(false));
                
                var mockAuditService = Substitute.For<IAuditService>();
                var mockRateLimiter = Substitute.For<IRateLimiter>();

                mockRateLimiter.CheckRateLimitAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
                    .Returns(Task.FromResult(new RateLimitResult
                    {
                        IsAllowed = true,
                        RemainingRequests = 10,
                        ResetTime = DateTime.UtcNow.AddMinutes(1)
                    }));

                var auditLogCalled = false;
                mockAuditService.LogAsync(Arg.Any<AuditEntry>())
                    .Returns(_ =>
                    {
                        auditLogCalled = true;
                        return Task.CompletedTask;
                    });

                var service = new AntifraudService(mockRedisFactory, mockAuditService, mockRateLimiter);
                var result = service.CheckPaymentAsync(ip, cpf, null, amount.Get).Result;

                return !result.IsAllowed && auditLogCalled;
            }
        ).QuickCheckThrowOnFailure();
    }
}

