import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface DashboardStats {
  totalUsers: number;
  totalCredits: number;
  analysesPerformed: number;
  estimatedRevenue: number;
  activeUsers: number;
}

export interface UsageData {
  date?: string;
  month?: string;
  registrations: number;
  analyses: number;
  revenue: number;
}

export interface SalesListItem {
  id: string;
  userId?: string;
  userName?: string;
  userEmail?: string;
  planId?: string;
  planName?: string;
  creditsAmount: number;
  price?: number;
  currency?: string;
  status: string;
  paymentMethod?: string;
  paymentId?: string;
  createdAt?: string;
  updatedAt?: string;
}

export type PaymentProvider = 'stripe' | 'mercadopago' | 'cakto' | 'kiwify';
export type MercadoPagoMode = 'test' | 'production';

export interface PaymentProviderSetting {
  provider: PaymentProvider;
  providers: PaymentProvider[];
  labels?: Record<PaymentProvider, string>;
  mercadoPagoMode?: MercadoPagoMode;
  mercadoPagoModes?: MercadoPagoMode[];
  mercadoPagoModeLabels?: Record<MercadoPagoMode, string>;
  mercadoPagoProductionHint?: string;
}

export interface PaymentConnectionTestResult {
  success: boolean;
  connected: boolean;
  provider: PaymentProvider;
  message: string;
  details?: Record<string, unknown> | null;
}

export interface PricingConfig {
  creditUnitPriceBRL: number;
  singleDiscountPercent: number;
  pack3DiscountPercent: number;
  pack5DiscountPercent: number;
  englishPriceBRL: number;
  englishBundlePriceBRL: number;
  transactionFeeBRL?: number;
  singlePriceBRL?: number;
  pack3PriceBRL?: number;
  pack5PriceBRL?: number;
}

export interface InterviewConfig {
  introductionPrompt: string;
  questionsPrompt: string;
  feedbackPrompt: string;
  phase1Minutes: number;
  phase2Minutes: number;
  phase3Minutes: number;
  maxVideoSpeechSeconds: number;
  maxSegmentSeconds: number;
}

export interface AdminPartner {
  id: string;
  nome: string;
  cpf?: string;
  descricao?: string;
  email?: string;
  ativo: boolean;
}

export interface CreatePartnerPayload {
  nome: string;
  cpf: string;
  descricao?: string;
}

export interface AdminCoupon {
  id: string;
  nome: string;
  porcentagemDesconto: number;
  ativo: boolean;
  parceiroId?: string;
  parceiroNome?: string;
  porcentagemParceiro?: number;
  totalCompras: number;
  totalUsosCpf: number;
  totalCadastrosViaLink: number;
  receitaTotal: number;
  totalParceiro: number;
  linkParceiro?: string;
}

export interface PartnerReferral {
  id: string;
  userId: string;
  userName: string;
  userEmail?: string;
  userCpf?: string;
  userCreatedAt?: string;
  couponId: string;
  couponCode: string;
  discountPercent: number;
  partnerId?: string;
  partnerName?: string;
  partnerLink?: string;
  linkedAt?: string;
}

export interface PendingPurchaseItem {
  id: string;
  userId?: string;
  userName?: string;
  userEmail?: string;
  planId?: string;
  planName?: string;
  creditsAmount: number;
  price?: number;
  paymentMethod?: string;
  paymentId?: string;
  status: string;
  createdAt?: string;
}

export interface KiwifySaleDetails {
  orderId: string;
  orderRef?: string;
  status?: string;
  paid: boolean;
  alreadyFulfilled: boolean;
  customerEmail?: string;
  priceBRL: number;
  externalReference?: string;
  paymentIdUsed?: string;
}

export interface AdminCreditActionResult {
  success: boolean;
  message?: string;
  processed?: boolean;
  paid?: boolean;
  alreadyFulfilled?: boolean;
  credits?: number;
  userId?: string;
  userEmail?: string;
  sale?: KiwifySaleDetails;
  error?: string;
  failureStage?: string;
  failureMessage?: string;
  orderId?: string;
  orderRef?: string;
}

