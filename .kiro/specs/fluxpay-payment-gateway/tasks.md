# Implementation Plan

- [x] 1. Set up project structure and core infrastructure





  - Create solution with projects: Api, Core, Infrastructure, Workers, Tests.Unit, Tests.Integration
  - Configure Entity Framework Core with PostgreSQL
  - Set up Redis connection and configuration
  - Configure dependency injection container
  - **IMPORTANT:** Write ONLY functional code, NO comments, NO XML documentation, NO unnecessary scripts
  - _Requirements: All_

- [ ] 2. Implement database models and migrations
- [ ] 2.1 Create EF Core DbContext and entity configurations
  - Implement Merchant, ApiKey, Customer, Payment, Transaction, Subscription entities
  - Implement MerchantWebhook, WebhookReceived, WebhookDelivery entities
  - Implement AuditLog, RefreshToken, User entities
  - Configure relationships, indexes, and constraints
  - _Requirements: All_

- [ ] 2.2 Create initial database migration
  - Generate migration with all tables from design document
  - Include indexes for performance (merchant_id, status, created_at)
  - _Requirements: All_

- [ ]* 2.3 Write property test for entity validation
  - **Property 2: PAN/CVV storage prohibition**
  - **Validates: Requirements 1.2**

- [ ] 3. Implement encryption and cryptography services
- [ ] 3.1 Create EncryptionService with AES-256-GCM
  - Implement Encrypt and Decrypt methods
  - Implement Hash and VerifyHash methods using SHA-256
  - Load encryption key from environment variables
  - _Requirements: 11.1, 11.2, 11.3_

- [ ]* 3.2 Write property test for encryption round-trip
  - **Property 36: Provider config encryption round-trip**
  - **Property 37: Webhook secret encryption round-trip**
  - **Validates: Requirements 11.1, 11.2, 11.3**

- [ ] 4. Implement HMAC signature services
- [ ] 4.1 Create HmacSignatureService
  - Implement ComputeSignature method for API requests and webhooks
  - Implement VerifySignature with constant-time comparison
  - Support message format: "${timestamp}.${nonce}.${payload}"
  - _Requirements: 6.2, 8.2_

- [ ]* 4.2 Write property test for signature round-trip
  - **Property 16: Webhook signature round-trip**
  - **Property 23: API signature round-trip**
  - **Validates: Requirements 6.2, 8.2**

- [ ] 5. Implement nonce store and replay protection
- [ ] 5.1 Create NonceStore using Redis
  - Implement IsNonceUniqueAsync method
  - Implement StoreNonceAsync with 24-hour TTL
  - Use key format: "nonce:{merchantId}:{nonce}"
  - _Requirements: 7.4, 8.4_

- [ ]* 5.2 Write property test for nonce replay protection
  - **Property 20: Webhook nonce replay protection**
  - **Property 25: API nonce replay protection**
  - **Validates: Requirements 7.4, 8.4**

- [ ] 6. Implement rate limiting service
- [ ] 6.1 Create RateLimiter using Redis sliding window
  - Implement CheckRateLimitAsync method
  - Support per-merchant and per-IP rate limits
  - Return remaining requests and reset time
  - _Requirements: 10.1, 10.2, 10.3, 10.4_

- [ ]* 6.2 Write property test for rate limit enforcement
  - **Property 32: Merchant rate limit enforcement**
  - **Property 33: Payment endpoint IP rate limit**
  - **Property 35: Rate limit window reset**
  - **Validates: Requirements 10.1, 10.2, 10.4**

- [ ] 7. Implement JWT authentication service
- [ ] 7.1 Create JwtService with RS256 signing
  - Implement GenerateAccessToken (15-minute expiry)
  - Implement GenerateRefreshToken (opaque, 30-day expiry)
  - Implement ValidateAccessToken
  - Store refresh tokens hashed in database
  - _Requirements: 9.1, 9.2, 9.4_

- [ ] 7.2 Implement refresh token rotation
  - Validate refresh token and check if revoked
  - Issue new access and refresh tokens
  - Mark old refresh token as revoked
  - Store new refresh token hashed
  - _Requirements: 9.2_

- [ ]* 7.3 Write property test for token lifecycle
  - **Property 27: Login token issuance format**
  - **Property 28: Refresh token rotation**
  - **Property 30: Expired access token rejection**
  - **Property 31: Revoked refresh token rejection**
  - **Validates: Requirements 9.1, 9.2, 9.4, 9.5**

- [ ] 8. Implement audit logging service
- [ ] 8.1 Create AuditService with HMAC signing
  - Implement LogAsync to create audit entries
  - Sign each entry with HMAC for integrity
  - Implement VerifyIntegrityAsync to check signatures
  - _Requirements: 12.1, 12.2, 12.3_

