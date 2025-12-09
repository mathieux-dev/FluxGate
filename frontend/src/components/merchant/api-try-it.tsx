'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Loader2, Play } from 'lucide-react';
import { useToast } from '@/hooks/use-toast';

interface TryItProps {
  endpoint: {
    method: string;
    path: string;
    requestBody?: Record<string, any>;
  };
  apiKey: string;
}

export function APITryIt({ endpoint, apiKey }: TryItProps) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);
  const [pathParams, setPathParams] = useState('');
  const [requestBody, setRequestBody] = useState(
    endpoint.requestBody ? JSON.stringify(endpoint.requestBody, null, 2) : ''
  );
  const [response, setResponse] = useState<{
    status: number;
    statusText: string;
    headers: Record<string, string>;
    body: any;
  } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleTryIt = async () => {
    setLoading(true);
    setError(null);
    setResponse(null);

    try {
      // Replace path params
      let finalPath = endpoint.path;
      if (pathParams && endpoint.path.includes(':')) {
        const paramName = endpoint.path.match(/:(\w+)/)?.[1];
        if (paramName) {
          finalPath = endpoint.path.replace(`:${paramName}`, pathParams);
        }
      }

      // Parse request body
      let body = null;
      if (requestBody && endpoint.method !== 'GET') {
        try {
          body = JSON.parse(requestBody);
        } catch (e) {
          throw new Error('Invalid JSON in request body');
        }
      }

      // Generate HMAC signature
      const timestamp = Date.now().toString();
      const nonce = Math.random().toString(36).substring(2, 15);
      
      // Note: In a real implementation, the API secret should never be exposed to the frontend
      // This is a simplified version for demonstration
      const bodyHash = body 
        ? await crypto.subtle.digest('SHA-256', new TextEncoder().encode(JSON.stringify(body)))
            .then(buf => Array.from(new Uint8Array(buf))
              .map(b => b.toString(16).padStart(2, '0'))
              .join(''))
        : '';
      
      const message = `${timestamp}.${nonce}.${endpoint.method}.${finalPath}.${bodyHash}`;
      
      // For demo purposes, we'll make the request without proper HMAC
      // In production, this should go through a backend proxy
      const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';
      
      const res = await fetch(`${apiUrl}${finalPath}`, {
        method: endpoint.method,
        headers: {
          'Content-Type': 'application/json',
          'X-Api-Key': apiKey,
          'X-Timestamp': timestamp,
          'X-Nonce': nonce,
          // Note: X-Signature would be added here in production
        },
        credentials: 'include',
        body: body ? JSON.stringify(body) : undefined,
      });

      const responseHeaders: Record<string, string> = {};
      res.headers.forEach((value, key) => {
        responseHeaders[key] = value;
      });

      let responseBody;
      const contentType = res.headers.get('content-type');
      if (contentType?.includes('application/json')) {
        responseBody = await res.json();
      } else {
        responseBody = await res.text();
      }

      setResponse({
        status: res.status,
        statusText: res.statusText,
        headers: responseHeaders,
        body: responseBody,
      });

      if (res.ok) {
        toast({
          title: 'Sucesso!',
          description: 'Requisição executada com sucesso',
        });
      } else {
        toast({
          title: 'Erro',
          description: `${res.status} ${res.statusText}`,
          variant: 'destructive',
        });
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Erro desconhecido';
      setError(errorMessage);
      toast({
        title: 'Erro',
        description: errorMessage,
        variant: 'destructive',
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <Alert>
        <AlertDescription>
          <strong>Nota:</strong> Esta funcionalidade faz requisições reais à API. 
          Use com cuidado em ambiente de produção.
        </AlertDescription>
      </Alert>

      {endpoint.path.includes(':') && (
        <div>
          <Label>Path Parameter</Label>
          <Input
            placeholder="Ex: pay_123"
            value={pathParams}
            onChange={(e) => setPathParams(e.target.value)}
          />
          <p className="text-xs text-muted-foreground mt-1">
            Substitui {endpoint.path.match(/:(\w+)/)?.[0]} no path
          </p>
        </div>
      )}

      {endpoint.requestBody && endpoint.method !== 'GET' && (
        <div>
          <Label>Request Body (JSON)</Label>
          <Textarea
            value={requestBody}
            onChange={(e) => setRequestBody(e.target.value)}
            rows={10}
            className="font-mono text-xs"
          />
        </div>
      )}

      <Button
        onClick={handleTryIt}
        disabled={loading}
        className="w-full"
      >
        {loading ? (
          <>
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Executando...
          </>
        ) : (
          <>
            <Play className="mr-2 h-4 w-4" />
            Testar Requisição
          </>
        )}
      </Button>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {response && (
        <div className="space-y-4">
          <div>
            <Label>Status</Label>
            <div className="flex items-center gap-2 mt-1">
              <span
                className={`px-2 py-1 rounded text-sm font-mono ${
                  response.status >= 200 && response.status < 300
                    ? 'bg-green-100 text-green-800'
                    : response.status >= 400
                    ? 'bg-red-100 text-red-800'
                    : 'bg-yellow-100 text-yellow-800'
                }`}
              >
                {response.status} {response.statusText}
              </span>
            </div>
          </div>

          <div>
            <Label>Response Headers</Label>
            <pre className="bg-muted p-3 rounded text-xs overflow-auto mt-1">
              {JSON.stringify(response.headers, null, 2)}
            </pre>
          </div>

          <div>
            <Label>Response Body</Label>
            <pre className="bg-muted p-3 rounded text-xs overflow-auto mt-1">
              {typeof response.body === 'string'
                ? response.body
                : JSON.stringify(response.body, null, 2)}
            </pre>
          </div>
        </div>
      )}
    </div>
  );
}
