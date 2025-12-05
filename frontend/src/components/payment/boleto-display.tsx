'use client';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Copy, Download, CheckCircle2 } from 'lucide-react';
import { useState } from 'react';
import { format } from 'date-fns';

export interface BoletoDisplayProps {
  boletoBarcode: string;
  boletoPdfUrl: string;
  expiresAt: string;
  onCopy: () => void;
}

export function BoletoDisplay({ boletoBarcode, boletoPdfUrl, expiresAt, onCopy }: BoletoDisplayProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(boletoBarcode);
      setCopied(true);
      onCopy();
      setTimeout(() => setCopied(false), 2000);
    } catch (error) {
      console.error('Failed to copy:', error);
    }
  };

  const handleDownload = () => {
    window.open(boletoPdfUrl, '_blank');
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Boleto Bancário</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          <div className="space-y-2">
            <p className="text-sm font-medium">Código de Barras:</p>
            <div className="bg-muted p-3 rounded text-sm font-mono break-all">
              {boletoBarcode}
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <Button onClick={handleCopy} variant={copied ? 'outline' : 'default'}>
              {copied ? (
                <>
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                  Copiado!
                </>
              ) : (
                <>
                  <Copy className="mr-2 h-4 w-4" />
                  Copiar código
                </>
              )}
            </Button>

            <Button onClick={handleDownload} variant="outline">
              <Download className="mr-2 h-4 w-4" />
              Baixar PDF
            </Button>
          </div>

          <div className="border-t pt-4">
            <p className="text-sm text-muted-foreground">
              Vencimento: {format(new Date(expiresAt), 'dd/MM/yyyy')}
            </p>
            <p className="text-xs text-muted-foreground mt-1">
              Após o vencimento, o boleto não poderá ser pago
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