- [ ]* 8.2 Write property test for audit log integrity
  - **Property 38: Payment operation audit logging**
  - **Property 39: Audit log signature integrity**
  - **Validates: Requirements 12.1, 12.2, 12.3**

- [ ] 9. Implement provider adapters
- [ ] 9.1 Create IProviderAdapter interface
  - Define methods: AuthorizeAsync, CaptureAsync, RefundAsync
  - Define ValidateWebhookSignatureAsync
  - _Requirements: 1.1, 21.1_

- [ ] 9.2 Implement PagarMeAdapter
  - Implement card authorization with token
  - Implement capture and refund
  - Implement subscription creation and cancellation
  - Implement webhook signature validation
  - _Requirements: 1.1, 4.1, 4.4, 7.1_

- [ ] 9.3 Implement GerencianetAdapter
  - Implement PIX QR code generation
  - Implement boleto generation
  - Implement webhook signature validation
  - _Requirements: 2.1, 3.1, 7.1_

- [ ] 9.4 Create ProviderFactory
  - Implement GetProvider by name
  - Implement GetProviderForPaymentMethod
  - _Requirements: 1.1, 2.1, 3.1_

- [ ]* 9.5 Write property test for provider routing
  - **Property 58: Refund provider routing**
  - **Validates: Requirements 21.1**

- [ ] 10. Implement payment service
- [ ] 10.1 Create PaymentService core logic
  - Implement CreatePaymentAsync with validation
  - Implement GetPaymentAsync with merchant isolation
  - Implement RefundPaymentAsync
  - Call provider adapters for authorization
  - Create payment and transaction records
  - _Requirements: 1.1, 1.4, 21.1_

- [ ]* 10.2 Write property test for payment creation
  - **Property 1: Card payment creation invariant**
  - **Property 3: Authorized payment data constraints**
  - **Property 4: Payment validation ordering**
  - **Validates: Requirements 1.1, 1.3, 1.4**

- [ ] 10.3 Implement PIX payment creation
  - Call GerencianetAdapter to generate QR code
  - Store payment with status "pending"
  - Return QR code and payment ID
  - _Requirements: 2.1, 2.2_

- [ ]* 10.4 Write property test for PIX payments
  - **Property 5: PIX payment response completeness**
  - **Property 6: PIX initial state invariant**
  - **Validates: Requirements 2.1, 2.2**

- [ ] 10.5 Implement boleto payment creation
  - Call GerencianetAdapter to generate boleto
  - Store payment with status "pending" and expiration
  - Return barcode, PDF URL, and payment ID
  - _Requirements: 3.1, 3.2_

- [ ]* 10.6 Write property test for boleto payments
  - **Property 8: Boleto response completeness**
  - **Property 9: Boleto initial state invariant**
  - **Validates: Requirements 3.1, 3.2**

- [ ] 10.7 Implement refund logic
  - Validate payment is refundable
  - Call provider adapter for refund
  - Create refund transaction record
  - Update payment status
  - _Requirements: 21.1, 21.2, 21.3, 21.4_

- [ ]* 10.8 Write property test for refunds
  - **Property 59: Refund transaction linkage**
  - **Property 60: Refund completion flow**
  - **Property 61: Partial refund arithmetic**
  - **Validates: Requirements 21.2, 21.3, 21.4**

- [ ] 11. Implement subscription service
- [ ] 11.1 Create SubscriptionService
  - Implement CreateSubscriptionAsync via PagarMeAdapter
  - Implement GetSubscriptionAsync
  - Implement CancelSubscriptionAsync
  - Link subscription charges to payments
  - _Requirements: 4.1, 4.2, 4.4_

- [ ]* 11.2 Write property test for subscriptions
  - **Property 11: Subscription creation completeness**
  - **Property 12: Subscription payment linkage**
  - **Property 13: Subscription cancellation state transition**
  - **Validates: Requirements 4.1, 4.2, 4.4**

- [ ] 12. Implement webhook service
- [ ] 12.1 Create WebhookService for inbound webhooks
  - Implement ValidateProviderWebhookAsync (signature, timestamp, nonce)
  - Implement ProcessProviderWebhookAsync
  - Store webhook in webhooks_received table
  - Update payment status based on webhook event
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [ ]* 12.2 Write property test for inbound webhook validation
  - **Property 17: Inbound webhook validation completeness**
  - **Property 18: Invalid webhook signature rejection**
  - **Property 19: Webhook timestamp skew rejection**
  - **Property 21: Valid webhook processing flow**
  - **Validates: Requirements 7.1, 7.2, 7.3, 7.5**

- [ ] 12.3 Implement outbound merchant webhooks
  - Implement SendMerchantWebhookAsync with HMAC signing
  - Include headers: X-Signature, X-Timestamp, X-Nonce, X-Trace-Id
  - Store webhook delivery record
  - Handle delivery failures and queue for retry
  - _Requirements: 6.1, 6.2_

