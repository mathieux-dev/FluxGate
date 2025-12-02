# Design Document

## Overview

FluxPay is an enterprise-grade white-label payment gateway built on .NET 7 that prioritizes security, compliance, and reliability. The system acts as an abstraction layer between merchants and payment providers (Pagar.me for cards/subscriptions, Gerencianet for PIX/boleto), implementing tokenization to avoid PCI-DSS scope, cryptographic request signing for API security, bidirectional webhook infrastructure with HMAC verification, and comprehensive audit trails.

The architecture follows clean architecture principles with clear separation between API layer, domain logic, infrastructure adapters, and background workers. All sensitive data is encrypted at rest using AES-256-GCM, secrets are managed through KMS/Render Secrets, and the system implements defense-in-depth with multiple security layers including rate limiting, replay protection, IP allowlisting for admin functions, and MFA.

## Architecture

### High-Level Architecture

```mermaid
graph TD
    subgraph Frontend
        A[Next.js Dashboard] -->|REST API| B[Auth Gateway]
    end
    
    subgraph Backend
        B --> C[Payments API - .NET 7]
        C --> D[PostgreSQL]
        C --> E[Redis - Nonces/Rate Limit]
        C --> F[Background Workers]
    end
    
    C --> G[Logflare / Grafana]
    C --> H[Secrets Manager / KMS]
    C --> Provider1[Pagar.me]
    C --> Provider2[Gerencianet]
    
    F --> D
    F --> Provider1
    F --> Provider2
```

### Deployment Architecture

- **API Service**: ASP.NET Core Web API running in Docker on Render Web Service
- **Background Workers**: Separate .NET IHostedService instances for reconciliation and webhook retry
- **Database**: Managed PostgreSQL (Render/Supabase) with connection pooling
- **Cache**: Redis (Upstash/Render Redis) for nonces, rate limiting, and session data
- **Secrets**: Render Secrets or AWS KMS for encryption keys and provider credentials
- **Monitoring**: OpenTelemetry → Grafana Cloud/Logflare for traces and logs
- **CI/CD**: GitHub Actions with SAST, dependency scanning, and automated deployment


## Components and Interfaces

### Core Components

#### 1. Payments API (ASP.NET Core)

**Controllers:**
- `PaymentsController`: Create payment, get payment status, refund
- `WebhooksController`: Receive provider webhooks, test merchant webhooks
- `MerchantsController`: Merchant self-service (view transactions, rotate keys)
- `AdminController`: Merchant management (create, configure, disable)
- `SubscriptionsController`: Create and manage recurring subscriptions
- `HealthController`: Health checks for orchestration

**Middleware:**
- `ApiKeyAuthenticationMiddleware`: Validates HMAC signatures for API requests
- `RateLimitingMiddleware`: Redis-based sliding window rate limiter
- `SecurityHeadersMiddleware`: Adds HSTS, CSP, X-Frame-Options, removes Server header
- `ExceptionHandlingMiddleware`: Catches exceptions, logs, returns standardized errors
- `RequestLoggingMiddleware`: Logs all requests with trace IDs for observability

#### 2. Domain Services

**PaymentService:**
```csharp
public interface IPaymentService
{
    Task<PaymentResult> CreatePaymentAsync(CreatePaymentRequest request, string merchantId);
    Task<Payment> GetPaymentAsync(Guid paymentId, string merchantId);
    Task<RefundResult> RefundPaymentAsync(Guid paymentId, RefundRequest request, string merchantId);
    Task UpdatePaymentStatusAsync(Guid paymentId, PaymentStatus status, string providerReference);
}
```

**SubscriptionService:**
```csharp
public interface ISubscriptionService
{
    Task<SubscriptionResult> CreateSubscriptionAsync(CreateSubscriptionRequest request, string merchantId);
    Task<Subscription> GetSubscriptionAsync(Guid subscriptionId, string merchantId);
    Task CancelSubscriptionAsync(Guid subscriptionId, string merchantId);
}
```

**WebhookService:**
```csharp
public interface IWebhookService
{
    Task<bool> ValidateProviderWebhookAsync(string provider, string signature, string payload, long timestamp, string nonce);
    Task ProcessProviderWebhookAsync(ProviderWebhookEvent webhookEvent);
    Task SendMerchantWebhookAsync(Guid merchantId, WebhookEvent webhookEvent);
    Task<WebhookDeliveryResult> TestMerchantWebhookAsync(Guid merchantId);
}
```

**AuthenticationService:**
```csharp
public interface IAuthenticationService
{
    Task<bool> ValidateApiKeySignatureAsync(string apiKey, string signature, string message, long timestamp, string nonce);
    Task<TokenPair> LoginAsync(string email, string password, string mfaCode);
    Task<TokenPair> RefreshTokenAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
}
```

**AuditService:**
```csharp
public interface IAuditService
{
    Task LogAsync(AuditEntry entry);
    Task<bool> VerifyIntegrityAsync(Guid auditId);
    Task ExportToStorageAsync(DateTime startDate, DateTime endDate);
}
```

#### 3. Provider Adapters

**IProviderAdapter Interface:**
```csharp
public interface IProviderAdapter
{
    string ProviderName { get; }
    Task<AuthorizationResult> AuthorizeAsync(AuthorizationRequest request);
    Task<CaptureResult> CaptureAsync(string providerTransactionId, long amountCents);
    Task<RefundResult> RefundAsync(string providerTransactionId, long amountCents);
    Task<bool> ValidateWebhookSignatureAsync(string signature, string payload, long timestamp);
}
```

**PagarMeAdapter:**
- Implements card tokenization flow
- Handles authorization, capture, refund for cards
- Manages subscription creation and cancellation
- Validates Pagar.me webhook signatures

**GerencianetAdapter:**
- Implements PIX dynamic QR code generation
- Handles boleto generation with barcode and PDF
- Polls for payment status updates
- Validates Gerencianet webhook signatures

**ProviderFactory:**
```csharp
public interface IProviderFactory
{
    IProviderAdapter GetProvider(string providerName);
    IProviderAdapter GetProviderForPaymentMethod(PaymentMethod method);
}
```

#### 4. Background Workers

**ReconciliationWorker:**
- Runs daily at 2 AM UTC
- Fetches transaction reports from Pagar.me and Gerencianet
- Compares internal payment records with provider records
- Flags mismatches for manual review
- Generates reconciliation report

**WebhookRetryWorker:**
- Polls for failed webhook deliveries every 5 minutes
- Retries with exponential backoff: 1m, 5m, 15m, 30m, 1h, 2h, 4h, 8h, 12h, 24h
- Marks webhooks as permanently failed after 10 attempts
- Sends alert to merchant dashboard

#### 5. Infrastructure Services

**EncryptionService:**
```csharp
public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
    string Hash(string input);
    bool VerifyHash(string input, string hash);
}
```

**NonceStore (Redis):**
```csharp
public interface INonceStore
{
    Task<bool> IsNonceUniqueAsync(string merchantId, string nonce);
    Task StoreNonceAsync(string merchantId, string nonce, TimeSpan ttl);
}
```

**RateLimiter (Redis):**
```csharp
public interface IRateLimiter
{
    Task<RateLimitResult> CheckRateLimitAsync(string key, int limit, TimeSpan window);
}
```


## Data Models

### Entity Relationship Diagram

