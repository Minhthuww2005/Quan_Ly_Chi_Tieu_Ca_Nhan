/**
 * API Service for HTD PAY
 * Centralized API calls to backend
 */

const API_BASE_URL = '/api';

// Helper function for API calls
async function apiCall<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const token = localStorage.getItem('token');
  
  const config: RequestInit = {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token && { Authorization: `Bearer ${token}` }),
      ...options.headers,
    },
  };

  const response = await fetch(`${API_BASE_URL}${endpoint}`, config);
  
  if (!response.ok) {
    const error = await response.json().catch(() => ({
      message: 'Đã xảy ra lỗi'
    }));
    throw new Error(error.message || `HTTP ${response.status}`);
  }

  return response.json();
}

// ==================== AUTH API ====================

export interface LoginRequest {
  identifier: string;
  password: string;
}

export interface LoginResponse {
  success: boolean;
  message: string;
  token: string;
  user: {
    user_id: number;
    name: string;
    email: string;
    phone: string;
    avatar?: string;
  };
}

export interface RegisterRequest {
  name: string;
  email: string;
  phone: string;
  password: string;
}

export interface RegisterResponse {
  success: boolean;
  message: string;
  userId: number;
}

export interface GoogleLoginRequest {
  credential: string; // Google ID token
}

export interface GoogleLoginResponse {
  success: boolean;
  message: string;
  token: string;
  user: {
    user_id: number;
    name: string;
    email: string;
    phone?: string;
    avatar?: string;
    authProvider?: string;
  };
}

