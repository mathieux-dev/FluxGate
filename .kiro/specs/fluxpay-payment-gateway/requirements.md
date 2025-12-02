# Requirements Document

## Introduction

FluxPay é um gateway de pagamentos white-label MVP construído com .NET 7, focado em segurança enterprise-grade, tokenização de cartões (sem armazenamento de PAN), e integração com múltiplos provedores de pagamento (Pagar.me para cartões/recorrência e Gerencianet para PIX/boleto). O sistema implementa autenticação HMAC para APIs machine-to-machine, webhooks assinados bidirecionais, proteção contra replay attacks, rate limiting, auditoria append-only, e conciliação automatizada.

## Glossary

- **FluxPay**: O sistema de gateway de pagamentos white-label
- **Merchant**: Cliente do gateway que processa pagamentos através do FluxPay
- **PAN**: Primary Account Number (número do cartão) - NUNCA armazenado
- **CVV**: Card Verification Value - NUNCA armazenado
- **Tokenização**: Processo de substituir dados sensíveis por tokens opacos
- **Provider**: Provedor de pagamento externo (Pagar.me, Gerencianet)
- **HMAC**: Hash-based Message Authentication Code para assinatura de mensagens
- **Nonce**: Number used once - valor único para prevenir replay attacks
- **Webhook**: Notificação HTTP assíncrona de eventos
- **API Key**: Credencial machine-to-machine para autenticação de merchants
- **JWT**: JSON Web Token para autenticação de usuários do dashboard
- **Replay Attack**: Ataque onde requisição válida é retransmitida maliciosamente
- **Rate Limiting**: Limitação de taxa de requisições por período
- **Audit Log**: Registro imutável de operações para auditoria
- **Reconciliation**: Processo de conciliação entre registros internos e do provedor
- **Background Worker**: Serviço que executa tarefas assíncronas em background
- **Redis**: Cache in-memory usado para nonces, rate limiting e cache
- **PostgreSQL**: Banco de dados relacional principal
- **Pagar.me**: Provedor para processamento de cartões e recorrência
- **Gerencianet**: Provedor para PIX e boleto bancário
- **White-label**: Sistema customizável com marca do cliente

## Requirements

### Requirement 1

**User Story:** As a merchant, I want to process credit card payments through tokenization, so that I never handle sensitive card data directly and remain PCI-compliant.

#### Acceptance Criteria

1. WHEN a merchant initiates a card payment with a valid card token from Pagar.me, THEN the FluxPay SHALL create a payment record and authorize the transaction through Pagar.me
2. WHEN the FluxPay processes a card payment, THEN the FluxPay SHALL NOT store PAN, CVV, or track data in any form
3. WHEN a card authorization succeeds, THEN the FluxPay SHALL store only the provider token, transaction ID, and masked card information
4. WHEN a card payment is created, THEN the FluxPay SHALL validate the request schema, apply rate limiting, and execute antifraud checks before forwarding to the provider
5. WHEN a card transaction completes, THEN the FluxPay SHALL return a payment ID and status to the merchant within 5 seconds

### Requirement 2

**User Story:** As a merchant, I want to accept PIX payments with dynamic QR codes, so that customers can pay instantly using their banking apps.

#### Acceptance Criteria

1. WHEN a merchant requests a PIX payment, THEN the FluxPay SHALL generate a dynamic QR code through Gerencianet and return the QR code data and payment ID
2. WHEN a PIX payment is created, THEN the FluxPay SHALL store the payment record with status "pending" and the Gerencianet transaction reference
3. WHEN Gerencianet notifies the FluxPay of PIX payment confirmation, THEN the FluxPay SHALL update the payment status to "paid" and trigger merchant webhook
4. WHEN a PIX QR code expires without payment, THEN the FluxPay SHALL update the payment status to "expired" after receiving provider notification

### Requirement 3

**User Story:** As a merchant, I want to generate boleto bancário for customers, so that they can pay at banks, lottery shops, or via banking apps.

#### Acceptance Criteria

1. WHEN a merchant requests a boleto payment, THEN the FluxPay SHALL generate a boleto through Gerencianet and return the barcode, PDF URL, and payment ID
2. WHEN a boleto is created, THEN the FluxPay SHALL store the payment record with status "pending" and expiration date
3. WHEN Gerencianet notifies the FluxPay of boleto payment confirmation, THEN the FluxPay SHALL update the payment status to "paid" and trigger merchant webhook
4. WHEN a boleto expires without payment, THEN the FluxPay SHALL update the payment status to "expired"

### Requirement 4

**User Story:** As a merchant, I want to create recurring subscriptions for customers, so that I can charge them automatically on a regular schedule.

#### Acceptance Criteria