```mermaid
erDiagram
    MERCHANTS ||--o{ CUSTOMERS : has
    MERCHANTS ||--o{ PAYMENTS : owns
    CUSTOMERS ||--o{ PAYMENTS : makes
    PAYMENTS ||--o{ TRANSACTIONS : contains
    MERCHANTS ||--o{ MERCHANT_WEBHOOKS : configures
    WEBHOOKS_RECEIVED }o--|| PAYMENTS : references
    MERCHANTS ||--o{ SUBSCRIPTIONS : owns
    SUBSCRIPTIONS ||--o{ PAYMENTS : generates
    MERCHANTS ||--o{ API_KEYS : has
    MERCHANTS ||--o{ AUDIT_LOGS : generates

    MERCHANTS {
        uuid id PK
        text name
        text email
        jsonb provider_config_encrypted
        bool active
        timestamptz created_at
        timestamptz updated_at
    }

    API_KEYS {
        uuid id PK
        uuid merchant_id FK
        text key_id
        text key_hash
        text key_secret_encrypted
        bool active
        timestamptz expires_at
        timestamptz created_at
    }

    CUSTOMERS {
        uuid id PK
        uuid merchant_id FK
        text name
        text email_hash
        text document_hash
        timestamptz created_at
    }

    PAYMENTS {
        uuid id PK
        uuid merchant_id FK
        uuid customer_id FK
        bigint amount_cents
        varchar method
        varchar status
        text provider
        text provider_payment_id
        jsonb provider_payload
        jsonb metadata
        timestamptz created_at
        timestamptz updated_at
    }

    TRANSACTIONS {
        uuid id PK
        uuid payment_id FK
        varchar type
        varchar status
        bigint amount_cents
        text provider_tx_id
        jsonb payload
        timestamptz created_at
    }

    SUBSCRIPTIONS {
        uuid id PK
        uuid merchant_id FK
        uuid customer_id FK
        text provider_subscription_id
        varchar status
        bigint amount_cents
        varchar interval
        timestamptz next_billing_date
        timestamptz created_at
        timestamptz cancelled_at
    }

    MERCHANT_WEBHOOKS {
        uuid id PK
        uuid merchant_id FK
        text endpoint_url
        text secret_encrypted
        bool active
        int retry_count
        timestamptz last_success_at
        timestamptz created_at
    }

    WEBHOOKS_RECEIVED {
        uuid id PK
        text provider
        text event_type
        jsonb payload
        bool processed
        timestamptz received_at
        timestamptz processed_at
    }

    WEBHOOK_DELIVERIES {
        uuid id PK
        uuid merchant_id FK
        uuid payment_id FK
        text event_type
        jsonb payload
        varchar status
        int attempt_count
        text last_error
        timestamptz next_retry_at
        timestamptz created_at
    }

    AUDIT_LOGS {
        uuid id PK
        uuid merchant_id FK
        text actor
        text action
        text resource_type
        uuid resource_id
        jsonb changes
        text signature
        timestamptz created_at
    }

    REFRESH_TOKENS {
        uuid id PK
        uuid user_id FK
        text token_hash
        bool revoked
        timestamptz expires_at
        timestamptz created_at
    }

    USERS {
        uuid id PK
        uuid merchant_id FK
        text email
        text password_hash
        text mfa_secret_encrypted
        bool mfa_enabled
        bool is_admin
        timestamptz created_at
    }
```

### Domain Models

**Payment:**
```csharp
public class Payment
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public Guid? CustomerId { get; set; }
    public long AmountCents { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string Provider { get; set; }
    public string ProviderPaymentId { get; set; }
    public JsonDocument ProviderPayload { get; set; }
    public JsonDocument Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public Merchant Merchant { get; set; }
    public Customer Customer { get; set; }
    public ICollection<Transaction> Transactions { get; set; }
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    Pix,
    Boleto
}

public enum PaymentStatus
{
    Pending,
    Authorized,
    Paid,
    Refunded,
    Failed,
    Expired,
    Cancelled
}
```

**Transaction:**
```csharp
public class Transaction
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public long AmountCents { get; set; }
    public string ProviderTxId { get; set; }
    public JsonDocument Payload { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public Payment Payment { get; set; }
}

public enum TransactionType
{
    Authorization,
    Capture,
    Refund,
    Chargeback
}

public enum TransactionStatus
{
    Pending,
    Success,
    Failed
}
```

**Merchant:**
```csharp
public class Merchant
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string ProviderConfigEncrypted { get; set; } // JSON encrypted
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public ICollection<ApiKey> ApiKeys { get; set; }
    public ICollection<Payment> Payments { get; set; }
    public ICollection<MerchantWebhook> Webhooks { get; set; }
}
```

**AuditLog:**
```csharp
public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? MerchantId { get; set; }
    public string Actor { get; set; }
    public string Action { get; set; }
    public string ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public JsonDocument Changes { get; set; }
    public string Signature { get; set; } // HMAC of entry for integrity
    public DateTime CreatedAt { get; set; }
}
```


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Payment Processing Properties

**Property 1: Card payment creation invariant**
*For any* valid card token and payment request, creating a payment should result in a payment record existing in the database and a call to the Pagar.me provider.
**Validates: Requirements 1.1**

**Property 2: PAN/CVV storage prohibition**
*For any* payment processed through the system, the database should never contain PAN, CVV, or track data patterns (16-digit card numbers, 3-4 digit CVV).
**Validates: Requirements 1.2**

**Property 3: Authorized payment data constraints**
*For any* successful card authorization, the stored payment record should contain only provider token, transaction ID, and masked card information (last 4 digits).
**Validates: Requirements 1.3**

**Property 4: Payment validation ordering**
*For any* payment creation request, schema validation, rate limiting, and antifraud checks should execute before any provider API call is made.
**Validates: Requirements 1.4**

**Property 5: PIX payment response completeness**
*For any* PIX payment request, the response should contain both a QR code (string or base64) and a payment ID (UUID).
**Validates: Requirements 2.1**

**Property 6: PIX initial state invariant**
*For any* newly created PIX payment, the payment record should have status "pending" and contain a Gerencianet transaction reference.
**Validates: Requirements 2.2**

**Property 7: PIX confirmation state transition**
*For any* PIX payment in "pending" status, receiving a confirmation webhook from Gerencianet should transition the status to "paid" and trigger a merchant webhook.
**Validates: Requirements 2.3**

**Property 8: Boleto response completeness**
*For any* boleto payment request, the response should contain barcode, PDF URL, and payment ID.
**Validates: Requirements 3.1**

**Property 9: Boleto initial state invariant**
*For any* newly created boleto payment, the payment record should have status "pending" and an expiration date.
**Validates: Requirements 3.2**

**Property 10: Boleto confirmation state transition**
*For any* boleto payment in "pending" status, receiving a confirmation webhook from Gerencianet should transition the status to "paid" and trigger a merchant webhook.
**Validates: Requirements 3.3**

**Property 11: Subscription creation completeness**
*For any* valid subscription request, the system should create a subscription through Pagar.me and return a subscription ID.
**Validates: Requirements 4.1**

**Property 12: Subscription payment linkage**
*For any* subscription charge event, the system should create a payment record with a foreign key linking to the subscription.
**Validates: Requirements 4.2**

**Property 13: Subscription cancellation state transition**
*For any* active subscription, cancellation should result in a Pagar.me cancellation call and status update to "cancelled".
**Validates: Requirements 4.4**

