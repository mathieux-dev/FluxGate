export interface APIKey {
  id: string;
  merchantId: string;
  keyId: string;
  active: boolean;
  expiresAt?: string;
  lastUsedAt?: string;
  usageCount?: number;
  createdAt: string;
}

export interface CreateAPIKeyRequest {
  name?: string;
}

export interface CreateAPIKeyResponse {
  keyId: string;
  keySecret: string;
  expiresAt?: string;
}

export interface RotateAPIKeyRequest {
  keyId: string;
}

export interface RotateAPIKeyResponse {
  newKeyId: string;
  newKeySecret: string;
  oldKeyExpiresAt: string;
}

export interface RevokeAPIKeyRequest {
  keyId: string;
}