1. WHEN a merchant creates a subscription with valid parameters, THEN the FluxPay SHALL create the subscription through Pagar.me and return the subscription ID
2. WHEN a subscription charge occurs, THEN the FluxPay SHALL create a payment record linked to the subscription
3. WHEN a subscription charge fails, THEN the FluxPay SHALL retry according to the configured retry policy and notify the merchant
4. WHEN a merchant cancels a subscription, THEN the FluxPay SHALL cancel it with Pagar.me and update the status to "cancelled"

### Requirement 5

**User Story:** As a merchant, I want to generate payment links with QR codes, so that customers can pay without integration complexity.

#### Acceptance Criteria

1. WHEN a merchant requests a payment link, THEN the FluxPay SHALL generate a unique URL and QR code for the payment
2. WHEN a customer accesses the payment link, THEN the FluxPay SHALL display a payment page with available payment methods
3. WHEN a customer completes payment through the link, THEN the FluxPay SHALL process the payment and redirect to the configured success URL
4. WHEN a payment link expires, THEN the FluxPay SHALL reject payment attempts and display an expiration message

### Requirement 6

**User Story:** As a merchant, I want to receive signed webhooks for payment events, so that I can update my system in real-time with cryptographic verification.

#### Acceptance Criteria

1. WHEN a payment status changes, THEN the FluxPay SHALL send an HMAC-SHA256 signed webhook to the merchant's configured endpoint with headers X-Signature, X-Timestamp, X-Nonce, and X-Trace-Id
2. WHEN the FluxPay sends a webhook, THEN the FluxPay SHALL compute the signature as Base64(HMAC_SHA256(merchant_secret, "${timestamp}.${nonce}.${payload}"))
3. WHEN a webhook delivery fails, THEN the FluxPay SHALL retry with exponential backoff up to 10 attempts over 24 hours
4. WHEN all webhook retries are exhausted, THEN the FluxPay SHALL mark the webhook as failed and alert the merchant through the dashboard

### Requirement 7

**User Story:** As the FluxPay system, I want to receive and validate webhooks from payment providers, so that I can update payment statuses accurately and securely.

#### Acceptance Criteria

1. WHEN the FluxPay receives a webhook from Pagar.me or Gerencianet, THEN the FluxPay SHALL validate the provider's signature, timestamp, and nonce before processing
2. WHEN a provider webhook signature is invalid, THEN the FluxPay SHALL reject the request with 401 Unauthorized and log the attempt
3. WHEN a provider webhook timestamp differs from current time by more than 120 seconds, THEN the FluxPay SHALL reject the request with 401 Unauthorized
4. WHEN a provider webhook nonce has been used before, THEN the FluxPay SHALL reject the request with 401 Unauthorized to prevent replay attacks
5. WHEN a valid provider webhook is received, THEN the FluxPay SHALL store it in webhooks_received table, update payment status, and trigger merchant webhook

### Requirement 8

**User Story:** As a merchant, I want to authenticate API requests using HMAC signatures, so that my requests are cryptographically verified and protected against tampering.

#### Acceptance Criteria

1. WHEN a merchant makes an API request, THEN the merchant SHALL send headers X-Api-Key, X-Timestamp, X-Nonce, and X-Signature
2. WHEN the FluxPay validates an API request, THEN the FluxPay SHALL compute the signature as HMAC_SHA256(kid_secret, "${timestamp}.${nonce}.${method}.${path}.${bodySha256Hex}") and compare using constant-time comparison
3. WHEN an API request timestamp differs from server time by more than 60 seconds, THEN the FluxPay SHALL reject the request with 401 Unauthorized
4. WHEN an API request nonce has been used before within 24 hours, THEN the FluxPay SHALL reject the request with 401 Unauthorized
5. WHEN an API request signature is invalid, THEN the FluxPay SHALL reject the request with 401 Unauthorized and log the attempt

### Requirement 9

**User Story:** As a dashboard user, I want to authenticate with JWT tokens, so that I can securely access the merchant dashboard.

#### Acceptance Criteria

1. WHEN a user logs in with valid credentials, THEN the FluxPay SHALL issue an RS256-signed access token with 15-minute expiry and an opaque refresh token with 30-day expiry
2. WHEN a user requests token refresh, THEN the FluxPay SHALL validate the refresh token, issue new access and refresh tokens, mark the old refresh token as revoked, and store the new refresh token hashed in the database
3. WHEN a user logs out, THEN the FluxPay SHALL revoke all refresh tokens for that session
4. WHEN an access token expires, THEN the FluxPay SHALL reject requests with 401 Unauthorized and require token refresh
5. WHEN a revoked refresh token is used, THEN the FluxPay SHALL reject the request with 401 Unauthorized

### Requirement 10

**User Story:** As the FluxPay system, I want to implement rate limiting per merchant and endpoint, so that I prevent abuse and ensure fair resource usage.

