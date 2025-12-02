# FluxPay Payment Gateway

Enterprise-grade white-label payment gateway built with .NET 9.

## Project Structure

```
FluxPay/
├── src/
│   ├── FluxPay.Api/              # ASP.NET Core Web API
│   ├── FluxPay.Core/             # Domain models and interfaces
│   ├── FluxPay.Infrastructure/   # Data access and external services
│   └── FluxPay.Workers/          # Background workers
├── tests/
│   ├── FluxPay.Tests.Unit/       # Unit tests
│   └── FluxPay.Tests.Integration/ # Integration tests
└── FluxPay.sln
```

## Prerequisites

- .NET 9.0 SDK
- PostgreSQL 14+
- Redis 6+

## Configuration

Update connection strings in `appsettings.json`:

```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Database=fluxpay;Username=postgres;Password=postgres"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

## Build

```bash
dotnet build
```

## Run

```bash
dotnet run --project src/FluxPay.Api
```

API will be available at `https://localhost:5001`

## Test

```bash
dotnet test
```
