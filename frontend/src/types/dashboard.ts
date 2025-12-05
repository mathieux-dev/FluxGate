export interface DashboardStats {
  totalTransactions: number;
  totalVolume: number;
  successRate: number;
  pendingPayments: number;
}

export interface ChartData {
  date: string;
  amount: number;
  count: number;
}

export interface RecentTransaction {
  id: string;
  amount: number;
  status: 'pending' | 'authorized' | 'paid' | 'refunded' | 'failed' | 'expired' | 'cancelled';
  method: 'card' | 'pix' | 'boleto';
  customerEmail: string;
  createdAt: string;
}
