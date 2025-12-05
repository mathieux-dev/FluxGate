'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { TransactionList } from '@/components/merchant/transaction-list';
import { TransactionPagination } from '@/components/merchant/transaction-pagination';
import { TransactionFilters as TransactionFiltersComponent } from '@/components/merchant/transaction-filters';
import { ExportDialog } from '@/components/merchant/export-dialog';
import { useTransactions } from '@/lib/hooks/use-transactions';
import { TransactionFilters } from '@/types/transaction';
import { Button } from '@/components/ui/button';
import { Download } from 'lucide-react';

export default function TransactionsPage() {
  const router = useRouter();
  const [filters, setFilters] = useState<TransactionFilters>({
    page: 1,
    limit: 50,
  });
  const [exportDialogOpen, setExportDialogOpen] = useState(false);

  const { data, isLoading } = useTransactions(filters);

  const handlePageChange = (page: number) => {
    setFilters((prev) => ({ ...prev, page }));
  };

  const handleViewDetails = (id: string) => {
    router.push(`/transactions/${id}`);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Transações</h1>
          <p className="text-muted-foreground">
            Visualize e gerencie todas as suas transações
          </p>
        </div>
        <Button
          onClick={() => setExportDialogOpen(true)}
          className="bg-palmeiras-green hover:bg-palmeiras-green-light"
        >
          <Download className="mr-2 h-4 w-4" />
          Exportar
        </Button>
      </div>

      <TransactionFiltersComponent filters={filters} onChange={setFilters} />

      <TransactionList
        transactions={data?.transactions || []}
        onViewDetails={handleViewDetails}
        isLoading={isLoading}
      />

      {data && data.totalPages > 1 && (
        <TransactionPagination
          currentPage={data.page}
          totalPages={data.totalPages}
          onPageChange={handlePageChange}
        />
      )}

      <ExportDialog
        filters={filters}
        open={exportDialogOpen}
        onOpenChange={setExportDialogOpen}
      />
    </div>
  );
}