**Property 14: Payment link uniqueness**
*For any* two payment link generation requests, the generated URLs should be unique (no collisions).
**Validates: Requirements 5.1**

### Webhook and Signature Properties

**Property 15: Outbound webhook signature format**
*For any* payment status change, the outbound webhook should include headers X-Signature, X-Timestamp, X-Nonce, and X-Trace-Id.
**Validates: Requirements 6.1**

**Property 16: Webhook signature round-trip**
*For any* webhook payload and merchant secret, signing the webhook and then verifying the signature should succeed (signature = Base64(HMAC_SHA256(secret, "${timestamp}.${nonce}.${payload}"))).
**Validates: Requirements 6.2**

**Property 17: Inbound webhook validation completeness**
*For any* incoming provider webhook, the system should validate signature, timestamp (within 120s), and nonce uniqueness before processing.
**Validates: Requirements 7.1**

**Property 18: Invalid webhook signature rejection**
*For any* provider webhook with invalid signature, the system should return 401 Unauthorized and create an audit log entry.
**Validates: Requirements 7.2**

**Property 19: Webhook timestamp skew rejection**
*For any* provider webhook with timestamp differing from server time by more than 120 seconds, the system should return 401 Unauthorized.
**Validates: Requirements 7.3**

**Property 20: Webhook nonce replay protection**
*For any* provider webhook nonce that has been used before within 24 hours, the system should return 401 Unauthorized.
**Validates: Requirements 7.4**

**Property 21: Valid webhook processing flow**
*For any* valid provider webhook, the system should store it in webhooks_received, update payment status, and trigger merchant webhook.
**Validates: Requirements 7.5**

### API Authentication Properties

**Property 22: API request header requirements**
*For any* merchant API request, the request should include headers X-Api-Key, X-Timestamp, X-Nonce, and X-Signature.
**Validates: Requirements 8.1**

**Property 23: API signature round-trip**
*For any* API request, signing the request (message = "${timestamp}.${nonce}.${method}.${path}.${bodySha256Hex}") and then verifying with constant-time comparison should succeed.
**Validates: Requirements 8.2**

**Property 24: API timestamp skew rejection**
*For any* API request with timestamp differing from server time by more than 60 seconds, the system should return 401 Unauthorized.
**Validates: Requirements 8.3**

**Property 25: API nonce replay protection**
*For any* API request nonce that has been used before within 24 hours, the system should return 401 Unauthorized.
**Validates: Requirements 8.4**

**Property 26: Invalid API signature rejection**
*For any* API request with invalid signature, the system should return 401 Unauthorized and create an audit log entry.
**Validates: Requirements 8.5**

### JWT and Token Properties

**Property 27: Login token issuance format**
*For any* successful login, the system should issue an RS256-signed access token with 15-minute expiry and an opaque refresh token with 30-day expiry.
**Validates: Requirements 9.1**

**Property 28: Refresh token rotation**
*For any* valid refresh token use, the system should issue new access and refresh tokens, mark the old refresh token as revoked, and store the new refresh token hashed.
**Validates: Requirements 9.2**

**Property 29: Logout token revocation**
*For any* logout request, the system should revoke all refresh tokens for that session.
**Validates: Requirements 9.3**

**Property 30: Expired access token rejection**
*For any* request with expired access token, the system should return 401 Unauthorized.
**Validates: Requirements 9.4**

**Property 31: Revoked refresh token rejection**
*For any* revoked refresh token, the system should return 401 Unauthorized when used.
**Validates: Requirements 9.5**

### Rate Limiting Properties

**Property 32: Merchant rate limit enforcement**
*For any* merchant making more than 200 requests per minute, the 201st request should be rejected with 429 Too Many Requests.
**Validates: Requirements 10.1**

**Property 33: Payment endpoint IP rate limit**
*For any* IP address making more than 20 requests per minute to payment creation endpoint, the 21st request should be rejected with 429 Too Many Requests.
**Validates: Requirements 10.2**

**Property 34: Rate limit response headers**
*For any* rate-limited request, the response should include a Retry-After header with seconds until limit resets.
**Validates: Requirements 10.3**

**Property 35: Rate limit window reset**
*For any* rate limit counter, when the time window expires, the counter should reset to zero for the next window.
**Validates: Requirements 10.4**

### Encryption Properties

**Property 36: Provider config encryption round-trip**
*For any* provider configuration data, encrypting with AES-256-GCM and then decrypting should return the original data.
**Validates: Requirements 11.1, 11.3**

**Property 37: Webhook secret encryption round-trip**
*For any* merchant webhook secret, encrypting with AES-256-GCM and then decrypting should return the original secret.
**Validates: Requirements 11.2, 11.3**

### Audit and Compliance Properties

**Property 38: Payment operation audit logging**
*For any* payment operation (create, refund, status change), an audit log entry should be created with timestamp, actor, action, resource, and outcome.
**Validates: Requirements 12.1**

**Property 39: Audit log signature integrity**
*For any* audit log entry, the entry should have an HMAC signature, and verifying the signature should succeed.
**Validates: Requirements 12.2, 12.3**

### Reconciliation Properties

**Property 40: Reconciliation mismatch detection**
*For any* payment with different status between FluxPay and provider records, the reconciliation process should flag it as a mismatch.
**Validates: Requirements 13.2**

**Property 41: Reconciliation mismatch alerting**
*For any* detected reconciliation mismatch, the system should create an alert and flag the payment for manual review.
**Validates: Requirements 13.3**

### Antifraud Properties

**Property 42: IP velocity limit enforcement**
*For any* IP address exceeding configured velocity limits, payment requests should be rejected with 429 Too Many Requests.
**Validates: Requirements 14.1**

**Property 43: Blacklist enforcement**
*For any* payment request containing a CPF or BIN on the blacklist, the request should be rejected with 403 Forbidden.
**Validates: Requirements 14.2**

**Property 44: Adaptive IP blocking**
*For any* IP address with multiple failed payment attempts (>3) within 10 minutes, the IP should be blocked for 1 hour.
**Validates: Requirements 14.3**

**Property 45: Antifraud audit logging**
*For any* triggered antifraud rule, an audit log entry should be created with rule details.
**Validates: Requirements 14.4**

### Merchant Management Properties

**Property 46: API key rotation completeness**
*For any* API key rotation request, the system should generate a new key pair, return the secret once, and mark the old key for deprecation.
**Validates: Requirements 15.4**

**Property 47: Merchant creation completeness**
*For any* merchant creation request, the system should generate a unique merchant ID, API key pair, and store the API key hash (not plaintext).
**Validates: Requirements 16.1**

**Property 48: Provider credential encryption**
*For any* provider credential configuration, the credentials should be encrypted before storage.
**Validates: Requirements 16.2**

**Property 49: Disabled merchant request rejection**
*For any* disabled merchant, all API requests should be rejected with 403 Forbidden.
**Validates: Requirements 16.4**

### Admin Security Properties

**Property 50: Admin IP allowlist enforcement**
*For any* request to admin endpoints from IP not on allowlist, the request should be rejected with 403 Forbidden.
**Validates: Requirements 17.1**

**Property 51: Admin MFA requirement**
*For any* admin login attempt, MFA verification should be required before token issuance.
**Validates: Requirements 17.2**

