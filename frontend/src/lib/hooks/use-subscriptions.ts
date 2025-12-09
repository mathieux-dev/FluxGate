import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { 
  Subscription, 
  SubscriptionDetails, 
  SubscriptionListResponse,
  CreateSubscriptionRequest 
} from '@/types/subscription';
import { useAuthStore } from '@/stores/auth-store';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

export function useSubscriptions(page = 1, limit = 50) {
  const accessToken = useAuthStore((state) => state.accessToken);

  return useQuery<SubscriptionListResponse>({
    queryKey: ['subscriptions', page, limit],
    queryFn: async () => {
      const params = new URLSearchParams({
        page: String(page),
        limit: String(limit),
      });

      const response = await fetch(`${API_URL}/api/subscriptions?${params}`, {
        headers: {
          'Authorization': `Bearer ${accessToken}`,
        },
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error('Failed to fetch subscriptions');
      }

      return response.json();
    },
    enabled: !!accessToken,
  });
}

export function useSubscription(id: string) {
  const accessToken = useAuthStore((state) => state.accessToken);

  return useQuery<SubscriptionDetails>({
    queryKey: ['subscription', id],
    queryFn: async () => {
      const response = await fetch(`${API_URL}/api/subscriptions/${id}`, {
        headers: {
          'Authorization': `Bearer ${accessToken}`,
        },
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error('Failed to fetch subscription');
      }

      return response.json();
    },
    enabled: !!accessToken && !!id,
  });
}

export function useCreateSubscription() {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);

  return useMutation({
    mutationFn: async (data: CreateSubscriptionRequest) => {
      const response = await fetch(`${API_URL}/api/subscriptions`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${accessToken}`,
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to create subscription');
      }

      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['subscriptions'] });
    },
  });
}

export function useCancelSubscription() {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);

  return useMutation({
    mutationFn: async (id: string) => {
      const response = await fetch(`${API_URL}/api/subscriptions/${id}/cancel`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${accessToken}`,
        },
        credentials: 'include',
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to cancel subscription');
      }

      return response.json();
    },
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: ['subscription', id] });
      queryClient.invalidateQueries({ queryKey: ['subscriptions'] });
    },
  });
}
