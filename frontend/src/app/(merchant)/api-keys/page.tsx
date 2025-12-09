'use client';

import { useState } from 'react';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAPIKeys } from '@/lib/hooks/use-api-keys';
import { APIKeyList } from '@/components/merchant/api-key-list';
import { CreateAPIKeyDialog } from '@/components/merchant/create-api-key-dialog';
import { Skeleton } from '@/components/ui/skeleton';

export default function APIKeysPage() {
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const { data: apiKeys, isLoading } = useAPIKeys();

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">API Keys</h1>
          <p className="text-muted-foreground">
            Gerencie suas chaves de API para integração
          </p>
        </div>
        <Button onClick={() => setCreateDialogOpen(true)}>
          <Plus className="mr-2 h-4 w-4" />
          Nova API Key
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Chaves Ativas</CardTitle>
          <CardDescription>
            Lista de todas as suas chaves de API ativas e suas estatísticas de uso
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-4">
              <Skeleton className="h-20 w-full" />
              <Skeleton className="h-20 w-full" />
              <Skeleton className="h-20 w-full" />
            </div>
          ) : (
            <APIKeyList apiKeys={apiKeys || []} />
          )}
        </CardContent>
      </Card>

      <CreateAPIKeyDialog 
        open={createDialogOpen} 
        onOpenChange={setCreateDialogOpen} 
      />
    </div>
  );
}