**Property 52: Admin MFA failure handling**
*For any* failed MFA verification, the login should be rejected and an audit log entry created.
**Validates: Requirements 17.3**

**Property 53: Admin token expiry**
*For any* admin session, access tokens should have 5-minute expiry (shorter than regular users).
**Validates: Requirements 17.4**

### Sandbox and Security Properties

**Property 54: Sandbox routing**
*For any* request in sandbox mode, the system should route to provider sandbox environments and mark transactions with is_test=true.
**Validates: Requirements 18.2**

**Property 55: Security headers presence**
*For any* HTTP response, the response should include Strict-Transport-Security, X-Content-Type-Options, X-Frame-Options, and Content-Security-Policy headers.
**Validates: Requirements 20.2**

**Property 56: Server header removal**
*For any* HTTP response, the response should not include Server or X-Powered-By headers.
**Validates: Requirements 20.3**

**Property 57: Strict JSON validation**
*For any* request with unexpected JSON fields, the system should reject with 400 Bad Request.
**Validates: Requirements 20.4**

### Refund Properties

**Property 58: Refund provider routing**
*For any* refund request, the refund should be initiated through the same provider as the original payment.
**Validates: Requirements 21.1**

**Property 59: Refund transaction linkage**
*For any* processed refund, a refund transaction record should be created with a foreign key to the original payment.
**Validates: Requirements 21.2**

**Property 60: Refund completion flow**
*For any* completed refund, the payment status should be updated to "refunded" and a merchant webhook should be triggered.
**Validates: Requirements 21.3**

**Property 61: Partial refund arithmetic**
*For any* partial refund of amount X on payment of amount Y, the remaining balance should be Y - X.
**Validates: Requirements 21.4**


## Error Handling

### Error Response Format

All API errors follow a consistent JSON structure:

```json
{
  "error": {
    "code": "PAYMENT_FAILED",
    "message": "Payment authorization failed",
    "details": {
      "provider_code": "insufficient_funds",
      "provider_message": "Insufficient funds"
    },
    "trace_id": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

### Error Categories

**Authentication Errors (401):**
- `INVALID_API_KEY`: API key not found or invalid
- `INVALID_SIGNATURE`: HMAC signature verification failed
- `TIMESTAMP_SKEW`: Request timestamp outside acceptable window
- `NONCE_REUSED`: Nonce has been used before (replay attack)
- `TOKEN_EXPIRED`: JWT access token has expired
- `TOKEN_REVOKED`: Refresh token has been revoked

**Authorization Errors (403):**
- `MERCHANT_DISABLED`: Merchant account is disabled
- `IP_NOT_ALLOWED`: IP address not on allowlist (admin endpoints)
- `BLACKLISTED`: CPF/BIN is on blacklist
- `INSUFFICIENT_PERMISSIONS`: User lacks required permissions

**Rate Limiting Errors (429):**
- `RATE_LIMIT_EXCEEDED`: Too many requests in time window
- Includes `Retry-After` header with seconds until reset

**Validation Errors (400):**
- `INVALID_REQUEST`: Schema validation failed
- `UNEXPECTED_FIELDS`: Request contains fields not in schema
- `INVALID_AMOUNT`: Amount is negative or exceeds limits
- `INVALID_PAYMENT_METHOD`: Payment method not supported

**Business Logic Errors (422):**
- `PAYMENT_ALREADY_REFUNDED`: Cannot refund already refunded payment
- `REFUND_AMOUNT_EXCEEDS_PAYMENT`: Refund amount greater than payment
- `SUBSCRIPTION_ALREADY_CANCELLED`: Cannot cancel already cancelled subscription

**Provider Errors (502/503):**
- `PROVIDER_UNAVAILABLE`: Payment provider is down
- `PROVIDER_TIMEOUT`: Provider request timed out
- Automatic retry with exponential backoff for transient errors

### Error Handling Strategy

**Transient Errors:**
- Database connection failures: Retry up to 3 times with exponential backoff
- Redis connection failures: Log error, continue without cache (degraded mode)
- Provider timeouts: Retry once, then return error to merchant

**Permanent Errors:**
- Validation failures: Return immediately with 400
- Authentication failures: Return immediately with 401
- Authorization failures: Return immediately with 403

**Circuit Breaker:**
- If provider error rate exceeds 50% over 1 minute, open circuit for 30 seconds
- During circuit open, return 503 Service Unavailable immediately
- After 30 seconds, allow one test request (half-open state)
- If test succeeds, close circuit; if fails, reopen for 60 seconds

### Logging and Monitoring

**Error Logging:**
- All errors logged with structured logging (JSON format)
- Include trace_id, merchant_id, error_code, stack_trace (for 5xx)
- Sensitive data (PAN, secrets) masked in logs

**Alerting Thresholds:**
- Error rate > 5% over 5 minutes: Page on-call engineer
- Provider error rate > 20% over 5 minutes: Alert operations team
- Authentication failure rate > 10% over 1 minute: Security alert
- Database connection failures: Immediate page


## Testing Strategy

### Overview

FluxPay employs a comprehensive testing strategy combining unit tests, property-based tests, integration tests, and contract tests to ensure correctness, security, and reliability. The dual approach of unit and property-based testing provides both concrete bug detection and general correctness verification.

### Property-Based Testing

**Framework:** FsCheck for .NET (F# property testing library compatible with xUnit)

**Configuration:**
- Minimum 100 iterations per property test
- Custom generators for domain models (Payment, Transaction, Merchant)
- Shrinking enabled to find minimal failing cases

**Property Test Organization:**
Each property-based test must:
1. Be tagged with a comment referencing the design document property
2. Use the format: `// Feature: fluxpay-payment-gateway, Property {number}: {property_text}`
3. Implement exactly one correctness property from the design document
4. Run at least 100 iterations with randomized inputs

**Example Property Test Structure:**
```csharp
[Property]
// Feature: fluxpay-payment-gateway, Property 2: PAN/CVV storage prohibition
public Property Payment_Should_Never_Store_PAN_Or_CVV(PaymentRequest request)
{
    return Prop.ForAll(
        Arb.From<PaymentRequest>(),
        req =>
        {
            var payment = _paymentService.CreatePaymentAsync(req).Result;
            var storedPayment = _dbContext.Payments.Find(payment.Id);
            
            return !ContainsPAN(storedPayment) && !ContainsCVV(storedPayment);
        }
    );
}
```

**Key Property Test Areas:**
- **Signature Round-trips:** Sign then verify for webhooks and API requests (Properties 16, 23)
- **Encryption Round-trips:** Encrypt then decrypt for sensitive data (Properties 36, 37)
- **State Transitions:** Payment status changes maintain invariants (Properties 7, 10, 13)
- **Security Invariants:** No PAN/CVV storage, nonce uniqueness, rate limiting (Properties 2, 20, 25, 32, 33)
- **Data Integrity:** Audit log signatures, refund arithmetic (Properties 39, 61)

### Unit Testing

**Framework:** xUnit with FluentAssertions and Moq

**Coverage Target:** ≥70% for critical services (PaymentService, WebhookService, AuthenticationService, ProviderAdapters)

**Unit Test Focus:**
- Specific examples demonstrating correct behavior
- Edge cases (empty inputs, boundary values, null handling)
- Error conditions and exception handling
- Integration points between components
- Mock external dependencies (providers, database, Redis)