- [ ]* 12.4 Write property test for outbound webhooks
  - **Property 15: Outbound webhook signature format**
  - **Validates: Requirements 6.1**

- [ ] 12.4 Implement payment status change triggers
  - Trigger merchant webhook on status change (pending → paid, paid → refunded)
  - _Requirements: 2.3, 3.3, 21.3_

- [ ]* 12.5 Write property test for status transitions
  - **Property 7: PIX confirmation state transition**
  - **Property 10: Boleto confirmation state transition**
  - **Validates: Requirements 2.3, 3.3**

- [ ] 13. Implement API authentication middleware
- [ ] 13.1 Create ApiKeyAuthenticationMiddleware
  - Extract headers: X-Api-Key, X-Timestamp, X-Nonce, X-Signature
  - Validate timestamp skew (≤60 seconds)
  - Check nonce uniqueness via NonceStore
  - Verify HMAC signature with constant-time comparison
  - Set merchant context on success
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [ ]* 13.2 Write property test for API authentication
  - **Property 22: API request header requirements**
  - **Property 24: API timestamp skew rejection**
  - **Property 26: Invalid API signature rejection**
  - **Validates: Requirements 8.1, 8.3, 8.5**

- [ ] 13.2 Create JwtAuthenticationMiddleware
  - Validate JWT access token
  - Check expiration
  - Set user context on success
  - _Requirements: 9.4_

- [ ] 14. Implement rate limiting middleware
- [ ] 14.1 Create RateLimitingMiddleware
  - Check merchant rate limit (200/min)
  - Check endpoint-specific limits (payment: 20/min per IP)
  - Return 429 with Retry-After header if exceeded
  - _Requirements: 10.1, 10.2, 10.3_

- [ ]* 14.2 Write property test for rate limit headers
  - **Property 34: Rate limit response headers**
  - **Validates: Requirements 10.3**

- [ ] 15. Implement security headers middleware
- [ ] 15.1 Create SecurityHeadersMiddleware
  - Add Strict-Transport-Security, X-Content-Type-Options, X-Frame-Options, CSP
  - Remove Server and X-Powered-By headers
  - _Requirements: 20.2, 20.3_

- [ ]* 15.2 Write property test for security headers
  - **Property 55: Security headers presence**
  - **Property 56: Server header removal**
  - **Validates: Requirements 20.2, 20.3**

- [ ] 16. Implement antifraud service
- [ ] 16.1 Create AntifraudService
  - Implement IP velocity check
  - Implement CPF/BIN blacklist check
  - Implement adaptive IP blocking (3 failures in 10 min → block 1 hour)
  - Log antifraud events to audit trail
  - _Requirements: 14.1, 14.2, 14.3, 14.4_

- [ ]* 16.2 Write property test for antifraud rules
  - **Property 42: IP velocity limit enforcement**
  - **Property 43: Blacklist enforcement**
  - **Property 44: Adaptive IP blocking**
  - **Property 45: Antifraud audit logging**
  - **Validates: Requirements 14.1, 14.2, 14.3, 14.4**

- [ ] 17. Implement payment controllers
- [ ] 17.1 Create PaymentsController
  - POST /v1/payments (create payment)
  - GET /v1/payments/:id (get payment status)
  - POST /v1/payments/:id/refund (refund payment)
  - Apply ApiKeyAuthentication and RateLimiting
  - Validate request schemas
  - _Requirements: 1.1, 21.1_

- [ ]* 17.2 Write property test for strict JSON validation
  - **Property 57: Strict JSON validation**
  - **Validates: Requirements 20.4**

- [ ] 18. Implement subscription controllers
- [ ] 18.1 Create SubscriptionsController
  - POST /v1/subscriptions (create subscription)
  - GET /v1/subscriptions/:id (get subscription)
  - POST /v1/subscriptions/:id/cancel (cancel subscription)
  - Apply ApiKeyAuthentication and RateLimiting
  - _Requirements: 4.1, 4.4_

- [ ] 19. Implement webhook controllers
- [ ] 19.1 Create WebhooksController for provider webhooks
  - POST /v1/webhooks/provider (receive provider webhooks)
  - Validate provider signature
  - Process webhook asynchronously
  - _Requirements: 7.1_

- [ ] 19.2 Implement merchant webhook test endpoint
  - POST /v1/webhooks/merchant/test (test webhook delivery)
  - Send test webhook to merchant endpoint
  - Return delivery result
  - _Requirements: 15.3_

- [ ] 20. Implement authentication controllers
- [ ] 20.1 Create AuthController
  - POST /v1/auth/login (login with email, password, MFA)
  - POST /v1/auth/refresh (refresh access token)
  - POST /v1/auth/logout (revoke refresh tokens)
  - _Requirements: 9.1, 9.2, 9.3_

