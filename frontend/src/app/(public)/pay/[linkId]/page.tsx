'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { AlertCircle, CheckCircle2 } from 'lucide-react';
import { PublicPaymentForm } from '@/components/payment/public-payment-form';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

interface PaymentLinkData {
  id: string;
  amount: number;
  description: string;
  status: 'active' | 'expired' | 'paid';
  expiresAt: string;
}

export default function PaymentLinkPage() {
  const params = useParams();
  const router = useRouter();
  const linkId = params.linkId as string;
  
  const [paymentLink, setPaymentLink] = useState<PaymentLinkData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [paymentSuccess, setPaymentSuccess] = useState(false);
  const [paymentId, setPaymentId] = useState<string | null>(null);

  useEffect(() => {
    const fetchPaymentLink = async () => {
      try {
        const response = await fetch(`${API_URL}/api/payment-links/${linkId}/public`);
        
        if (!response.ok) {
          if (response.status === 404) {
            setError('Link de pagamento não encontrado');
          } else {
            setError('Erro ao carregar link de pagamento');
          }
          return;
        }

        const data = await response.json();
        setPaymentLink(data);

        if (data.status === 'expired') {
          setError('Este link de pagamento expirou');
        } else if (data.status === 'paid') {
          setError('Este link de pagamento já foi utilizado');
        }
      } catch (err) {
        setError('Erro ao conectar com o servidor');
      } finally {
        setIsLoading(false);
      }
    };

    if (linkId) {
      fetchPaymentLink();
    }
  }, [linkId]);

  const handlePaymentSuccess = (id: string) => {
    setPaymentId(id);
    setPaymentSuccess(true);
  };

  const handlePaymentError = (error: Error) => {
    setError(error.message);
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-palmeiras-green to-palmeiras-green-light flex items-center justify-center p-4">
        <Card className="w-full max-w-2xl">
          <CardHeader>
            <Skeleton className="h-8 w-3/4" />
          </CardHeader>
          <CardContent className="space-y-4">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-2/3" />
            <Skeleton className="h-32 w-full" />
          </CardContent>
        </Card>
      </div>
    );
  }

  if (paymentSuccess) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-palmeiras-green to-palmeiras-green-light flex items-center justify-center p-4">
        <Card className="w-full max-w-2xl">
          <CardHeader>
            <div className="flex items-center justify-center mb-4">
              <div className="rounded-full bg-green-100 p-3">
                <CheckCircle2 className="h-12 w-12 text-green-600" />
              </div>
            </div>
            <CardTitle className="text-center text-2xl">Pagamento Realizado com Sucesso!</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="text-center space-y-2">
              <p className="text-muted-foreground">
                Seu pagamento foi processado com sucesso.
              </p>
              {paymentId && (
                <p className="text-sm text-muted-foreground">
                  ID do Pagamento: <span className="font-mono">{paymentId}</span>
                </p>
              )}
            </div>
            
            <Alert className="bg-green-50 border-green-200">
              <CheckCircle2 className="h-4 w-4 text-green-600" />
              <AlertDescription className="text-green-800">
                Você receberá uma confirmação por email em breve.
              </AlertDescription>
            </Alert>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !paymentLink) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-palmeiras-green to-palmeiras-green-light flex items-center justify-center p-4">
        <Card className="w-full max-w-2xl">
          <CardHeader>
            <div className="flex items-center justify-center mb-4">
              <div className="rounded-full bg-red-100 p-3">
                <AlertCircle className="h-12 w-12 text-red-600" />
              </div>
            </div>
            <CardTitle className="text-center text-2xl">Erro</CardTitle>
          </CardHeader>
          <CardContent>
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{error || 'Link de pagamento inválido'}</AlertDescription>
            </Alert>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-palmeiras-green to-palmeiras-green-light flex items-center justify-center p-4">
      <div className="w-full max-w-2xl">
        <Card className="mb-6">
          <CardHeader>
            <CardTitle className="text-2xl">Finalizar Pagamento</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-muted-foreground">Descrição:</span>
                <span className="font-medium">{paymentLink.description}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-muted-foreground">Valor:</span>
                <span className="text-2xl font-bold text-palmeiras-green">
                  R$ {(paymentLink.amount / 100).toFixed(2)}
                </span>
              </div>
            </div>
          </CardContent>
        </Card>

        <PublicPaymentForm
          linkId={linkId}
          amount={paymentLink.amount}
          description={paymentLink.description}
          onSuccess={handlePaymentSuccess}
          onError={handlePaymentError}
        />
      </div>
    </div>
  );
}
