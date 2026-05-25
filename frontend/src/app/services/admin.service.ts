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

export type PaymentProvider = 'stripe' | 'mercadopago';

export interface PaymentProviderSetting {
  provider: PaymentProvider;
  providers: PaymentProvider[];
  labels?: Record<PaymentProvider, string>;
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
  singlePriceBRL?: number;
  pack3PriceBRL?: number;
  pack5PriceBRL?: number;
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
  receitaTotal: number;
  totalParceiro: number;
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

  getPaymentProvider(): Observable<{ success: boolean } & PaymentProviderSetting> {
    return this.http.get<{ success: boolean } & PaymentProviderSetting>(
      `${this.apiUrl}/admin/settings/payment-provider`,
      { headers: this.getAuthHeaders() }
    );
  }

  updatePaymentProvider(provider: PaymentProvider): Observable<{ success: boolean; message?: string; provider: PaymentProvider }> {
    return this.http.put<{ success: boolean; message?: string; provider: PaymentProvider }>(
      `${this.apiUrl}/admin/settings/payment-provider`,
      { provider },
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
        englishBundlePriceBRL: config.englishBundlePriceBRL
      },
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
}

