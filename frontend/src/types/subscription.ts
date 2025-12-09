export type SubscriptionStatus = 'active' | 'cancelled' | 'past_due';
export type SubscriptionInterval = 'daily' | 'weekly' | 'monthly' | 'yearly';

export interface Subscription {
  id: string;
  merchantId: string;
  customerId: string;
  customerName: string;
  customerEmail: string;
  providerSubscriptionId: string;
  status: SubscriptionStatus;
  amount: number;
  interval: SubscriptionInterval;
  nextBillingDate: string;
  createdAt: string;
  cancelledAt?: string;
}

export interface SubscriptionCharge {
  id: string;
  subscriptionId: string;
  amount: number;
  status: 'pending' | 'paid' | 'failed';
  billingDate: string;
  paidAt?: string;
  failureReason?: string;
}

export interface SubscriptionDetails extends Subscription {
  charges: SubscriptionCharge[];
}

export interface CreateSubscriptionRequest {
  customerId: string;
  customerName: string;
  customerEmail: string;
  amount: number;
  interval: SubscriptionInterval;
  cardToken: string;
}

export interface SubscriptionListResponse {
  subscriptions: Subscription[];
  total: number;
  page: number;
  limit: number;
  totalPages: number;
}
