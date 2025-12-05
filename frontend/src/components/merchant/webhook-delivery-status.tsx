'use client';

import { WebhookDelivery } from '@/types/transaction';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { format } from 'date-fns';
import { ptBR } from 'date-fns/locale';
import { CheckCircle2, XCircle, Clock } from 'lucide-react';

interface WebhookDeliveryStatusProps {
  deliveries: WebhookDelivery[];
}

const statusIcons = {
  pending: <Clock className="h-4 w-4" />,
  success: <CheckCircle2 className="h-4 w-4" />,
  failed: <XCircle className="h-4 w-4" />,
};

const statusLabels = {
  pending: 'Pendente',
  success: 'Entregue',
  failed: 'Falhou',
};

const statusColors = {
  pending: 'bg-yellow-500',
  success: 'bg-green-600',
  failed: 'bg-red-600',
};

export function WebhookDeliveryStatus({ deliveries }: WebhookDeliveryStatusProps) {
  if (deliveries.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Status de Webhooks</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">Nenhum webhook configurado</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Status de Webhooks</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {deliveries.map((delivery) => (
            <div key={delivery.id} className="border rounded-lg p-4 space-y-2">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  {statusIcons[delivery.status]}
                  <span className="font-medium">{delivery.url}</span>
                </div>
                <Badge className={statusColors[delivery.status]}>
                  {statusLabels[delivery.status]}
                </Badge>
              </div>
              
              <div className="text-sm text-muted-foreground space-y-1">
                <p>
                  Tentativas: {delivery.retryCount}
                </p>
                <p>
                  Última tentativa:{' '}
                  {format(new Date(delivery.lastAttemptAt), "dd/MM/yyyy 'às' HH:mm", {
                    locale: ptBR,
                  })}
                </p>
                {delivery.response && (
                  <details className="mt-2">
                    <summary className="cursor-pointer text-palmeiras-green hover:underline">
                      Ver resposta
                    </summary>
                    <pre className="mt-2 p-2 bg-muted rounded text-xs overflow-auto">
                      {delivery.response}
                    </pre>
                  </details>
                )}
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