**Example Unit Test:**
```csharp
[Fact]
public async Task CreatePayment_WithInvalidAmount_ShouldThrowValidationException()
{
    // Arrange
    var request = new CreatePaymentRequest { AmountCents = -100 };
    
    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(
        () => _paymentService.CreatePaymentAsync(request, "merchant-123")
    );
}
```

**Unit Test Organization:**
- One test class per service/controller
- Descriptive test names: `MethodName_Scenario_ExpectedBehavior`
- Arrange-Act-Assert pattern
- Test fixtures for common setup

### Integration Testing

**Framework:** xUnit with Testcontainers for PostgreSQL and Redis

**Scope:**
- Full request-response cycles through API controllers
- Database migrations and queries
- Redis operations (nonces, rate limiting)
- Background worker execution
- Provider adapter integration (with mocked HTTP responses)

**Testcontainers Setup:**
```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly RedisContainer _redisContainer;
    
    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        await _redisContainer.StartAsync();
        await RunMigrations();
    }
}
```

**Integration Test Scenarios:**
1. **Card Payment Flow:** Create payment with token → Authorize → Webhook received → Status updated → Merchant webhook sent
2. **PIX Payment Flow:** Create PIX → QR generated → Confirmation webhook → Status updated
3. **Webhook Retry:** Failed webhook → Retry worker runs → Exponential backoff → Success or permanent failure
4. **Reconciliation:** Create payments → Mock provider reports → Run reconciliation → Detect mismatches
5. **Rate Limiting:** Make 201 requests → Verify 201st is rejected with 429
6. **Nonce Replay:** Send request → Replay same nonce → Verify rejection

### Contract Testing

**Framework:** Pact for consumer-driven contracts or Postman/Newman for provider contracts

**Provider Contracts:**
- **Pagar.me:** Card authorization, capture, refund, subscription creation, webhook format
- **Gerencianet:** PIX creation, boleto creation, status polling, webhook format

**Contract Test Approach:**
1. Define expected request/response schemas for each provider endpoint
2. Record provider interactions during integration tests
3. Verify FluxPay sends requests matching provider specifications
4. Verify FluxPay correctly handles provider responses
5. Run contract tests against provider sandbox environments

