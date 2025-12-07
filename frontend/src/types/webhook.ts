export interface WebhookConfig {
  id: string;
  merchantId: string;
  url: string;
  secret: string;
  active: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface WebhookDelivery {
  id: string;
  webhookId: string;
  paymentId: string;
  event: string;
  status: 'pending' | 'success' | 'failed';
  retryCount: number;
  lastAttemptAt: string;
  nextRetryAt?: string;
  responseCode?: number;
  responseBody?: string;
  createdAt: string;
}

export interface CreateWebhookConfigRequest {
  url: string;
}

export interface CreateWebhookConfigResponse {
  id: string;
  url: string;
  secret: string;
}

export interface UpdateWebhookConfigRequest {
  url: string;
}

export interface WebhookTestRequest {
  event?: string;
}

export interface WebhookTestResponse {
  success: boolean;
  statusCode: number;
  payload: Record<string, unknown>;
  response: Record<string, unknown>;
  headers: Record<string, string>;
}
