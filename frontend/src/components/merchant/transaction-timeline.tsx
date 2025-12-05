'use client';

import { TransactionEvent } from '@/types/transaction';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { format } from 'date-fns';
import { ptBR } from 'date-fns/locale';
import { CheckCircle2, XCircle, Clock } from 'lucide-react';

interface TransactionTimelineProps {
  events: TransactionEvent[];
}

const eventTypeLabels = {
  authorization: 'Autorização',
  capture: 'Captura',
  refund: 'Reembolso',
  chargeback: 'Chargeback',
};

const statusIcons = {
  pending: <Clock className="h-5 w-5 text-yellow-500" />,
  success: <CheckCircle2 className="h-5 w-5 text-green-600" />,
  failed: <XCircle className="h-5 w-5 text-red-600" />,
};

const statusLabels = {
  pending: 'Pendente',
  success: 'Sucesso',
  failed: 'Falhou',
};

export function TransactionTimeline({ events }: TransactionTimelineProps) {
  if (events.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Timeline de Eventos</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">Nenhum evento registrado</p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Timeline de Eventos</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {events.map((event, index) => (
            <div key={event.id} className="flex gap-4">
              <div className="flex flex-col items-center">
                <div className="flex h-10 w-10 items-center justify-center rounded-full border-2 bg-background">
                  {statusIcons[event.status]}
                </div>
                {index < events.length - 1 && (
                  <div className="h-full w-0.5 bg-border mt-2" />
                )}
              </div>
              
              <div className="flex-1 pb-8">
                <div className="flex items-center justify-between mb-1">
                  <h4 className="font-semibold">{eventTypeLabels[event.type]}</h4>
                  <Badge variant={event.status === 'success' ? 'default' : 'secondary'}>
                    {statusLabels[event.status]}
                  </Badge>
                </div>
                
                <p className="text-sm text-muted-foreground mb-2">
                  {format(new Date(event.createdAt), "dd/MM/yyyy 'às' HH:mm", { locale: ptBR })}
                </p>
                
                <div className="text-sm space-y-1">
                  <p>
                    <span className="font-medium">Valor:</span>{' '}
                    {new Intl.NumberFormat('pt-BR', {
                      style: 'currency',
                      currency: 'BRL',
                    }).format(event.amount / 100)}
                  </p>
                  <p>
                    <span className="font-medium">ID Provedor:</span>{' '}
                    <span className="font-mono text-xs">{event.providerTxId}</span>
                  </p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