**Merchant Webhook Contract:**
- Define webhook payload schema
- Provide signature validation examples in multiple languages (C#, Python, Node.js, PHP)
- Test that merchants can verify webhook signatures

### End-to-End Testing

**Framework:** Playwright or Cypress for Next.js dashboard

**E2E Scenarios:**
1. Merchant login → View transactions → Filter by status
2. Merchant webhook test → Trigger test webhook → View response
3. Merchant API key rotation → Generate new key → Old key deprecated
4. Admin create merchant → Configure providers → Merchant can process payment

**E2E Test Environment:**
- Separate test database and Redis instance
- Provider sandbox mode enabled
- Test data seeded before each run
- Cleanup after each test

### CI/CD Pipeline Testing

**GitHub Actions Workflow:**

```yaml
name: CI

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '7.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore
      
      - name: Run unit tests
        run: dotnet test --no-build --filter Category=Unit --logger "trx;LogFileName=unit-tests.trx"
      
      - name: Run property tests
        run: dotnet test --no-build --filter Category=Property --logger "trx;LogFileName=property-tests.trx"
      
      - name: Run integration tests
        run: dotnet test --no-build --filter Category=Integration --logger "trx;LogFileName=integration-tests.trx"
      
      - name: Code coverage
        run: dotnet test --no-build --collect:"XPlat Code Coverage"
      
      - name: Static analysis
        run: dotnet format --verify-no-changes
      
      - name: SAST scan
        uses: github/codeql-action/analyze@v2
      
      - name: Dependency scan
        run: dotnet list package --vulnerable --include-transitive
      
      - name: Build Docker image
        if: github.ref == 'refs/heads/main'
        run: docker build -t fluxpay-api:${{ github.sha }} .
      
      - name: Run contract tests
        if: github.ref == 'refs/heads/main'
        run: newman run postman-collection.json --environment staging
```

### Test Data Management

**Generators for Property Tests:**
- `PaymentRequestGenerator`: Valid payment requests with random amounts, methods, customer data
- `MerchantGenerator`: Merchants with valid API keys and provider configs
- `WebhookEventGenerator`: Valid webhook payloads with signatures
- `CardTokenGenerator`: Valid Pagar.me card tokens (test mode)

**Test Data Constraints:**
- Amounts: 100 to 1,000,000 cents (R$1 to R$10,000)
- Timestamps: Within ±60 seconds of current time for valid requests
- Nonces: UUIDs, guaranteed unique per test run
- CPF/CNPJ: Valid check digits using Brazilian validation rules

**Fixtures:**
- Seed merchants with known API keys for integration tests
- Pre-create test customers and payments for dashboard E2E tests
- Mock provider responses for common scenarios (success, insufficient funds, timeout)

### Performance Testing

**Load Testing (Optional for MVP):**
- Apache JMeter or k6 for load testing
- Target: 100 requests/second sustained for 5 minutes
- Monitor response times, error rates, database connections

**Stress Testing:**
- Gradually increase load until system degrades
- Identify bottlenecks (database, Redis, provider API)
- Verify graceful degradation and error handling


## Security Architecture

### Defense in Depth

FluxPay implements multiple layers of security controls:

**Layer 1: Network**
- TLS 1.2+ for all connections
- IP allowlist for admin endpoints
- WAF rules blocking OWASP Top 10 patterns
- DDoS protection via Cloudflare or Render

**Layer 2: Authentication**
- HMAC-SHA256 signatures for API requests
- RS256 JWT for user sessions
- MFA for admin users
- Nonce-based replay protection

**Layer 3: Authorization**
- Role-based access control (RBAC)
- Merchant isolation (row-level security)
- API key scoping (read-only vs full access)
- Admin IP allowlist enforcement

**Layer 4: Data Protection**
- AES-256-GCM encryption at rest for secrets
- No PAN/CVV storage (tokenization only)
- Hashed API keys and refresh tokens
- Masked sensitive data in logs

**Layer 5: Application**
- Input validation and sanitization
- Rate limiting per merchant and endpoint
- Antifraud rules (velocity, blacklist)
- Circuit breakers for provider failures

**Layer 6: Monitoring**
- Audit logs for all operations
- Security alerts for anomalies
- SAST and dependency scanning in CI
- Regular security reviews

### Cryptographic Standards

**Hashing:**
- Passwords: Argon2id (memory-hard, resistant to GPU attacks)
- API Keys: SHA-256
- Refresh Tokens: SHA-256
- Audit Log Signatures: HMAC-SHA256

**Encryption:**
- Symmetric: AES-256-GCM (authenticated encryption)
- Asymmetric: RSA-2048 for JWT signing (RS256)
- Key Derivation: PBKDF2 with 100,000 iterations

**Signatures:**
- Webhooks: HMAC-SHA256
- API Requests: HMAC-SHA256
- Audit Logs: HMAC-SHA256

**Random Generation:**
- Nonces: Cryptographically secure random (System.Security.Cryptography.RandomNumberGenerator)
- API Keys: 32 bytes of secure random, base64-encoded
- Encryption IVs: 12 bytes for GCM mode

### Key Management

**Key Hierarchy:**
```
Master Key (KMS/Render Secrets)
  ├─ Data Encryption Key (DEK) - rotated annually
  │   ├─ Provider Config Encryption
  │   ├─ Webhook Secret Encryption
  │   └─ MFA Secret Encryption
  ├─ JWT Signing Key (RSA private key) - rotated annually
  └─ Audit Log HMAC Key - rotated annually
```

**Key Storage:**
- Master keys: AWS KMS or Render Secrets (never in code/config)
- DEKs: Encrypted with master key, stored in database
- JWT keys: Encrypted with master key, stored in database
- API keys: Only hashes stored, plaintext shown once at creation

**Key Rotation Procedure:**
1. Generate new key version
2. Encrypt new data with new key
3. Re-encrypt existing data in background job
4. Mark old key for deprecation after grace period (30 days)
5. Delete old key after all data re-encrypted

### Secrets Management

**Environment Variables (Render Secrets):**
```
DB_CONNECTION_STRING
REDIS_URL
PAGARME_API_KEY
GERENCIANET_CLIENT_ID
GERENCIANET_CLIENT_SECRET
JWT_PRIVATE_KEY
JWT_PUBLIC_KEY
MASTER_ENCRYPTION_KEY
AUDIT_HMAC_KEY
```

**Secret Rotation:**
- Provider API keys: Rotate quarterly or on suspected compromise
- JWT signing keys: Rotate annually
- Encryption keys: Rotate annually
- Database passwords: Rotate semi-annually

**Secret Access Control:**
- Secrets accessible only to API and worker processes
- No secrets in logs, error messages, or client responses
- Secrets encrypted in memory when possible
- Secrets cleared from memory after use

### Compliance Considerations

**PCI-DSS Scope Reduction:**
- No card data storage (tokenization via Pagar.me)
- No PAN transmission (tokens only)
- Reduced scope to SAQ A (merchant-hosted checkout)

**LGPD (Brazilian GDPR) Compliance:**
- Customer data minimization (only necessary fields)
- Email and CPF hashed for privacy
- Data retention policies (7 years for financial records)
- Right to erasure (anonymize customer data on request)
- Audit trail for data access

**Security Standards:**
- OWASP Top 10 mitigation
- CWE Top 25 awareness
- NIST Cybersecurity Framework alignment

### Incident Response

**Security Incident Playbook:**

1. **Detection:** Alert triggered (auth failures, anomalies, vulnerability scan)
2. **Triage:** Assess severity (critical, high, medium, low)
3. **Containment:** Block malicious IPs, revoke compromised keys, disable affected merchants
4. **Investigation:** Review audit logs, identify root cause, assess impact
5. **Remediation:** Patch vulnerabilities, rotate secrets, notify affected parties
6. **Recovery:** Restore normal operations, monitor for recurrence
7. **Post-Mortem:** Document incident, update runbooks, improve detection

**Breach Notification:**
- Notify affected merchants within 24 hours
- Notify ANPD (Brazilian data protection authority) within 72 hours if customer data exposed
- Provide remediation guidance and support


## API Specifications

### Authentication Flows

#### API Key Authentication (Machine-to-Machine)

**Request Headers:**
```
X-Api-Key: merchant-abc123
X-Timestamp: 1701388800
X-Nonce: 550e8400-e29b-41d4-a716-446655440000
X-Signature: base64(hmac_sha256(secret, message))
```

**Signature Computation:**
```
message = "${timestamp}.${nonce}.${method}.${path}.${bodySha256Hex}"
signature = Base64(HMAC_SHA256(api_key_secret, message))
```

**Validation Steps:**
1. Lookup API key by X-Api-Key header
2. Verify timestamp skew ≤ 60 seconds
3. Check nonce uniqueness in Redis (TTL 24h)
4. Recompute signature and compare with constant-time
5. If valid, authorize request; else return 401

#### JWT Authentication (Dashboard Users)

**Login Request:**
```http
POST /v1/auth/login
Content-Type: application/json

{
  "email": "merchant@example.com",
  "password": "secure_password",
  "mfa_code": "123456"
}
```

**Login Response:**
```json
{
  "access_token": "eyJhbGc...",
  "refresh_token": "opaque_token_abc123",
  "expires_in": 900,
  "token_type": "Bearer"
}
```

**Token Refresh:**
```http
POST /v1/auth/refresh
Content-Type: application/json

{
  "refresh_token": "opaque_token_abc123"
}
```

**Token Refresh Response:**
```json
{
  "access_token": "eyJhbGc...",
  "refresh_token": "opaque_token_xyz789",
  "expires_in": 900,
  "token_type": "Bearer"
}
```

### Payment Endpoints

#### Create Card Payment

```http
POST /v1/payments
X-Api-Key: merchant-abc123
X-Timestamp: 1701388800
X-Nonce: 550e8400-e29b-41d4-a716-446655440000
X-Signature: base64(hmac_sha256(...))
Content-Type: application/json

{
  "amount_cents": 10000,
  "method": "credit_card",
  "card_token": "card_tok_abc123",
  "customer": {
    "name": "João Silva",
    "email": "joao@example.com",
    "document": "12345678900"
  },
  "metadata": {
    "order_id": "ORD-12345"
  }
}
```

**Response (202 Accepted):**
```json
{
  "payment_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "pending",
  "amount_cents": 10000,
  "method": "credit_card",
  "created_at": "2024-12-01T10:00:00Z"
}
```

#### Create PIX Payment

```http
POST /v1/payments
Content-Type: application/json

{
  "amount_cents": 5000,
  "method": "pix",
  "customer": {
    "name": "Maria Santos",
    "email": "maria@example.com",
    "document": "98765432100"
  }
}
```

**Response (201 Created):**
```json
{
  "payment_id": "660f9500-f39c-52e5-b827-557766551111",
  "status": "pending",
  "amount_cents": 5000,
  "method": "pix",
  "pix": {
    "qr_code": "00020126580014br.gov.bcb.pix...",
    "qr_code_url": "https://api.gerencianet.com.br/qr/abc123.png",
    "expires_at": "2024-12-01T11:00:00Z"
  },
  "created_at": "2024-12-01T10:00:00Z"
}
```

#### Create Boleto Payment

```http
POST /v1/payments
Content-Type: application/json

{
  "amount_cents": 15000,
  "method": "boleto",
  "customer": {
    "name": "Carlos Oliveira",
    "email": "carlos@example.com",
    "document": "11122233344"
  }
}
```

**Response (201 Created):**
```json
{
  "payment_id": "770fa611-g49d-63f6-c938-668877662222",
  "status": "pending",
  "amount_cents": 15000,
  "method": "boleto",
  "boleto": {
    "barcode": "34191.79001 01043.510047 91020.150008 1 82880000015000",
    "digitable_line": "34191790010104351004791020150008182880000015000",
    "pdf_url": "https://api.gerencianet.com.br/boleto/abc123.pdf",
    "expires_at": "2024-12-08T23:59:59Z"
  },
  "created_at": "2024-12-01T10:00:00Z"
}
```

#### Get Payment Status

```http
GET /v1/payments/550e8400-e29b-41d4-a716-446655440000
X-Api-Key: merchant-abc123
X-Timestamp: 1701388800
X-Nonce: 660e8400-e29b-41d4-a716-446655440001
X-Signature: base64(hmac_sha256(...))
```

**Response (200 OK):**
```json
{
  "payment_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "paid",
  "amount_cents": 10000,
  "method": "credit_card",
  "customer": {
    "name": "João Silva",
    "email": "joao@example.com"
  },
  "transactions": [
    {
      "id": "880fb722-h59e-74g7-d049-779988773333",
      "type": "authorization",
      "status": "success",
      "amount_cents": 10000,
      "created_at": "2024-12-01T10:00:05Z"
    },
    {
      "id": "990fc833-i69f-85h8-e150-880099884444",
      "type": "capture",
      "status": "success",
      "amount_cents": 10000,
      "created_at": "2024-12-01T10:00:10Z"
    }
  ],
  "created_at": "2024-12-01T10:00:00Z",
  "updated_at": "2024-12-01T10:00:10Z"
}
```

#### Refund Payment

```http
POST /v1/payments/550e8400-e29b-41d4-a716-446655440000/refund
Content-Type: application/json

{
  "amount_cents": 5000,
  "reason": "Customer requested partial refund"
}
```

**Response (200 OK):**
```json
{
  "refund_id": "aa0fd944-j79g-96i9-f261-991100995555",
  "payment_id": "550e8400-e29b-41d4-a716-446655440000",
  "amount_cents": 5000,
  "status": "processing",
  "created_at": "2024-12-01T11:00:00Z"
}
```

### Subscription Endpoints

#### Create Subscription

```http
POST /v1/subscriptions
Content-Type: application/json

{
  "amount_cents": 9900,
  "interval": "monthly",
  "card_token": "card_tok_abc123",
  "customer": {
    "name": "Ana Costa",
    "email": "ana@example.com",
    "document": "55566677788"
  },
  "metadata": {
    "plan": "premium"
  }
}
```

**Response (201 Created):**
```json
{
  "subscription_id": "bb0fe055-k89h-07j0-g372-002211006666",
  "status": "active",
  "amount_cents": 9900,
  "interval": "monthly",
  "next_billing_date": "2025-01-01T00:00:00Z",
  "created_at": "2024-12-01T10:00:00Z"
}
```

#### Cancel Subscription

```http
POST /v1/subscriptions/bb0fe055-k89h-07j0-g372-002211006666/cancel
```

**Response (200 OK):**
```json
{
  "subscription_id": "bb0fe055-k89h-07j0-g372-002211006666",
  "status": "cancelled",
  "cancelled_at": "2024-12-01T12:00:00Z"
}
```

### Webhook Endpoints

#### Receive Provider Webhook

```http
POST /v1/webhooks/provider
X-Provider: pagarme
X-Signature: base64(hmac_sha256(...))
X-Timestamp: 1701388800
X-Nonce: cc0ff166-l99i-18k1-h483-113322117777
Content-Type: application/json

{
  "event": "transaction.paid",
  "transaction": {
    "id": "tran_abc123",
    "status": "paid",
    "amount": 10000
  }
}
```

**Response (200 OK):**
```json
{
  "received": true
}
```

#### Test Merchant Webhook

```http
POST /v1/webhooks/merchant/test
Authorization: Bearer eyJhbGc...
Content-Type: application/json

{
  "endpoint_url": "https://merchant.example.com/webhooks"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "status_code": 200,
  "response_time_ms": 145,
  "response_body": "{\"received\":true}"
}
```

### Merchant Webhook Format (Outbound)

**Headers:**
```
X-Signature: base64(hmac_sha256(merchant_secret, "${timestamp}.${nonce}.${payload}"))
X-Timestamp: 1701388800
X-Nonce: dd0fg277-m00j-29l2-i594-224433228888
X-Trace-Id: ee0fh388-n11k-30m3-j605-335544339999
Content-Type: application/json
```

**Payload:**
```json
{
  "event": "payment.paid",
  "payment": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "status": "paid",
    "amount_cents": 10000,
    "method": "credit_card",
    "created_at": "2024-12-01T10:00:00Z",
    "paid_at": "2024-12-01T10:00:10Z"
  },
  "metadata": {
    "order_id": "ORD-12345"
  }
}
```

### Admin Endpoints

#### Create Merchant

```http
POST /v1/admin/merchants
Authorization: Bearer eyJhbGc... (admin token)
Content-Type: application/json

{
  "name": "Acme Store",
  "email": "admin@acmestore.com",
  "provider_config": {
    "pagarme": {
      "api_key": "ak_test_abc123"
    },
    "gerencianet": {
      "client_id": "Client_Id_abc123",
      "client_secret": "Client_Secret_xyz789"
    }
  }
}
```

**Response (201 Created):**
```json
{
  "merchant_id": "ff0gi499-o22l-41n4-k716-446655440000",
  "name": "Acme Store",
  "email": "admin@acmestore.com",
  "api_key": {
    "key_id": "merchant-ff0gi499",
    "key_secret": "sk_live_abc123xyz789..." // Shown only once
  },
  "created_at": "2024-12-01T10:00:00Z"
}
```

#### Disable Merchant

```http
POST /v1/admin/merchants/ff0gi499-o22l-41n4-k716-446655440000/disable
Authorization: Bearer eyJhbGc... (admin token)
```

**Response (200 OK):**
```json
{
  "merchant_id": "ff0gi499-o22l-41n4-k716-446655440000",
  "active": false,
  "disabled_at": "2024-12-01T15:00:00Z"
}
```

### Health Check

```http
GET /health
```

**Response (200 OK):**
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "checks": {
    "database": "healthy",
    "redis": "healthy",
    "pagarme": "healthy",
    "gerencianet": "healthy"
  },
  "timestamp": "2024-12-01T10:00:00Z"
}
```


## Deployment and Operations

### Docker Configuration

**Dockerfile (Multi-stage Build):**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["Kiro.FluxPay.Api/Kiro.FluxPay.Api.csproj", "Kiro.FluxPay.Api/"]
COPY ["Kiro.FluxPay.Core/Kiro.FluxPay.Core.csproj", "Kiro.FluxPay.Core/"]
COPY ["Kiro.FluxPay.Infrastructure/Kiro.FluxPay.Infrastructure.csproj", "Kiro.FluxPay.Infrastructure/"]
COPY ["Kiro.FluxPay.Workers/Kiro.FluxPay.Workers.csproj", "Kiro.FluxPay.Workers/"]
RUN dotnet restore "Kiro.FluxPay.Api/Kiro.FluxPay.Api.csproj"

# Copy source and build
COPY . .
WORKDIR /src/Kiro.FluxPay.Api
RUN dotnet publish -c Release -o /app --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app .

# Security: Run as non-root user
RUN useradd -m -u 1000 fluxpay && chown -R fluxpay:fluxpay /app
USER fluxpay

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "Kiro.FluxPay.Api.dll"]
```

