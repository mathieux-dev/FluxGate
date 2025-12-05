using Microsoft.EntityFrameworkCore;
using FluxPay.Core.Entities;

namespace FluxPay.Infrastructure.Data;

public class FluxPayDbContext : DbContext
{
    public FluxPayDbContext(DbContextOptions<FluxPayDbContext> options) : base(options)
    {
    }

    public DbSet<Merchant> Merchants { get; set; } = null!;
    public DbSet<ApiKey> ApiKeys { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<MerchantWebhook> MerchantWebhooks { get; set; } = null!;
    public DbSet<WebhookReceived> WebhooksReceived { get; set; } = null!;
    public DbSet<WebhookDelivery> WebhookDeliveries { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.ToTable("merchants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ProviderConfigEncrypted).IsRequired().HasColumnType("text");
            entity.Property(e => e.Active).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.Active);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("api_keys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KeyId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.KeyHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.KeySecretEncrypted).IsRequired().HasColumnType("text");
            entity.Property(e => e.Active).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.ApiKeys)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.KeyId).IsUnique();
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.Active);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.EmailHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.DocumentHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.Customers)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.EmailHash);
            entity.HasIndex(e => e.DocumentHash);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AmountCents).IsRequired();
            entity.Property(e => e.Method).IsRequired().HasConversion<string>();
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProviderPaymentId).HasMaxLength(255);
            entity.Property(e => e.ProviderPayload).HasColumnType("jsonb");
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.IsTest).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.Payments)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Payments)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.MerchantId, e.Status });
            entity.HasIndex(e => new { e.MerchantId, e.CreatedAt });
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasConversion<string>();
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.AmountCents).IsRequired();
            entity.Property(e => e.ProviderTxId).HasMaxLength(255);
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.IsTest).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Payment)
                .WithMany(p => p.Transactions)
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProviderSubscriptionId).HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.AmountCents).IsRequired();
            entity.Property(e => e.Interval).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.Subscriptions)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Subscriptions)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextBillingDate);
        });

        modelBuilder.Entity<MerchantWebhook>(entity =>
        {
            entity.ToTable("merchant_webhooks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EndpointUrl).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.SecretEncrypted).IsRequired().HasColumnType("text");
            entity.Property(e => e.Active).IsRequired();
            entity.Property(e => e.RetryCount).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.Webhooks)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.MerchantId);
        });

        modelBuilder.Entity<WebhookReceived>(entity =>
        {
            entity.ToTable("webhooks_received");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.Processed).IsRequired();
            entity.Property(e => e.ReceivedAt).IsRequired();
            
            entity.HasIndex(e => e.Provider);
            entity.HasIndex(e => e.Processed);
            entity.HasIndex(e => e.ReceivedAt);
        });

        modelBuilder.Entity<WebhookDelivery>(entity =>
        {
            entity.ToTable("webhook_deliveries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Payload).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.AttemptCount).IsRequired();
            entity.Property(e => e.LastError).HasColumnType("text");
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Payment)
                .WithMany(p => p.WebhookDeliveries)
                .HasForeignKey(e => e.PaymentId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.PaymentId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRetryAt);
            entity.HasIndex(e => new { e.Status, e.NextRetryAt });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Actor).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Changes).HasColumnType("jsonb");
            entity.Property(e => e.Signature).IsRequired().HasMaxLength(512);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.AuditLogs)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.MerchantId);
            entity.HasIndex(e => e.ResourceType);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.MerchantId, e.CreatedAt });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.MfaSecretEncrypted).HasColumnType("text");
            entity.Property(e => e.MfaEnabled).IsRequired();
            entity.Property(e => e.IsAdmin).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Merchant)
                .WithMany(m => m.Users)
                .HasForeignKey(e => e.MerchantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.MerchantId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Revoked).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => new { e.UserId, e.Revoked });
        });
    }
}
