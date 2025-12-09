import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { PaymentLink, PaymentLinkListResponse, CreatePaymentLinkRequest } from '@/types/payment-link';
import { useAuthStore } from '@/stores/auth-store';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

export function usePaymentLinks(page = 1, limit = 50) {
  const accessToken = useAuthStore((state) => state.accessToken);

  return useQuery<PaymentLinkListResponse>({
    queryKey: ['payment-links', page, limit],
    queryFn: async () => {
      const params = new URLSearchParams();
      params.append('page', String(page));
      params.append('limit', String(limit));

      const response = await fetch(`${API_URL}/api/payment-links?${params}`, {
        headers: {
          'Authorization': `Bearer ${accessToken}`,
        },
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error('Failed to fetch payment links');
      }

      return response.json();
    },
    enabled: !!accessToken,
  });
}

export function usePaymentLink(id: string) {
  const accessToken = useAuthStore((state) => state.accessToken);

  return useQuery<PaymentLink>({
    queryKey: ['payment-link', id],
    queryFn: async () => {
      const response = await fetch(`${API_URL}/api/payment-links/${id}`, {
        headers: {
          'Authorization': `Bearer ${accessToken}`,
        },
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error('Failed to fetch payment link');
      }

      return response.json();
    },
    enabled: !!accessToken && !!id,
  });
}

export function useCreatePaymentLink() {
  const queryClient = useQueryClient();
  const accessToken = useAuthStore((state) => state.accessToken);

  return useMutation({
    mutationFn: async (data: CreatePaymentLinkRequest) => {
      const response = await fetch(`${API_URL}/api/payment-links`, {
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
        throw new Error(error.message || 'Failed to create payment link');
      }

      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payment-links'] });
    },
  });
}