- [ ]* 20.2 Write property test for logout
  - **Property 29: Logout token revocation**
  - **Validates: Requirements 9.3**

- [ ] 21. Implement merchant management
- [ ] 21.1 Create MerchantsController for self-service
  - GET /v1/merchants/me (get merchant details)
  - GET /v1/merchants/me/transactions (list transactions)
  - POST /v1/merchants/me/api-keys/rotate (rotate API key)
  - Apply JwtAuthentication
  - _Requirements: 15.1, 15.2, 15.4_

- [ ]* 21.2 Write property test for API key rotation
  - **Property 46: API key rotation completeness**
  - **Validates: Requirements 15.4**

- [ ] 22. Implement admin controllers
- [ ] 22.1 Create AdminController with IP allowlist
  - POST /v1/admin/merchants (create merchant)
  - GET /v1/admin/merchants/:id (get merchant details)
  - POST /v1/admin/merchants/:id/disable (disable merchant)
  - Apply JwtAuthentication with admin role check
  - Apply IP allowlist middleware
  - _Requirements: 16.1, 16.2, 16.4, 17.1_

- [ ]* 22.2 Write property test for merchant management
  - **Property 47: Merchant creation completeness**
  - **Property 48: Provider credential encryption**
  - **Property 49: Disabled merchant request rejection**
  - **Validates: Requirements 16.1, 16.2, 16.4**

- [ ] 22.3 Implement admin MFA enforcement
  - Require MFA verification on admin login
  - Enforce 5-minute access token expiry for admins
  - _Requirements: 17.2, 17.4_

- [ ]* 22.4 Write property test for admin security
  - **Property 50: Admin IP allowlist enforcement**
  - **Property 51: Admin MFA requirement**
  - **Property 52: Admin MFA failure handling**
  - **Property 53: Admin token expiry**
  - **Validates: Requirements 17.1, 17.2, 17.3, 17.4**

- [ ] 23. Implement health check endpoint
- [ ] 23.1 Create HealthController
  - GET /health (check database, Redis, provider connectivity)
  - Return 200 OK with status details
  - _Requirements: 19.1_

- [ ] 24. Implement reconciliation worker
- [ ] 24.1 Create ReconciliationWorker background service
  - Run daily at 2 AM UTC
  - Fetch transaction reports from Pagar.me and Gerencianet
  - Compare with internal payment records
  - Flag mismatches and create alerts
  - Generate reconciliation report
  - _Requirements: 13.1, 13.2, 13.3, 13.4_

- [ ]* 24.2 Write property test for reconciliation
  - **Property 40: Reconciliation mismatch detection**
  - **Property 41: Reconciliation mismatch alerting**
  - **Validates: Requirements 13.2, 13.3**

- [ ] 25. Implement webhook retry worker
- [ ] 25.1 Create WebhookRetryWorker background service
  - Poll for failed webhook deliveries every 5 minutes
  - Retry with exponential backoff (1m, 5m, 15m, 30m, 1h, 2h, 4h, 8h, 12h, 24h)
  - Mark as permanently failed after 10 attempts
  - Send alert to merchant dashboard
  - _Requirements: 6.3, 6.4_

- [ ] 26. Implement sandbox mode
- [ ] 26.1 Add sandbox routing to provider adapters
  - Route to provider sandbox environments when sandbox mode enabled
  - Mark transactions with is_test=true
  - _Requirements: 18.2_

- [ ]* 26.2 Write property test for sandbox routing
  - **Property 54: Sandbox routing**
  - **Validates: Requirements 18.2**

- [ ] 27. Configure OpenTelemetry and logging
- [ ] 27.1 Set up OpenTelemetry tracing
  - Configure ASP.NET Core, HttpClient, Npgsql, Redis instrumentation
  - Export to Grafana Cloud or Logflare
  - _Requirements: 22.1_

- [ ] 27.2 Configure structured logging with Serilog
  - Log to console (JSON format) and Logflare
  - Implement sensitive data masking
  - _Requirements: 22.2_

- [ ] 28. Set up CI/CD pipeline
- [ ] 28.1 Create GitHub Actions workflow
  - Run unit tests, property tests, integration tests
  - Run static analysis (dotnet-format, Roslyn analyzers)
  - Run SAST (GitHub code scanning or Snyk)
  - Run dependency vulnerability scanning
  - Build Docker image on main branch
  - _Requirements: 23.1, 23.2, 23.3, 23.4_

- [ ] 29. Final checkpoint - Ensure all tests pass
  - Run all unit tests, property tests, and integration tests
  - Verify no compilation errors or warnings
  - Ensure all critical paths are covered
  - Ask the user if questions arise