**docker-compose.yml (Development):**
```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - DB_HOST=postgres
      - DB_PORT=5432
      - DB_NAME=fluxpay
      - DB_USER=fluxpay
      - DB_PASSWORD=fluxpay_dev
      - REDIS_URL=redis://redis:6379
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_started
    volumes:
      - ./logs:/app/logs

  worker:
    build: .
    command: ["dotnet", "Kiro.FluxPay.Workers.dll"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - DB_HOST=postgres
      - DB_PORT=5432
      - DB_NAME=fluxpay
      - DB_USER=fluxpay
      - DB_PASSWORD=fluxpay_dev
      - REDIS_URL=redis://redis:6379
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_started

  postgres:
    image: postgres:15-alpine
    environment:
      POSTGRES_DB: fluxpay
      POSTGRES_USER: fluxpay
      POSTGRES_PASSWORD: fluxpay_dev
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U fluxpay"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    command: redis-server --appendonly yes

volumes:
  postgres_data:
  redis_data:
```

### Render Deployment

**Web Service Configuration:**
- **Type:** Docker
- **Region:** São Paulo (for Brazilian merchants)
- **Instance Type:** Standard (2 CPU, 4GB RAM)
- **Auto-scaling:** 2-10 instances based on CPU/memory
- **Health Check:** GET /health every 30s

