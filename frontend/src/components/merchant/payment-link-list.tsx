'use client';

import { useRouter } from 'next/navigation';
import { formatDistanceToNow } from 'date-fns';
import { ptBR } from 'date-fns/locale';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { PaymentLink } from '@/types/payment-link';

interface PaymentLinkListProps {
  paymentLinks: PaymentLink[];
  isLoading: boolean;
}

export function PaymentLinkList({ paymentLinks, isLoading }: PaymentLinkListProps) {
  const router = useRouter();

  if (isLoading) {
    return (
      <div className="space-y-2">
        {[...Array(5)].map((_, i) => (
          <Skeleton key={i} className="h-16 w-full" />
        ))}
      </div>
    );
  }

  if (!paymentLinks.length) {
    return (
      <div className="text-center py-12">
        <p className="text-muted-foreground">Nenhum link de pagamento encontrado</p>
      </div>
    );
  }

  const getStatusBadge = (status: PaymentLink['status']) => {
    const variants = {
      active: 'default',
      expired: 'secondary',
      paid: 'default',
    } as const;

    const labels = {
      active: 'Ativo',
      expired: 'Expirado',
      paid: 'Pago',
    };

    return (
      <Badge variant={variants[status]} className={status === 'active' ? 'bg-palmeiras-green' : ''}>
        {labels[status]}
      </Badge>
    );
  };

  const formatCurrency = (cents: number) => {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(cents / 100);
  };

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Descrição</TableHead>
            <TableHead>Valor</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Pagamentos</TableHead>
            <TableHead>Criado</TableHead>
            <TableHead>Expira</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {paymentLinks.map((link) => (
            <TableRow
              key={link.id}
              className="cursor-pointer hover:bg-muted/50"
              onClick={() => router.push(`/payment-links/${link.id}`)}
            >
              <TableCell className="font-medium">{link.description}</TableCell>
              <TableCell>{formatCurrency(link.amount)}</TableCell>
              <TableCell>{getStatusBadge(link.status)}</TableCell>
              <TableCell>{link.paymentCount}</TableCell>
              <TableCell>
                {formatDistanceToNow(new Date(link.createdAt), {
                  addSuffix: true,
                  locale: ptBR,
                })}
              </TableCell>
              <TableCell>
                {formatDistanceToNow(new Date(link.expiresAt), {
                  addSuffix: true,
                  locale: ptBR,
                })}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