export const authAPI = {
  login: (data: LoginRequest) =>
    apiCall<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  register: (data: RegisterRequest) =>
    apiCall<RegisterResponse>('/auth/register', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  forgotPassword: (email: string) =>
    apiCall<{ success: boolean; message: string; resetToken?: string }>('/auth/forgot-password', {
      method: 'POST',
      body: JSON.stringify({ email }),
    }),

  resetPassword: (token: string, newPassword: string) =>
    apiCall<{ success: boolean; message: string }>('/auth/reset-password', {
      method: 'POST',
      body: JSON.stringify({ token, newPassword }),
    }),

  changePassword: (userId: number, oldPassword: string, newPassword: string) =>
    apiCall<{ success: boolean; message: string }>('/auth/change-password', {
      method: 'POST',
      body: JSON.stringify({ userId, oldPassword, newPassword }),
    }),

  // Set/Change PIN
  setPin: (userId: number, newPin: string, oldPin?: string) =>
    apiCall<{ success: boolean; message: string }>('/auth/set-pin', {
      method: 'POST',
      body: JSON.stringify({ 
        userId: userId, 
        pinCode: newPin,
        ...(oldPin && { oldPinCode: oldPin })
      }),
    }),

  // Google Sign-In
  googleLogin: (data: GoogleLoginRequest) =>
    apiCall<GoogleLoginResponse>('/auth/google', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  socialLogin: (provider: string, accessToken: string) =>
    apiCall<LoginResponse>('/auth/social', {
      method: 'POST',
      body: JSON.stringify({ provider, accessToken }),
    }),
};

// ==================== WALLET API ====================

export const walletAPI = {
  getWallet: (userId: number) =>
    apiCall<{ success: boolean; wallet: any }>(`/wallet/${userId}`),
};

// ==================== TRANSACTION API ====================

export interface TransferRequest {
  user_id: number;
  recipient_id: number;
  amount: number;
  description?: string;
}

export interface TopupRequest {
  user_id: number;
  card_id: number;
  amount: number;
}

export interface BillPaymentRequest {
  user_id: number;
  biller_id: number;
  amount: number;
  account_number?: string;
}

export const transactionAPI = {
  getTransactions: (userId: number, limit = 50, skip = 0) =>
    apiCall<{ success: boolean; transactions: any[]; total: number }>(
      `/transactions/${userId}?limit=${limit}&skip=${skip}`
    ),

  transfer: (data: TransferRequest) =>
    apiCall<{ success: boolean; message: string; transaction: any }>(
      '/transactions/transfer',
      {
        method: 'POST',
        body: JSON.stringify(data),
      }
    ),

  topup: (data: TopupRequest) =>
    apiCall<{ success: boolean; message: string; transaction: any }>(
      '/transactions/topup',
      {
        method: 'POST',
        body: JSON.stringify(data),
      }
    ),

  payBill: (data: BillPaymentRequest) =>
    apiCall<{ success: boolean; message: string; transaction: any }>(
      '/transactions/bill',
      {
        method: 'POST',
        body: JSON.stringify(data),
      }
    ),
};

// ==================== CARD API ====================

export const cardAPI = {
  getCards: (userId: number) =>
    apiCall<{ success: boolean; cards: any[] }>(`/cards/${userId}`),
};

// ==================== MERCHANT & BILLER API ====================

export const merchantAPI = {
  getMerchants: () =>
    apiCall<{ success: boolean; merchants: any[] }>('/merchants'),

  getBillers: () =>
    apiCall<{ success: boolean; billers: any[] }>('/merchants/billers'),
};

// ==================== NOTIFICATION API ====================

export const notificationAPI = {
  getNotifications: (userId: number) =>
    apiCall<{ success: boolean; notifications: any[] }>(
      `/notifications/${userId}`
    ),

  markAsRead: (notificationId: number) =>
    apiCall<{ success: boolean; message: string }>(
      `/notifications/${notificationId}/read`,
      {
        method: 'PATCH',
      }
    ),
};

// ==================== CONTACT API ====================

export const contactAPI = {
  getContacts: (userId: number) =>
    apiCall<{ success: boolean; contacts: any[] }>(`/contacts/${userId}`),
};

// ==================== FINANCIAL PRODUCTS API ====================

export const financialAPI = {
  getSavings: (userId: number) =>
    apiCall<{ success: boolean; savings: any[] }>(`/savings/${userId}`),

  getLoans: (userId: number) =>
    apiCall<{ success: boolean; loans: any[] }>(`/loans/${userId}`),

  getInsurance: (userId: number) =>
    apiCall<{ success: boolean; insurance: any[] }>(`/insurance/${userId}`),

  getInvestments: (userId: number) =>
    apiCall<{ success: boolean; investments: any[] }>(`/investments/${userId}`),
};

// ==================== TRANSFER API ====================

export interface ExecuteTransferRequest {
  senderId: number;
  receiverPhone: string;
  amount: number;
  pinCode: string;
  description?: string;
}

export const transferAPI = {
  execute: (data: ExecuteTransferRequest) =>
    apiCall<{ success: boolean; message: string; data: any }>('/transfer/execute', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  getHistory: (userId: number, limit = 50, page = 1) =>
    apiCall<{ success: boolean; data: any }>(`/transfer/history/${userId}?limit=${limit}&page=${page}`),

  getDailySummary: (userId: number) =>
    apiCall<{ success: boolean; data: any }>(`/transfer/daily-summary/${userId}`),
};

// ==================== BANK & TOP-UP API ====================

export interface LinkBankRequest {
  user_id: number;
  gateway_transaction_id: string;
  bank_name?: string;
  account_number?: string;
  account_holder?: string;
}

export interface TopUpRequest {
  user_id: number;
  linked_bank_id: number;
  amount: number;
  pin_code: string;
  description?: string;
}

export const bankAPI = {
  // Link bank account
  linkBank: (data: LinkBankRequest) =>
    apiCall<{ success: boolean; message: string; data: any }>('/bank/link', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  // Top-up wallet from linked bank
  topUp: (data: TopUpRequest) =>
    apiCall<{ success: boolean; message: string; data: any }>('/bank/topup', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  // Get all linked banks
  getLinkedBanks: (userId: number) =>
    apiCall<{ success: boolean; data: { banks: any[] } }>(`/bank/linked/${userId}`),

  // Unlink bank account
  unlinkBank: (bankId: number, userId: number) =>
    apiCall<{ success: boolean; message: string }>(`/bank/unlink/${bankId}`, {
      method: 'DELETE',
      body: JSON.stringify({ user_id: userId }),
    }),

  // Set default bank
  setDefaultBank: (userId: number, bankId: number) =>
    apiCall<{ success: boolean; message: string; data: any }>('/bank/set-default', {
      method: 'PUT',
      body: JSON.stringify({ user_id: userId, bank_id: bankId }),
    }),
};

// ==================== SUPPORTED BANKS API ====================

export interface SupportedBankData {
  bank_code: string;
  bank_name: string;
  bank_full_name: string;
  logo_url?: string;
  is_active: boolean;
  display_order: number;
  gateway_support: {
    vnpay: boolean;
    momo: boolean;
    zalopay: boolean;
  };
}

export const supportedBankAPI = {
  // Get all active supported banks
  getSupportedBanks: () =>
    apiCall<{ success: boolean; data: SupportedBankData[]; total: number }>('/banks/supported'),

  // Get all banks (admin - including inactive)
  getAllBanks: () =>
    apiCall<{ success: boolean; data: SupportedBankData[]; total: number }>('/banks/supported/all'),

  // Add new bank (admin)
  addBank: (data: Partial<SupportedBankData>) =>
    apiCall<{ success: boolean; message: string; data: SupportedBankData }>('/banks/supported', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  // Update bank (admin)
  updateBank: (bankCode: string, data: Partial<SupportedBankData>) =>
    apiCall<{ success: boolean; message: string; data: SupportedBankData }>(`/banks/supported/${bankCode}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  // Toggle bank status (admin)
  toggleBankStatus: (bankCode: string) =>
    apiCall<{ success: boolean; message: string; data: any }>(`/banks/supported/${bankCode}/toggle`, {
      method: 'PATCH',
    }),

  // Delete bank (admin)
  deleteBank: (bankCode: string) =>
    apiCall<{ success: boolean; message: string }>(`/banks/supported/${bankCode}`, {
      method: 'DELETE',
    }),
};

// ==================== BANK ACCOUNT API ====================

export interface BankAccountVerifyRequest {
  bank_code: string;
  account_number: string;
}

export interface BankAccountData {
  account_id: number;
  account_number: string;
  account_name: string;
  bank_code: string;
  bank_name: string;
  branch: string;
  balance: number;
  status: string;
}

export const bankAccountAPI = {
  // Verify if account exists
  verifyAccount: (data: BankAccountVerifyRequest) =>
    apiCall<{ success: boolean; data: BankAccountData; message: string }>(
      '/bank-accounts/verify',
      {
        method: 'POST',
        body: JSON.stringify(data),
      }
    ),

  // Search accounts
  searchAccounts: (query: string) =>
    apiCall<{ success: boolean; data: BankAccountData[] }>(
      `/bank-accounts/search?q=${encodeURIComponent(query)}`
    ),

  // Get all accounts
  getAllAccounts: () =>
    apiCall<{ success: boolean; accounts: BankAccountData[] }>(
      '/bank-accounts'
    ),
};

// Export all APIs
export const API = {
  auth: authAPI,
  wallet: walletAPI,
  transaction: transactionAPI,
  card: cardAPI,
  merchant: merchantAPI,
  notification: notificationAPI,
  contact: contactAPI,
  financial: financialAPI,
  transfer: transferAPI,
  bank: bankAPI,
  supportedBank: supportedBankAPI,
  bankAccount: bankAccountAPI,
};

export default API;