export interface AdminUserSearchItem {
  id: string;
  email?: string;
  name?: string;
  credits: number;
}

export interface AdminUserListItem {
  id: string;
  email?: string;
  name?: string;
  cpf?: string;
  userType?: string;
  credits: number;
  purchasesCount: number;
  createdAt?: string;
  lastAnalysisAt?: string;
}

export interface AdminPurchaseBuyerItem {
  id: string;
  email?: string;
  name?: string;
  credits: number;
  purchasesCount: number;
  lastPurchaseAt?: string;
}

export interface KiwifyWebhookLogItem {
  id: string;
  orderId?: string;
  orderRef?: string;
  eventType?: string;
  paymentStatus?: string;
  processed: boolean;
  alreadyFulfilled: boolean;
  credits?: number;
  userId?: string;
  httpStatus: number;
  apiVersion?: string;
  message?: string;
  erro?: string;
  failureStage?: string;
  processingDetails?: string;
  payloadRecebido?: string;
  payloadParseado?: string;
  respostaJson?: string;
  createdAt?: string;
}

export interface SalesStatsSummary {
  totalPurchases: number;
  totalRevenue: number;
  approvedRevenue: number;
  pendingRevenue: number;
  totalCreditsSold: number;
  completedPurchases: number;
  pendingPurchases: number;
  cancelledPurchases: number;
  uniqueBuyers: number;
}

export interface CouponMetricRow {
  couponId: string;
  couponName: string;
  discountPercent: number;
  ativo: boolean;
  parceiroId?: string;
  parceiroNome?: string;
  parceiroPercent?: number;
  purchasesCount: number;
  uniqueCpfUses: number;
  revenueTotal: number;
  partnerTotal: number;
}

export interface PartnerMetricRow {
  parceiroId: string;
  parceiroNome: string;
  couponsCount: number;
  purchasesCount: number;
  revenueTotal: number;
  partnerTotal: number;
}

export interface CouponMetrics {
  byCoupon: CouponMetricRow[];
  byPartner: PartnerMetricRow[];
  totalPurchasesWithCoupon: number;
  totalRevenueWithCoupon: number;
  totalPartnerPayout: number;
}