**Background Worker Configuration:**
- **Type:** Docker (separate service)
- **Instance Type:** Standard (1 CPU, 2GB RAM)
- **Scaling:** Fixed 1 instance (cron-based workers)

**Database:**
- **Type:** Managed PostgreSQL 15
- **Plan:** Standard (25GB storage, 1GB RAM)
- **Backups:** Daily automated backups, 7-day retention
- **Connection Pooling:** PgBouncer with 100 max connections

**Redis:**
- **Type:** Managed Redis (Upstash or Render Redis)
- **Plan:** Standard (1GB memory)
- **Persistence:** AOF enabled
- **Eviction:** allkeys-lru for cache, noeviction for nonces

**Environment Variables (Render Secrets):**
```
# Database
DB_CONNECTION_STRING=postgresql://user:pass@host:5432/fluxpay

# Redis
REDIS_URL=redis://default:pass@host:6379

# Providers
PAGARME_API_KEY=ak_live_...
GERENCIANET_CLIENT_ID=Client_Id_...
GERENCIANET_CLIENT_SECRET=Client_Secret_...

# Cryptography
JWT_PRIVATE_KEY=-----BEGIN RSA PRIVATE KEY-----...
JWT_PUBLIC_KEY=-----BEGIN PUBLIC KEY-----...
MASTER_ENCRYPTION_KEY=base64_encoded_32_bytes
AUDIT_HMAC_KEY=base64_encoded_32_bytes

# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
ALLOWED_ORIGINS=https://dashboard.fluxpay.com

# Admin Security
ADMIN_IP_ALLOWLIST=203.0.113.0/24,198.51.100.50

# Monitoring
LOGFLARE_API_KEY=...
LOGFLARE_SOURCE_ID=...
```

### Database Migrations

**Migration Strategy:**
- Migrations run automatically on application startup (development)
- Migrations run manually before deployment (production)
- Zero-downtime migrations: additive changes first, then remove old columns

**Migration Commands:**
```bash
# Create migration
dotnet ef migrations add InitialCreate --project Kiro.FluxPay.Infrastructure

# Apply migrations
dotnet ef database update --project Kiro.FluxPay.Infrastructure

# Generate SQL script for production
dotnet ef migrations script --project Kiro.FluxPay.Infrastructure --output migration.sql
```

**Production Migration Process:**
1. Review migration SQL script
2. Test on staging database
3. Schedule maintenance window (if breaking changes)
4. Apply migration with transaction
5. Verify data integrity
6. Deploy new application version

### Monitoring and Observability

**OpenTelemetry Configuration:**
```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql()
        .AddRedisInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("https://otlp.grafana.com");
        }))
    .WithMetrics(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());
```

**Key Metrics:**
- **Request Rate:** Requests per second by endpoint
- **Error Rate:** 4xx and 5xx responses per second
- **Latency:** P50, P95, P99 response times
- **Payment Success Rate:** Successful payments / total payments
- **Provider Availability:** Uptime percentage for Pagar.me and Gerencianet
- **Webhook Delivery Rate:** Successful deliveries / total webhooks
- **Database Connections:** Active connections, connection pool usage
- **Redis Operations:** Commands per second, hit rate

**Dashboards:**
1. **Overview:** Request rate, error rate, latency, payment volume
2. **Payments:** Success rate by method, provider performance, refund rate
3. **Security:** Auth failures, rate limit hits, blacklist blocks
4. **Infrastructure:** CPU, memory, database connections, Redis memory
5. **Webhooks:** Delivery success rate, retry queue depth, average latency

**Alerts:**
- **Critical (Page):** Error rate > 5%, database down, Redis down, payment success rate < 90%
- **High (Slack):** Error rate > 2%, latency P95 > 2s, webhook delivery rate < 95%
- **Medium (Email):** Provider error rate > 10%, reconciliation mismatches detected
- **Low (Dashboard):** Rate limit hits increasing, disk usage > 80%

### Logging

**Structured Logging (Serilog):**
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "FluxPay")
    .Enrich.WithProperty("Environment", environment)
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Logflare(apiKey, sourceId)
    .CreateLogger();
```

**Log Levels:**
- **Trace:** Detailed diagnostic information (disabled in production)
- **Debug:** Internal state for debugging (disabled in production)
- **Information:** General application flow (payment created, webhook sent)
- **Warning:** Unexpected but handled (provider timeout, retry triggered)
- **Error:** Errors requiring attention (payment failed, webhook delivery failed)
- **Critical:** System failures (database down, unhandled exception)

**Log Masking:**
```csharp
public class SensitiveDataMaskingEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        // Mask PAN patterns (16 digits)
        // Mask email addresses (show only domain)
        // Mask CPF/CNPJ (show only last 4 digits)
        // Mask API keys and secrets
    }
}
```

### Backup and Disaster Recovery

**Database Backups:**
- **Frequency:** Daily automated backups at 2 AM UTC
- **Retention:** 7 daily, 4 weekly, 12 monthly
- **Storage:** Render managed backups + manual export to S3
- **Encryption:** AES-256 encryption at rest

**Backup Testing:**
- Monthly restore test to staging environment
- Verify data integrity and application functionality
- Document restore time (RTO target: 4 hours)

**Disaster Recovery Plan:**
1. **Detection:** Monitoring alerts indicate outage
2. **Assessment:** Determine scope (database, application, provider)
3. **Communication:** Notify merchants via status page
4. **Recovery:** Restore from backup or failover to secondary region
5. **Verification:** Run smoke tests, verify payment processing
6. **Post-Mortem:** Document incident, update runbooks

**RPO/RTO Targets:**
- **Recovery Point Objective (RPO):** 1 hour (maximum data loss)
- **Recovery Time Objective (RTO):** 4 hours (maximum downtime)

### Runbooks

**Common Operations:**

1. **Rotate API Key:**
   - Generate new key via admin endpoint
   - Provide new key to merchant
   - Set grace period (30 days)
   - Deprecate old key after grace period

2. **Handle Provider Outage:**
   - Monitor provider status pages
   - Enable circuit breaker if error rate > 50%
   - Notify merchants via webhook and dashboard
   - Queue payments for retry when provider recovers

3. **Investigate Payment Failure:**
   - Query payment by ID
   - Review transaction history and provider responses
   - Check audit logs for related events
   - Contact provider support if needed

4. **Resolve Reconciliation Mismatch:**
   - Review reconciliation report
   - Compare FluxPay and provider records
   - Identify root cause (webhook missed, status not updated)
   - Manually update payment status if needed
   - Trigger merchant webhook

5. **Respond to Security Incident:**
   - Follow incident response playbook
   - Block malicious IPs via WAF
   - Revoke compromised API keys
   - Rotate secrets if needed
   - Notify affected merchants

