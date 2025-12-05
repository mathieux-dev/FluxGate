import { useQuery } from '@tanstack/react-query';
import { APIClient } from '@/lib/api/client';
import { DashboardStats, ChartData, RecentTransaction } from '@/types/dashboard';
import { useAuthStore } from '@/stores/auth-store';

const apiClient = new APIClient({
  baseURL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000',
});

export function useDashboardStats() {
  const accessToken = useAuthStore((state) => state.accessToken);

  return useQuery<DashboardStats>({
    queryKey: ['dashboard', 'stats'],
    queryFn: async () => {
      if (accessToken) {
        apiClient.setAccessToken(accessToken);
      }
      return apiClient.get<DashboardStats>('/dashboard/stats');
    },
    enabled: !!accessToken,
    refetchInterval: 30000,
  });
}

export function useDashboardChart() {
  const accessToken = useAuthStore((state) => state.accessToken);

  return useQuery<ChartData[]>({
    queryKey: ['dashboard', 'chart'],
    queryFn: async () => {
      if (accessToken) {
        apiClient.setAccessToken(accessToken);
      }
      return apiClient.get<ChartData[]>('/dashboard/chart');
    },
    enabled: !!accessToken,
    refetchInterval: 30000,
  });
}

export function useRecentTransactions() {
  const accessToken = useAuthStore((state) => state.accessToken);

  return useQuery<RecentTransaction[]>({
    queryKey: ['dashboard', 'recent-transactions'],
    queryFn: async () => {
      if (accessToken) {
        apiClient.setAccessToken(accessToken);
      }
      return apiClient.get<RecentTransaction[]>('/dashboard/recent-transactions');
    },
    enabled: !!accessToken,
    refetchInterval: 30000,
  });
}