export interface CreateCouponPayload {
  nome: string;
  porcentagemDesconto: number;
  parceiroId?: string | null;
  porcentagemParceiro?: number | null;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('curriculospro_token');
    console.log('🔑 AdminService - Token do localStorage:', token ? token.substring(0, 20) + '...' : 'não encontrado');
    let headers = new HttpHeaders();
    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
      console.log('✅ AdminService - Header Authorization configurado');
    } else {
      console.warn('⚠️ AdminService - Token não encontrado no localStorage');
    }
    return headers;
  }


  getDashboardStats(): Observable<{ success: boolean; stats: DashboardStats }> {
    return this.http.get<{ success: boolean; stats: DashboardStats }>(
      `${this.apiUrl}/admin/stats`,
      { headers: this.getAuthHeaders() }
    ).pipe(
      catchError((error) => {
        if (error.status === 401) {
          console.error('❌ Erro 401 - Token inválido ou expirado');
          console.error('Por favor, faça logout e login novamente');
        }
        return throwError(() => error);
      })
    );
  }

  getDailyUsage(days: number = 30): Observable<{ success: boolean; data: UsageData[] }> {
    return this.http.get<{ success: boolean; data: UsageData[] }>(
      `${this.apiUrl}/admin/usage/daily?days=${days}`,
      { headers: this.getAuthHeaders() }
    ).pipe(
      catchError((error) => {
        if (error.status === 401) {
          console.error('❌ Erro 401 - Token inválido ou expirado');
        }
        return throwError(() => error);
      })
    );
  }

  getMonthlyUsage(months: number = 12): Observable<{ success: boolean; data: UsageData[] }> {
    return this.http.get<{ success: boolean; data: UsageData[] }>(
      `${this.apiUrl}/admin/usage/monthly?months=${months}`,
      { headers: this.getAuthHeaders() }
    ).pipe(
      catchError((error) => {
        if (error.status === 401) {
          console.error('❌ Erro 401 - Token inválido ou expirado');
        }
        return throwError(() => error);
      })
    );
  }

  getSales(limit: number = 100, offset: number = 0): Observable<{ success: boolean; purchases: SalesListItem[]; total: number; limit: number; offset: number }> {
    return this.http.get<{ success: boolean; purchases: SalesListItem[]; total: number; limit: number; offset: number }>(
      `${this.apiUrl}/admin/sales?limit=${limit}&offset=${offset}`,
      { headers: this.getAuthHeaders() }
    ).pipe(
      catchError((error) => {
        if (error.status === 401) {
          console.error('❌ Erro 401 - Token inválido ou expirado');
        }
        return throwError(() => error);
      })
    );
  }

  getSalesStatistics(startDate?: string, endDate?: string): Observable<{ success: boolean; stats: SalesStatsSummary }> {
    const params = new URLSearchParams();
    if (startDate?.trim()) params.set('startDate', startDate.trim());
    if (endDate?.trim()) params.set('endDate', endDate.trim());
    const qs = params.toString();
    return this.http.get<{ success: boolean; stats: SalesStatsSummary }>(
      `${this.apiUrl}/admin/sales/statistics${qs ? `?${qs}` : ''}`,
      { headers: this.getAuthHeaders() }
    );
  }

  getPaymentProvider(): Observable<{ success: boolean } & PaymentProviderSetting> {
    return this.http.get<{ success: boolean } & PaymentProviderSetting>(
      `${this.apiUrl}/admin/settings/payment-provider`,
      { headers: this.getAuthHeaders() }
    );
  }

  updatePaymentProvider(
    provider: PaymentProvider,
    mercadoPagoMode?: MercadoPagoMode
  ): Observable<{ success: boolean; message?: string; warning?: string; provider: PaymentProvider; mercadoPagoMode?: MercadoPagoMode }> {
    return this.http.put<{ success: boolean; message?: string; warning?: string; provider: PaymentProvider; mercadoPagoMode?: MercadoPagoMode }>(
      `${this.apiUrl}/admin/settings/payment-provider`,
      { provider, mercadoPagoMode },
      { headers: this.getAuthHeaders() }
    );
  }

  testPaymentProviderConnection(provider?: PaymentProvider): Observable<PaymentConnectionTestResult> {
    return this.http.post<PaymentConnectionTestResult>(
      `${this.apiUrl}/admin/settings/payment-provider/test`,
      provider ? { provider } : {},
      { headers: this.getAuthHeaders() }
    );
  }

  getPricingSettings(): Observable<{ success: boolean; config: PricingConfig }> {
    return this.http.get<{ success: boolean; config: PricingConfig }>(
      `${this.apiUrl}/admin/settings/pricing`,
      { headers: this.getAuthHeaders() }
    );
  }

  updatePricingSettings(config: PricingConfig): Observable<{ success: boolean; message?: string; config: PricingConfig }> {
    return this.http.put<{ success: boolean; message?: string; config: PricingConfig }>(
      `${this.apiUrl}/admin/settings/pricing`,
      {
        creditUnitPriceBRL: config.creditUnitPriceBRL,
        singleDiscountPercent: config.singleDiscountPercent,
        pack3DiscountPercent: config.pack3DiscountPercent,
        pack5DiscountPercent: config.pack5DiscountPercent,
        englishPriceBRL: config.englishPriceBRL,
        englishBundlePriceBRL: config.englishBundlePriceBRL,
        transactionFeeBRL: config.transactionFeeBRL ?? 0
      },
      { headers: this.getAuthHeaders() }
    );
  }

  getInterviewConfigSettings(): Observable<{ success: boolean; config: InterviewConfig }> {
    return this.http.get<{ success: boolean; config: InterviewConfig }>(
      `${this.apiUrl}/admin/settings/interview-config`,
      { headers: this.getAuthHeaders() }
    );
  }

  updateInterviewConfigSettings(config: Partial<InterviewConfig>): Observable<{ success: boolean; message?: string; config: InterviewConfig }> {
    return this.http.put<{ success: boolean; message?: string; config: InterviewConfig }>(
      `${this.apiUrl}/admin/settings/interview-config`,
      config,
      { headers: this.getAuthHeaders() }
    );
  }

  getPartners(): Observable<{ success: boolean; partners: AdminPartner[] }> {
    return this.http.get<{ success: boolean; partners: AdminPartner[] }>(
      `${this.apiUrl}/admin/partners`,
      { headers: this.getAuthHeaders() }
    );
  }

  createPartner(payload: CreatePartnerPayload): Observable<{ success: boolean; message?: string; partner: AdminPartner }> {
    return this.http.post<{ success: boolean; message?: string; partner: AdminPartner }>(
      `${this.apiUrl}/admin/partners`,
      {
        nome: payload.nome,
        cpf: payload.cpf,
        descricao: payload.descricao || null
      },
      { headers: this.getAuthHeaders() }
    );
  }

  getCoupons(): Observable<{ success: boolean; coupons: AdminCoupon[] }> {
    return this.http.get<{ success: boolean; coupons: AdminCoupon[] }>(
      `${this.apiUrl}/admin/coupons`,
      { headers: this.getAuthHeaders() }
    );
  }

  createCoupon(payload: CreateCouponPayload): Observable<{ success: boolean; message?: string; coupon: AdminCoupon }> {
    return this.http.post<{ success: boolean; message?: string; coupon: AdminCoupon }>(
      `${this.apiUrl}/admin/coupons`,
      payload,
      { headers: this.getAuthHeaders() }
    );
  }

  updateCoupon(
    couponId: string,
    body: { ativo?: boolean; porcentagemDesconto?: number; parceiroId?: string; porcentagemParceiro?: number; clearParceiro?: boolean }
  ): Observable<{ success: boolean; message?: string; coupon: AdminCoupon }> {
    return this.http.put<{ success: boolean; message?: string; coupon: AdminCoupon }>(
      `${this.apiUrl}/admin/coupons/${couponId}`,
      body,
      { headers: this.getAuthHeaders() }
    );
  }

  getCouponMetrics(): Observable<{ success: boolean; metrics: CouponMetrics }> {
    return this.http.get<{ success: boolean; metrics: CouponMetrics }>(
      `${this.apiUrl}/admin/coupons/metrics`,
      { headers: this.getAuthHeaders() }
    );
  }

  getPartnerReferrals(): Observable<{ success: boolean; referrals: PartnerReferral[] }> {
    return this.http.get<{ success: boolean; referrals: PartnerReferral[] }>(
      `${this.apiUrl}/admin/partner-referrals`,
      { headers: this.getAuthHeaders() }
    );
  }

  getPendingPurchases(userId?: string, limit = 50): Observable<{ success: boolean; purchases: PendingPurchaseItem[] }> {
    const params = new URLSearchParams();
    if (userId?.trim()) params.set('userId', userId.trim());
    params.set('limit', String(limit));
    const qs = params.toString();
    return this.http.get<{ success: boolean; purchases: PendingPurchaseItem[] }>(
      `${this.apiUrl}/admin/purchases/pending${qs ? `?${qs}` : ''}`,
      { headers: this.getAuthHeaders() }
    );
  }

  getPurchaseBuyers(limit = 300): Observable<{ success: boolean; buyers: AdminPurchaseBuyerItem[] }> {
    return this.http.get<{ success: boolean; buyers: AdminPurchaseBuyerItem[] }>(
      `${this.apiUrl}/admin/purchases/buyers?limit=${limit}`,
      { headers: this.getAuthHeaders() }
    );
  }

  createPendingPurchase(body: {
    userId?: string;
    email?: string;
    planId: string;
    kiwifyOrderId?: string;
  }): Observable<{ success: boolean; message?: string; purchase?: PendingPurchaseItem; error?: string }> {
    return this.http.post<{ success: boolean; message?: string; purchase?: PendingPurchaseItem; error?: string }>(
      `${this.apiUrl}/admin/purchases/pending`,
      body,
      { headers: this.getAuthHeaders() }
    );
  }

  getKiwifySale(orderId: string): Observable<{ success: boolean; sale: KiwifySaleDetails; error?: string }> {
    return this.http.get<{ success: boolean; sale: KiwifySaleDetails; error?: string }>(
      `${this.apiUrl}/admin/kiwify/sales/${encodeURIComponent(orderId.trim())}`,
      { headers: this.getAuthHeaders() }
    );
  }

  reconcileKiwifyOrder(body: {
    orderId: string;
    pendingPurchaseId?: string;
  }): Observable<AdminCreditActionResult> {
    return this.http.post<AdminCreditActionResult>(
      `${this.apiUrl}/admin/kiwify/reconcile`,
      body,
      { headers: this.getAuthHeaders() }
    );
  }

  processKiwifyWebhook(body: {
    payload: string;
    pendingPurchaseId?: string;
  }): Observable<AdminCreditActionResult> {
    return this.http.post<AdminCreditActionResult>(
      `${this.apiUrl}/admin/kiwify/webhook`,
      body,
      { headers: this.getAuthHeaders() }
    );
  }

  searchUsers(q: string, limit = 20): Observable<{ success: boolean; users: AdminUserSearchItem[] }> {
    const params = new URLSearchParams({ q: q.trim(), limit: String(limit) });
    return this.http.get<{ success: boolean; users: AdminUserSearchItem[] }>(
      `${this.apiUrl}/admin/users/search?${params.toString()}`,
      { headers: this.getAuthHeaders() }
    );
  }

  getUsers(params?: { limit?: number; offset?: number; q?: string }): Observable<{ success: boolean; users: AdminUserListItem[] }> {
    const search = new URLSearchParams();
    search.set('limit', String(params?.limit ?? 300));
    search.set('offset', String(params?.offset ?? 0));
    if (params?.q?.trim()) {
      search.set('q', params.q.trim());
    }
    return this.http.get<{ success: boolean; users: AdminUserListItem[] }>(
      `${this.apiUrl}/admin/users?${search.toString()}`,
      { headers: this.getAuthHeaders() }
    );
  }

  getKiwifyWebhookLogs(params?: {
    orderId?: string;
    orderRef?: string;
    limit?: number;
  }): Observable<{ success: boolean; logs: KiwifyWebhookLogItem[] }> {
    const search = new URLSearchParams();
    if (params?.orderId?.trim()) search.set('orderId', params.orderId.trim());
    if (params?.orderRef?.trim()) search.set('orderRef', params.orderRef.trim());
    search.set('limit', String(params?.limit ?? 50));
    const query = search.toString();
    return this.http.get<{ success: boolean; logs: KiwifyWebhookLogItem[] }>(
      `${this.apiUrl}/admin/kiwify/webhook-logs${query ? `?${query}` : ''}`,
      { headers: this.getAuthHeaders() }
    );
  }

  grantManualCredits(body: {
    userId?: string;
    email?: string;
    planId?: string;
    credits?: number;
    price?: number;
    paymentMethod?: string;
    paymentId?: string;
    pendingPurchaseId?: string;
    reason?: string;
    sendEmail?: boolean;
  }): Observable<AdminCreditActionResult> {
    return this.http.post<AdminCreditActionResult>(
      `${this.apiUrl}/admin/credits/grant`,
      body,
      { headers: this.getAuthHeaders() }
    );
  }

  testPaymentHub(body?: { userId?: string; credits?: number; message?: string }): Observable<{
    success: boolean;
    message?: string;
    userId?: string;
    credits?: number;
    hubPath?: string;
    eventName?: string;
    error?: string;
  }> {
    return this.http.post<{
      success: boolean;
      message?: string;
      userId?: string;
      credits?: number;
      hubPath?: string;
      eventName?: string;
      error?: string;
    }>(
      `${this.apiUrl}/test/payment-hub`,
      body ?? {},
      { headers: this.getAuthHeaders() }
    );
  }
}

