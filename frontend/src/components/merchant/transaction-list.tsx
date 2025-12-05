'use client';

import { Transaction } from '@/types/transaction';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { formatCurrency } from '@/lib/utils';
import { format } from 'date-fns';
import { ptBR } from 'date-fns/locale';

interface TransactionListProps {
  transactions: Transaction[];
  onViewDetails: (id: string) => void;
  isLoading: boolean;
}

const statusColors = {
  pending: 'bg-yellow-500',
  authorized: 'bg-blue-500',
  paid: 'bg-green-600',
  refunded: 'bg-purple-500',
  failed: 'bg-red-600',
  expired: 'bg-gray-500',
  cancelled: 'bg-gray-600',
};

const statusLabels = {
  pending: 'Pendente',
  authorized: 'Autorizado',
  paid: 'Pago',
  refunded: 'Reembolsado',
  failed: 'Falhou',
  expired: 'Expirado',
  cancelled: 'Cancelado',
};

const methodLabels = {
  card: 'Cartão',
  pix: 'PIX',
  boleto: 'Boleto',
};

export function TransactionList({ transactions, onViewDetails, isLoading }: TransactionListProps) {
  if (isLoading) {
    return (
      <div className="space-y-2">
        {[...Array(10)].map((_, i) => (
          <Skeleton key={i} className="h-16 w-full" />
        ))}
      </div>
    );
  }

  if (transactions.length === 0) {
    return (
      <div className="text-center py-12">
        <p className="text-muted-foreground">Nenhuma transação encontrada</p>
      </div>
    );
  }

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>ID</TableHead>
            <TableHead>Cliente</TableHead>
            <TableHead>Valor</TableHead>
            <TableHead>Método</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Data</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {transactions.map((transaction) => (
            <TableRow
              key={transaction.id}
              className="cursor-pointer hover:bg-muted/50"
              onClick={() => onViewDetails(transaction.id)}
            >
              <TableCell className="font-mono text-sm">
                {transaction.id.slice(0, 8)}...
              </TableCell>
              <TableCell>{transaction.customerEmail}</TableCell>
              <TableCell className="font-semibold">
                {formatCurrency(transaction.amount)}
              </TableCell>
              <TableCell>{methodLabels[transaction.method]}</TableCell>
              <TableCell>
                <Badge className={statusColors[transaction.status]}>
                  {statusLabels[transaction.status]}
                </Badge>
              </TableCell>
              <TableCell>
                {format(new Date(transaction.createdAt), 'dd/MM/yyyy HH:mm', { locale: ptBR })}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