#### Acceptance Criteria

1. WHEN a merchant makes API requests, THEN the FluxPay SHALL limit requests to 200 per minute per merchant using Redis sliding window
2. WHEN requests to the payment creation endpoint exceed 20 per minute per IP address, THEN the FluxPay SHALL reject additional requests with 429 Too Many Requests
3. WHEN rate limit is exceeded, THEN the FluxPay SHALL include Retry-After header indicating seconds until limit resets
4. WHEN rate limit window expires, THEN the FluxPay SHALL reset the counter for the next window

### Requirement 11

**User Story:** As a security administrator, I want all sensitive configuration data encrypted at rest, so that compromised database access does not expose secrets.

#### Acceptance Criteria

1. WHEN the FluxPay stores provider configuration, THEN the FluxPay SHALL encrypt the data using AES-256-GCM with a key from KMS or Render Secrets
2. WHEN the FluxPay stores merchant webhook secrets, THEN the FluxPay SHALL encrypt the secrets using AES-256-GCM
3. WHEN the FluxPay retrieves encrypted data, THEN the FluxPay SHALL decrypt it using the same encryption key
4. WHEN encryption keys are rotated, THEN the FluxPay SHALL re-encrypt all sensitive data with the new key according to documented rotation procedure

### Requirement 12

**User Story:** As a compliance officer, I want all system operations logged in an append-only audit trail, so that I can investigate incidents and prove compliance.

#### Acceptance Criteria

1. WHEN any payment operation occurs, THEN the FluxPay SHALL create an append-only audit log entry with timestamp, actor, action, resource, and outcome
2. WHEN an audit log entry is created, THEN the FluxPay SHALL sign it with HMAC to ensure integrity
3. WHEN audit logs are queried, THEN the FluxPay SHALL verify HMAC signatures to detect tampering
4. WHEN audit logs reach retention threshold, THEN the FluxPay SHALL export them to S3 or Azure Blob Storage for long-term immutable storage

### Requirement 13

**User Story:** As a merchant, I want the system to perform daily reconciliation with payment providers, so that discrepancies are detected and resolved promptly.

#### Acceptance Criteria

1. WHEN the reconciliation worker runs daily, THEN the FluxPay SHALL fetch transaction reports from Pagar.me and Gerencianet for the previous day
2. WHEN the reconciliation worker compares records, THEN the FluxPay SHALL identify payments with status mismatches between FluxPay and providers
3. WHEN a reconciliation mismatch is detected, THEN the FluxPay SHALL create an alert and flag the payment for manual review
4. WHEN reconciliation completes, THEN the FluxPay SHALL generate a reconciliation report accessible in the dashboard

### Requirement 14

**User Story:** As a merchant, I want basic antifraud rules applied to transactions, so that suspicious activity is blocked automatically.

#### Acceptance Criteria

1. WHEN a payment request originates from an IP address exceeding velocity limits, THEN the FluxPay SHALL reject the request with 429 Too Many Requests
2. WHEN a payment request contains a CPF or BIN on the blacklist, THEN the FluxPay SHALL reject the request with 403 Forbidden
3. WHEN multiple failed payment attempts occur from the same IP within 10 minutes, THEN the FluxPay SHALL temporarily block that IP for 1 hour
4. WHEN an antifraud rule triggers, THEN the FluxPay SHALL log the event in the audit trail with rule details

### Requirement 15

**User Story:** As a merchant, I want to access a dashboard to view transactions, test webhooks, and manage API keys, so that I can monitor and control my integration.

#### Acceptance Criteria

1. WHEN a merchant logs into the dashboard, THEN the FluxPay SHALL display a list of recent transactions with status, amount, method, and timestamp
2. WHEN a merchant views transaction details, THEN the FluxPay SHALL display full transaction history, provider responses, and webhook delivery status
3. WHEN a merchant tests webhook delivery, THEN the FluxPay SHALL send a test webhook to the configured endpoint and display the response
4. WHEN a merchant rotates API keys, THEN the FluxPay SHALL generate a new key pair, display the new secret once, and mark the old key for deprecation after a grace period

### Requirement 16

**User Story:** As an administrator, I want to create and manage merchant accounts, so that I can onboard new clients to the platform.

#### Acceptance Criteria

1. WHEN an administrator creates a merchant account, THEN the FluxPay SHALL generate a unique merchant ID, API key pair, and store the API key hash
2. WHEN an administrator configures provider credentials for a merchant, THEN the FluxPay SHALL encrypt and store the credentials
3. WHEN an administrator views merchant details, THEN the FluxPay SHALL display merchant metadata, transaction volume, and status
4. WHEN an administrator disables a merchant, THEN the FluxPay SHALL reject all API requests from that merchant with 403 Forbidden

### Requirement 17

