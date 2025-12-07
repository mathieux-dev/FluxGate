'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { 
  WebhookConfig, 
  WebhookDelivery, 
  CreateWebhookConfigRequest,
  CreateWebhookConfigResponse,
  UpdateWebhookConfigRequest,
  WebhookTestRequest,
  WebhookTestResponse
} from '@/types/webhook';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

export function useWebhookConfig() {
  return useQuery<WebhookConfig>({
    queryKey: ['webhook-config'],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/api/webhooks/config`, {
        credentials: 'include',
      });
      if (!res.ok) throw new Error('Failed to fetch webhook config');
      return res.json();
    },
  });
}

export function useCreateWebhookConfig() {
  const queryClient = useQueryClient();
  
  return useMutation<CreateWebhookConfigResponse, Error, CreateWebhookConfigRequest>({
    mutationFn: async (data) => {
      const res = await fetch(`${API_URL}/api/webhooks/config`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error('Failed to create webhook config');
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['webhook-config'] });
    },
  });
}

export function useUpdateWebhookConfig() {
  const queryClient = useQueryClient();
  
  return useMutation<WebhookConfig, Error, UpdateWebhookConfigRequest>({
    mutationFn: async (data) => {
      const res = await fetch(`${API_URL}/api/webhooks/config`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error('Failed to update webhook config');
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['webhook-config'] });
    },
  });
}

export function useGenerateWebhookSecret() {
  const queryClient = useQueryClient();
  
  return useMutation<{ secret: string }, Error>({
    mutationFn: async () => {
      const res = await fetch(`${API_URL}/api/webhooks/config/regenerate-secret`, {
        method: 'POST',
        credentials: 'include',
      });
      if (!res.ok) throw new Error('Failed to generate webhook secret');
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['webhook-config'] });
    },
  });
}

export function useTestWebhook() {
  return useMutation<WebhookTestResponse, Error, WebhookTestRequest | undefined>({
    mutationFn: async (data = {}) => {
      const res = await fetch(`${API_URL}/api/webhooks/test`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error('Failed to test webhook');
      return res.json();
    },
  });
}

export function useWebhookDeliveries() {
  return useQuery<WebhookDelivery[]>({
    queryKey: ['webhook-deliveries'],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/api/webhooks/deliveries`, {
        credentials: 'include',
      });
      if (!res.ok) throw new Error('Failed to fetch webhook deliveries');
      return res.json();
    },
    refetchInterval: 10000,
  });
}
