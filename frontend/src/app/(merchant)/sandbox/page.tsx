'use client';

import { useState } from 'react';
import { PaymentForm, PaymentMethod } from '@/components/payment/payment-form';
import { CardTokenization } from '@/components/payment/card-tokenization';
import { PIXQRCode } from '@/components/payment/pix-qr-code';
import { BoletoDisplay } from '@/components/payment/boleto-display';
import { TestCredentials } from '@/components/sandbox/test-credentials';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { CheckCircle2, XCircle } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';

type PaymentResult = {
  method: PaymentMethod;
  paymentId: string;
  qrCode?: string;
  qrCodeText?: string;
  boletoBarcode?: string;
  boletoPdfUrl?: string;
  expiresAt?: string;
  cardToken?: string;
};

export default function SandboxPage() {
  const [result, setResult] = useState<{ success: boolean; data: any } | null>(null);
  const [paymentResult, setPaymentResult] = useState<PaymentResult | null>(null);
  const { toast } = useToast();

  const handleSuccess = (paymentId: string, data: any) => {
    const method = data.method as PaymentMethod;
    
    if (method === 'pix') {
      const expiresAt = new Date();
      expiresAt.setHours(expiresAt.getHours() + 1);
      
      setPaymentResult({
        method: 'pix',
        paymentId,
        qrCode: `00020126580014br.gov.bcb.pix0136${paymentId}520400005303986540510.005802BR5913Test Merchant6009SAO PAULO62070503***6304${Math.random().toString(36).substr(2, 4).toUpperCase()}`,
        qrCodeText: `00020126580014br.gov.bcb.pix0136${paymentId}520400005303986540510.005802BR5913Test Merchant6009SAO PAULO62070503***6304${Math.random().toString(36).substr(2, 4).toUpperCase()}`,
        expiresAt: expiresAt.toISOString(),
      });
    } else if (method === 'boleto') {
      const expiresAt = new Date();
      expiresAt.setDate(expiresAt.getDate() + 3);
      
      setPaymentResult({
        method: 'boleto',
        paymentId,
        boletoBarcode: '34191.79001 01043.510047 91020.150008 1 84560000010000',
        boletoPdfUrl: `https://example.com/boleto/${paymentId}.pdf`,
        expiresAt: expiresAt.toISOString(),
      });
    }
    
    setResult({ success: true, data: { paymentId, ...data } });
    toast({
      title: 'Sucesso',
      description: 'Pagamento criado com sucesso!',
    });
  };

  const handleError = (error: Error) => {
    setResult({ success: false, data: { message: error.message } });
    toast({
      title: 'Erro',
      description: error.message,
      variant: 'destructive',
    });
  };

  const handleCopy = () => {
    toast({
      title: 'Copiado',
      description: 'Código copiado para a área de transferência',
    });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Ambiente de Testes</h1>
        <p className="text-muted-foreground mt-2">
          Teste integrações de pagamento sem transações reais
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="space-y-6">
          <PaymentForm onSuccess={handleSuccess} onError={handleError} />
          
          {result && (
            <Alert variant={result.success ? 'default' : 'destructive'}>
              <div className="flex items-start gap-2">
                {result.success ? (
                  <CheckCircle2 className="h-5 w-5 text-green-600" />
                ) : (
                  <XCircle className="h-5 w-5" />
                )}
                <div className="flex-1">
                  <AlertDescription>
                    <div className="font-semibold mb-2">
                      {result.success ? 'Pagamento criado com sucesso!' : 'Erro ao criar pagamento'}
                    </div>
                    <pre className="text-xs bg-muted p-2 rounded overflow-auto">
                      {JSON.stringify(result.data, null, 2)}
                    </pre>
                  </AlertDescription>
                </div>
              </div>
            </Alert>
          )}

          {paymentResult?.method === 'pix' && paymentResult.qrCode && paymentResult.qrCodeText && paymentResult.expiresAt && (
            <PIXQRCode
              qrCode={paymentResult.qrCode}
              qrCodeText={paymentResult.qrCodeText}
              expiresAt={paymentResult.expiresAt}
              onCopy={handleCopy}
            />
          )}

          {paymentResult?.method === 'boleto' && paymentResult.boletoBarcode && paymentResult.boletoPdfUrl && paymentResult.expiresAt && (
            <BoletoDisplay
              boletoBarcode={paymentResult.boletoBarcode}
              boletoPdfUrl={paymentResult.boletoPdfUrl}
              expiresAt={paymentResult.expiresAt}
              onCopy={handleCopy}
            />
          )}
        </div>

        <div>
          <TestCredentials />
        </div>
      </div>
    </div>
  );
}
