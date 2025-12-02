# FluxPay Backend

API REST do gateway de pagamentos FluxPay construída com .NET 9.

## Estrutura do Projeto

```
backend/
├── src/
│   ├── FluxPay.Api/              # ASP.NET Core Web API
│   ├── FluxPay.Core/             # Domain models e interfaces
│   ├── FluxPay.Infrastructure/   # Data access e serviços externos
│   └── FluxPay.Workers/          # Background workers
├── tests/
│   ├── FluxPay.Tests.Unit/       # Testes unitários
│   └── FluxPay.Tests.Integration/ # Testes de integração
└── FluxPay.sln
```

## Pré-requisitos

- .NET 9.0 SDK
- PostgreSQL 14+
- Redis 6+

## Configuração

1. Atualize as connection strings em `src/FluxPay.Api/appsettings.json`:

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

## Comandos

### Build
```bash
dotnet build
```

### Executar API
```bash
dotnet run --project src/FluxPay.Api
```

API disponível em: `https://localhost:5001`

Swagger UI: `https://localhost:5001/swagger`

### Executar Workers
```bash
dotnet run --project src/FluxPay.Workers
```

### Testes
```bash
# Todos os testes
dotnet test

# Apenas testes unitários
dotnet test tests/FluxPay.Tests.Unit

# Apenas testes de integração
dotnet test tests/FluxPay.Tests.Integration
```

### Migrations (Entity Framework)
```bash
# Criar migration
dotnet ef migrations add MigrationName --project src/FluxPay.Infrastructure --startup-project src/FluxPay.Api

# Aplicar migrations
dotnet ef database update --project src/FluxPay.Infrastructure --startup-project src/FluxPay.Api
```

## Tecnologias

- **Framework**: .NET 9.0
- **API**: ASP.NET Core Web API
- **ORM**: Entity Framework Core 8.0
- **Database**: PostgreSQL (Npgsql)
- **Cache**: Redis (StackExchange.Redis)
- **Testes**: xUnit
- **Documentação**: Swagger/OpenAPI

## Arquitetura

O projeto segue os princípios de Clean Architecture:

- **Api**: Controllers, Middleware, configuração da aplicação
- **Core**: Entidades de domínio, interfaces, configurações
- **Infrastructure**: Implementações de repositórios, serviços externos, DbContext
- **Workers**: Serviços em background (reconciliação, retry de webhooks)

## Segurança

- Autenticação HMAC para APIs machine-to-machine
- JWT (RS256) para autenticação de usuários
- Criptografia AES-256-GCM para dados sensíveis
- Rate limiting por merchant e por IP
- Proteção contra replay attacks com nonces
- Audit logs com assinatura HMAC

## Próximos Passos

Consulte o arquivo `tasks.md` na pasta `.kiro/specs/fluxpay-payment-gateway/` para ver as tarefas de implementação.
