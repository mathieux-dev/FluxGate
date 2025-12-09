'use client';

import { useState } from 'react';
import Image from 'next/image';
import { format } from 'date-fns';
import { ptBR } from 'date-fns/locale';
import { Copy, Download, ExternalLink } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/hooks/use-toast';
import { PaymentLink } from '@/types/payment-link';
import QRCode from 'qrcode';

interface PaymentLinkDetailsProps {
  paymentLink: PaymentLink;
}

export function PaymentLinkDetails({ paymentLink }: PaymentLinkDetailsProps) {
  const { toast } = useToast();
  const [qrCodeUrl, setQrCodeUrl] = useState<string>('');

  const formatCurrency = (cents: number) => {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(cents / 100);
  };

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

  const copyToClipboard = async (text: string, label: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast({
        title: 'Copiado!',
        description: `${label} copiado para a área de transferência`,
      });
    } catch (error) {
      toast({
        title: 'Erro ao copiar',
        description: 'Não foi possível copiar para a área de transferência',
        variant: 'destructive',
      });
    }
  };

  const downloadQRCode = async () => {
    try {
      const url = await QRCode.toDataURL(paymentLink.url, {
        width: 512,
        margin: 2,
      });
      setQrCodeUrl(url);

      const link = document.createElement('a');
      link.href = url;
      link.download = `qrcode-${paymentLink.id}.png`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      toast({
        title: 'QR Code baixado',
        description: 'O QR Code foi baixado com sucesso',
      });
    } catch (error) {
      toast({
        title: 'Erro ao baixar',
        description: 'Não foi possível baixar o QR Code',
        variant: 'destructive',
      });
    }
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Informações do Link</CardTitle>
              <CardDescription>Detalhes do link de pagamento</CardDescription>
            </div>
            {getStatusBadge(paymentLink.status)}
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <p className="text-sm font-medium text-muted-foreground">Descrição</p>
              <p className="text-lg">{paymentLink.description}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Valor</p>
              <p className="text-lg font-bold">{formatCurrency(paymentLink.amount)}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Criado em</p>
              <p className="text-lg">
                {format(new Date(paymentLink.createdAt), "dd 'de' MMMM 'de' yyyy", {
                  locale: ptBR,
                })}
              </p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Expira em</p>
              <p className="text-lg">
                {format(new Date(paymentLink.expiresAt), "dd 'de' MMMM 'de' yyyy", {
                  locale: ptBR,
                })}
              </p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Pagamentos</p>
              <p className="text-lg">{paymentLink.paymentCount}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Link de Pagamento</CardTitle>
          <CardDescription>Compartilhe este link com seus clientes</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-2">
            <div className="flex-1 p-3 bg-muted rounded-md font-mono text-sm break-all">
              {paymentLink.url}
            </div>
            <Button
              variant="outline"
              size="icon"
              onClick={() => copyToClipboard(paymentLink.url, 'Link')}
            >
              <Copy className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              onClick={() => window.open(paymentLink.url, '_blank')}
            >
              <ExternalLink className="h-4 w-4" />
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>QR Code</CardTitle>
          <CardDescription>
            Seus clientes podem escanear este QR Code para acessar o pagamento
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-col items-center gap-4">
            <div className="p-4 bg-white rounded-lg">
              <Image
                src={paymentLink.qrCode}
                alt="QR Code"
                width={256}
                height={256}
                className="w-64 h-64"
              />
            </div>
            <Button
              variant="outline"
              onClick={downloadQRCode}
              className="w-full sm:w-auto"
            >
              <Download className="h-4 w-4 mr-2" />
              Baixar QR Code
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
