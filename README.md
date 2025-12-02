# FluxPay Payment Gateway

Gateway de pagamentos white-label enterprise-grade com backend em .NET 9 e frontend em Next.js.

## 🏗️ Estrutura do Projeto

```
FluxPay/
├── backend/              # API REST e Workers (.NET 9)
│   ├── src/
│   │   ├── FluxPay.Api/
│   │   ├── FluxPay.Core/
│   │   ├── FluxPay.Infrastructure/
│   │   └── FluxPay.Workers/
│   ├── tests/
│   └── FluxPay.sln
│
├── frontend/             # Dashboard Web (Next.js - Em desenvolvimento)
│   └── README.md
│
├── .kiro/               # Specs e documentação (não versionado)
└── CONTRIBUTING.md      # Guia de contribuição
```

## 🚀 Quick Start

### Backend

```bash
cd backend
dotnet build
dotnet run --project src/FluxPay.Api
```

API disponível em: `https://localhost:5001`

Documentação completa: [backend/README.md](backend/README.md)

### Frontend

_Em desenvolvimento. Será implementado após conclusão do backend._

Documentação: [frontend/README.md](frontend/README.md)

## 📋 Pré-requisitos

### Backend
- .NET 9.0 SDK
- PostgreSQL 14+
- Redis 6+

### Frontend (futuro)
- Node.js 18+
- npm ou yarn

## 🔧 Tecnologias

### Backend
- .NET 9.0 / ASP.NET Core
- Entity Framework Core + PostgreSQL
- Redis (StackExchange.Redis)
- xUnit para testes

### Frontend (planejado)
- Next.js 14+
- TypeScript
- Tailwind CSS
- React Query

## 📚 Documentação

- [Backend README](backend/README.md) - Documentação completa da API
- [Frontend README](frontend/README.md) - Planejamento do dashboard
- [CONTRIBUTING.md](CONTRIBUTING.md) - Guia de contribuição e estratégia Git
- Specs detalhadas em `.kiro/specs/fluxpay-payment-gateway/`

## 🔐 Segurança

- Autenticação HMAC para APIs machine-to-machine
- JWT (RS256) para autenticação de usuários
- Criptografia AES-256-GCM para dados sensíveis
- Rate limiting e proteção contra replay attacks
- Audit logs com assinatura HMAC

## 🤝 Contribuindo

Consulte [CONTRIBUTING.md](CONTRIBUTING.md) para detalhes sobre:
- Estratégia de commits (Conventional Commits)
- Workflow de desenvolvimento
- Padrões de código

## 📝 Status do Projeto

- ✅ **Task 1**: Estrutura do projeto e infraestrutura core
- 🔄 **Em andamento**: Implementação dos modelos de dados e migrations
- ⏳ **Próximo**: Serviços de criptografia e autenticação

Veja o progresso completo em `.kiro/specs/fluxpay-payment-gateway/tasks.md`