**User Story:** As a security administrator, I want admin endpoints protected by IP allowlist and MFA, so that administrative functions are secured against unauthorized access.

#### Acceptance Criteria

1. WHEN a request to an admin endpoint originates from an IP not on the allowlist, THEN the FluxPay SHALL reject the request with 403 Forbidden
2. WHEN an admin user logs in, THEN the FluxPay SHALL require MFA verification before issuing tokens
3. WHEN an admin user fails MFA verification, THEN the FluxPay SHALL reject the login attempt and log the failure
4. WHEN an admin session is established, THEN the FluxPay SHALL enforce shorter token expiry (5 minutes for access tokens)

### Requirement 18

**User Story:** As a developer, I want comprehensive API documentation with sandbox environment, so that I can integrate FluxPay without risking production data.

#### Acceptance Criteria

1. WHEN a developer accesses the API documentation, THEN the FluxPay SHALL provide OpenAPI/Swagger documentation with all endpoints, schemas, and examples
2. WHEN a developer uses sandbox mode, THEN the FluxPay SHALL route requests to provider sandbox environments and clearly mark transactions as test
3. WHEN a developer views webhook documentation, THEN the FluxPay SHALL provide signature validation examples in multiple languages
4. WHEN a developer accesses production documentation, THEN the FluxPay SHALL require authentication to view sensitive details

### Requirement 19

**User Story:** As a DevOps engineer, I want the system deployed as Docker containers with health checks, so that I can ensure reliability and easy scaling.

#### Acceptance Criteria

1. WHEN the FluxPay API starts, THEN the FluxPay SHALL expose a /health endpoint returning 200 OK with database and Redis connectivity status
2. WHEN the FluxPay is deployed, THEN the FluxPay SHALL run as a Docker container with all dependencies containerized
3. WHEN the FluxPay connects to PostgreSQL, THEN the FluxPay SHALL use connection pooling and automatic retry on transient failures
4. WHEN the FluxPay connects to Redis, THEN the FluxPay SHALL handle connection failures gracefully and log errors

### Requirement 20

**User Story:** As a security engineer, I want all HTTP traffic encrypted with TLS 1.2+ and security headers configured, so that data in transit is protected.

#### Acceptance Criteria

1. WHEN the FluxPay serves any endpoint, THEN the FluxPay SHALL require TLS 1.2 or higher and reject unencrypted connections
2. WHEN the FluxPay responds to requests, THEN the FluxPay SHALL include security headers: Strict-Transport-Security, X-Content-Type-Options, X-Frame-Options, and Content-Security-Policy
3. WHEN the FluxPay responds to requests, THEN the FluxPay SHALL remove server identification headers (Server, X-Powered-By)
4. WHEN the FluxPay receives requests with unexpected JSON fields, THEN the FluxPay SHALL reject the request with 400 Bad Request

### Requirement 21

**User Story:** As a merchant, I want to refund payments, so that I can handle returns and disputes appropriately.

#### Acceptance Criteria

1. WHEN a merchant requests a refund for a paid transaction, THEN the FluxPay SHALL initiate the refund through the original payment provider
2. WHEN a refund is processed, THEN the FluxPay SHALL create a refund transaction record linked to the original payment
3. WHEN a refund completes, THEN the FluxPay SHALL update the payment status to "refunded" and trigger a merchant webhook
4. WHEN a partial refund is requested, THEN the FluxPay SHALL process the refund for the specified amount and update the remaining balance

### Requirement 22

**User Story:** As a monitoring engineer, I want the system instrumented with OpenTelemetry and logs exported to observability platforms, so that I can detect and diagnose issues quickly.

#### Acceptance Criteria

1. WHEN the FluxPay processes requests, THEN the FluxPay SHALL emit OpenTelemetry traces with span IDs for distributed tracing
2. WHEN the FluxPay logs events, THEN the FluxPay SHALL export structured logs to Logflare or Grafana Cloud
3. WHEN critical errors occur, THEN the FluxPay SHALL trigger alerts through the configured alerting system
4. WHEN payment failure rates exceed 5% over 5 minutes, THEN the FluxPay SHALL trigger an anomaly alert

### Requirement 23

**User Story:** As a security engineer, I want automated SAST and dependency scanning in CI/CD, so that vulnerabilities are detected before deployment.

#### Acceptance Criteria

1. WHEN code is pushed to the repository, THEN the CI pipeline SHALL run static analysis using Roslyn analyzers and dotnet-format
2. WHEN the CI pipeline runs, THEN the CI pipeline SHALL execute SAST scanning using GitHub code scanning or Snyk
3. WHEN the CI pipeline detects high-severity vulnerabilities, THEN the CI pipeline SHALL fail the build and prevent deployment
4. WHEN dependencies have known vulnerabilities, THEN the CI pipeline SHALL create alerts and block merging to main branch
