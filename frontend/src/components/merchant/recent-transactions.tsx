import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { RecentTransaction } from '@/types/dashboard';

interface RecentTransactionsProps {
  transactions: RecentTransaction[];
}

export function RecentTransactions({ transactions }: RecentTransactionsProps) {
  const formatCurrency = (cents: number) => {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(cents / 100);
  };

  const formatDate = (dateString: string) => {
    return new Intl.DateTimeFormat('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(dateString));
  };

  const getStatusVariant = (status: RecentTransaction['status']) => {
    switch (status) {
      case 'paid':
        return 'default';
      case 'pending':
      case 'authorized':
        return 'secondary';
      case 'failed':
      case 'cancelled':
        return 'destructive';
      case 'refunded':
        return 'outline';
      default:
        return 'secondary';
    }
  };

  const getStatusLabel = (status: RecentTransaction['status']) => {
    const labels: Record<RecentTransaction['status'], string> = {
      pending: 'Pendente',
      authorized: 'Autorizado',
      paid: 'Pago',
      refunded: 'Reembolsado',
      failed: 'Falhou',
      expired: 'Expirado',
      cancelled: 'Cancelado',
    };
    return labels[status];
  };

  const getMethodLabel = (method: RecentTransaction['method']) => {
    const labels: Record<RecentTransaction['method'], string> = {
      card: 'Cartão',
      pix: 'PIX',
      boleto: 'Boleto',
    };
    return labels[method];
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Transações Recentes</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {transactions.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nenhuma transação encontrada</p>
          ) : (
            transactions.map((transaction) => (
              <div
                key={transaction.id}
                className="flex items-center justify-between border-b pb-4 last:border-0 last:pb-0"
              >
                <div className="space-y-1">
                  <p className="text-sm font-medium">{formatCurrency(transaction.amount)}</p>
                  <div className="flex items-center gap-2">
                    <Badge variant={getStatusVariant(transaction.status)}>
                      {getStatusLabel(transaction.status)}
                    </Badge>
                    <span className="text-xs text-muted-foreground">
                      {getMethodLabel(transaction.method)}
                    </span>
                  </div>
                  <p className="text-xs text-muted-foreground">{transaction.customerEmail}</p>
                </div>
                <div className="text-right">
                  <p className="text-xs text-muted-foreground">{formatDate(transaction.createdAt)}</p>
                </div>
              </div>
            ))
          )}
        </div>
      </CardContent>
    </Card>
  );
}
