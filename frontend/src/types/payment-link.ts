export type PaymentLinkStatus = 'active' | 'expired' | 'paid';

export interface PaymentLink {
  id: string;
  merchantId: string;
  amount: number;
  description: string;
  status: PaymentLinkStatus;
  url: string;
  qrCode: string;
  expiresAt: string;
  paymentCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePaymentLinkRequest {
  amount: number;
  description: string;
  expiresAt: string;
}

export interface PaymentLinkListResponse {
  paymentLinks: PaymentLink[];
  total: number;
  page: number;
  limit: number;
  totalPages: number;
}
