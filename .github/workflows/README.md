# CI/CD Pipeline

This GitHub Actions workflow provides comprehensive CI/CD for the FluxPay payment gateway.

## Workflow Jobs

### 1. Test
Runs all test suites with PostgreSQL and Redis services:
- Unit tests
- Property-based tests
- Integration tests

### 2. Static Analysis
- dotnet-format verification
- Roslyn analyzer enforcement
- Code style validation

### 3. Security Scanning
- GitHub CodeQL analysis
- Snyk vulnerability scanning
- SARIF report generation

### 4. Dependency Scan
- Vulnerable package detection
- Transitive dependency analysis
- Automated vulnerability reporting

### 5. Build Docker
- Builds Docker image on main branch
- Pushes to Docker Hub
- Uses layer caching for optimization

## Required Secrets

Configure these secrets in GitHub repository settings:

- `DOCKER_USERNAME`: Docker Hub username
- `DOCKER_PASSWORD`: Docker Hub password or access token
- `SNYK_TOKEN`: Snyk API token (optional, for enhanced security scanning)

## Triggers

- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches

## Requirements Validation

This workflow validates:
- Requirement 23.1: Unit tests, property tests, integration tests
- Requirement 23.2: Static analysis (dotnet-format, Roslyn analyzers)
- Requirement 23.3: SAST (GitHub code scanning)
- Requirement 23.4: Dependency vulnerability scanning
