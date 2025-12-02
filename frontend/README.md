# FluxPay Frontend

Dashboard web do FluxPay para gerenciamento de merchants e visualização de transações.

## Tecnologias Planejadas

- **Framework**: Next.js 14+ (React)
- **Linguagem**: TypeScript
- **Estilização**: Tailwind CSS
- **State Management**: Zustand ou React Query
- **Autenticação**: JWT com refresh tokens
- **Gráficos**: Recharts ou Chart.js

## Estrutura Planejada

```
frontend/
├── src/
│   ├── app/              # App Router (Next.js 14+)
│   ├── components/       # Componentes reutilizáveis
│   ├── lib/             # Utilitários e configurações
│   ├── hooks/           # Custom React hooks
│   └── types/           # TypeScript types
├── public/              # Assets estáticos
└── package.json
```

## Funcionalidades Planejadas

### Dashboard Merchant
- Visualização de transações recentes
- Gráficos de volume de pagamentos
- Status de webhooks
- Gerenciamento de API keys
- Teste de webhooks

### Dashboard Admin
- Gerenciamento de merchants
- Visualização de métricas globais
- Configuração de provedores de pagamento
- Logs de auditoria

## Próximos Passos

1. Inicializar projeto Next.js
2. Configurar TypeScript e Tailwind CSS
3. Implementar autenticação JWT
4. Criar componentes base do dashboard
5. Integrar com API do backend

## Desenvolvimento

```bash
# Instalar dependências
npm install

# Executar em desenvolvimento
npm run dev

# Build para produção
npm run build

# Executar produção
npm start
```

_Este projeto será implementado após a conclusão do backend._
