'use client';

import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { useExportTransactions } from '@/lib/hooks/use-transactions';
import { useToast } from '@/hooks/use-toast';
import { TransactionFilters } from '@/types/transaction';
import { Loader2, FileDown, FileSpreadsheet, FileJson } from 'lucide-react';

interface ExportDialogProps {
  filters: TransactionFilters;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ExportDialog({ filters, open, onOpenChange }: ExportDialogProps) {
  const [format, setFormat] = useState<'csv' | 'excel' | 'json'>('csv');
  const { toast } = useToast();
  const { mutate: exportTransactions, isPending } = useExportTransactions();

  const handleExport = () => {
    exportTransactions(
      { format, filters },
      {
        onSuccess: () => {
          toast({
            title: 'Exportação concluída',
            description: 'O arquivo foi baixado com sucesso',
          });
          onOpenChange(false);
        },
        onError: (error) => {
          toast({
            title: 'Erro ao exportar',
            description: error.message,
            variant: 'destructive',
          });
        },
      }
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Exportar Transações</DialogTitle>
          <DialogDescription>
            Escolha o formato de exportação dos dados
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-3">
            <div
              className={`flex items-center space-x-3 p-4 border rounded-lg cursor-pointer transition-colors ${
                format === 'csv'
                  ? 'border-palmeiras-green bg-palmeiras-green/5'
                  : 'hover:bg-muted/50'
              }`}
              onClick={() => setFormat('csv')}
            >
              <input
                type="radio"
                id="csv"
                name="format"
                value="csv"
                checked={format === 'csv'}
                onChange={() => setFormat('csv')}
                className="h-4 w-4 text-palmeiras-green"
              />
              <FileDown className="h-5 w-5 text-muted-foreground" />
              <div className="flex-1">
                <Label htmlFor="csv" className="cursor-pointer font-medium">
                  CSV
                </Label>
                <p className="text-xs text-muted-foreground">
                  Formato compatível com Excel e Google Sheets
                </p>
              </div>
            </div>

            <div
              className={`flex items-center space-x-3 p-4 border rounded-lg cursor-pointer transition-colors ${
                format === 'excel'
                  ? 'border-palmeiras-green bg-palmeiras-green/5'
                  : 'hover:bg-muted/50'
              }`}
              onClick={() => setFormat('excel')}
            >
              <input
                type="radio"
                id="excel"
                name="format"
                value="excel"
                checked={format === 'excel'}
                onChange={() => setFormat('excel')}
                className="h-4 w-4 text-palmeiras-green"
              />
              <FileSpreadsheet className="h-5 w-5 text-muted-foreground" />
              <div className="flex-1">
                <Label htmlFor="excel" className="cursor-pointer font-medium">
                  Excel (XLSX)
                </Label>
                <p className="text-xs text-muted-foreground">
                  Formato nativo do Microsoft Excel
                </p>
              </div>
            </div>

            <div
              className={`flex items-center space-x-3 p-4 border rounded-lg cursor-pointer transition-colors ${
                format === 'json'
                  ? 'border-palmeiras-green bg-palmeiras-green/5'
                  : 'hover:bg-muted/50'
              }`}
              onClick={() => setFormat('json')}
            >
              <input
                type="radio"
                id="json"
                name="format"
                value="json"
                checked={format === 'json'}
                onChange={() => setFormat('json')}
                className="h-4 w-4 text-palmeiras-green"
              />
              <FileJson className="h-5 w-5 text-muted-foreground" />
              <div className="flex-1">
                <Label htmlFor="json" className="cursor-pointer font-medium">
                  JSON
                </Label>
                <p className="text-xs text-muted-foreground">
                  Formato para processamento programático
                </p>
              </div>
            </div>
          </div>

          <div className="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-4">
            <p className="text-sm text-blue-800 dark:text-blue-200">
              Os dados exportados incluirão todas as transações que correspondem aos filtros
              aplicados atualmente.
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isPending}
          >
            Cancelar
          </Button>
          <Button
            onClick={handleExport}
            disabled={isPending}
            className="bg-palmeiras-green hover:bg-palmeiras-green-light"
          >
            {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Exportar
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
