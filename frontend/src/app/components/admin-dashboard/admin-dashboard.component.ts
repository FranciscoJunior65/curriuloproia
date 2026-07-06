import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminService,
  DashboardStats,
  UsageData,
  PaymentProvider,
  MercadoPagoMode,
  PricingConfig,
  InterviewConfig,
  AdminPartner,
  AdminCoupon,
  CouponMetrics,
  PartnerReferral,
  PendingPurchaseItem,
  KiwifySaleDetails,
  KiwifyWebhookLogItem,
  AdminPurchaseBuyerItem,
  SalesListItem,
  SalesStatsSummary
} from '../../services/admin.service';
import { AuthService } from '../../services/auth.service';
import { PricingPlansService } from '../../services/pricing-plans.service';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { SiteHeaderComponent } from '../site-header/site-header.component';
import { PartnerFormDialogComponent } from './partner-form-dialog.component';
import { formatCpfCnpjDisplay, getDocumentDigits, partnerDocumentLabel } from '../../utils/documento.utils';
import { formatPercentDisplay, maskPercentInput } from '../../utils/percent.utils';
import { formatBrlDisplay, maskBrlInput } from '../../utils/currency.utils';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SiteHeaderComponent,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatDialogModule
  ],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss'
})
export class AdminDashboardComponent implements OnInit {
  stats: DashboardStats | null = null;
  recentSales: SalesListItem[] = [];
  salesStats: SalesStatsSummary | null = null;
  dailyUsage: UsageData[] = [];
  monthlyUsage: UsageData[] = [];
  
  loading = true;
  loadingSales = false;
  loadingDaily = false;
  loadingMonthly = false;
  
  selectedPeriod = 30; // dias
  selectedMonths = 12; // meses
  selectedChartDays = 7;
  isAdmin = false;
  accessDenied = false;

  paymentProvider: PaymentProvider = 'stripe';
  mercadoPagoMode: MercadoPagoMode = 'test';
  mercadoPagoModes: MercadoPagoMode[] = ['test', 'production'];
  mercadoPagoModeLabels: Record<MercadoPagoMode, string> = {
    test: 'Teste (sandbox)',
    production: 'Produção (cobrança real)'
  };
  mercadoPagoProductionHint = '';
  paymentProviders: PaymentProvider[] = ['stripe', 'mercadopago', 'cakto', 'kiwify'];
  paymentProviderLabels: Record<PaymentProvider, string> = {
    stripe: 'Stripe',
    mercadopago: 'Mercado Pago',
    cakto: 'Cakto',
    kiwify: 'Kiwify'
  };
  loadingPaymentSettings = false;
  savingPaymentProvider = false;
  testingPaymentConnection = false;
  paymentSettingsMessage = '';
  paymentSettingsError = '';
  paymentConnectionMessage = '';
  paymentConnectionSuccess = false;

  pricingConfig: PricingConfig = {
    creditUnitPriceBRL: 7.9,
    singleDiscountPercent: 0,
    pack3DiscountPercent: 0,
    pack5DiscountPercent: 4.05,
    englishPriceBRL: 17.9,
    englishBundlePriceBRL: 5.9,
    transactionFeeBRL: 0
  };
  creditUnitPriceText = '7,90';
  transactionFeeText = '0,00';
  singleDiscountText = '0,00';
  pack3DiscountText = '0,00';
  pack5DiscountText = '4,05';
  englishPriceText = '17,90';
  englishBundlePriceText = '5,90';
  loadingPricingSettings = false;
  savingPricingSettings = false;
  pricingSettingsMessage = '';
  pricingSettingsError = '';

  interviewConfig: InterviewConfig = {
    introductionPrompt: '',
    questionsPrompt: '',
    feedbackPrompt: '',
    phase1Minutes: 15,
    phase2Minutes: 10,
    phase3Minutes: 10,
    maxVideoSpeechSeconds: 300,
    maxSegmentSeconds: 45
  };
  loadingInterviewConfig = false;
  savingInterviewConfig = false;
  interviewConfigMessage = '';
  interviewConfigError = '';

  loadingCoupons = false;
  savingCoupon = false;
  coupons: AdminCoupon[] = [];
  partners: AdminPartner[] = [];
  partnerReferrals: PartnerReferral[] = [];
  loadingPartnerReferrals = false;
  couponMetrics: CouponMetrics | null = null;
  couponSettingsMessage = '';
  couponSettingsError = '';
  copiedCouponLinkId: string | null = null;

  newCouponCode = '';
  newCouponDiscount = 10;
  newCouponDiscountText = '10,00';
  newCouponPartnerId = '';
  newCouponPartnerPercent = 10;
  newCouponPartnerPercentText = '10,00';
  linkPartnerToCoupon = false;

  partnerSettingsMessage = '';
  partnerSettingsError = '';

  pendingPurchases: PendingPurchaseItem[] = [];
  loadingPendingPurchases = false;
  pendingUserEmail = '';
  pendingUserId = '';
  pendingPlanId = 'single';
  pendingKiwifyOrderId = '';
  savingPendingPurchase = false;

  kiwifyOrderId = '';
  kiwifySale: KiwifySaleDetails | null = null;
  loadingKiwifySale = false;
  reconcilingKiwify = false;
  selectedPendingPurchaseId = '';

  manualGrantEmail = '';
  manualGrantUserId = '';
  manualGrantBuyerId = '';
  manualGrantBuyers: AdminPurchaseBuyerItem[] = [];
  loadingManualGrantBuyers = false;
  manualGrantSelectedUser: AdminPurchaseBuyerItem | null = null;
  manualGrantPlanId = 'single';
  manualGrantCredits: number | null = null;
  manualGrantReason = '';
  manualGrantSendEmail = true;
  grantingManualCredits = false;

  kiwifyWebhookJson = '';
  processingKiwifyWebhook = false;

  kiwifyWebhookLogs: KiwifyWebhookLogItem[] = [];
  loadingKiwifyWebhookLogs = false;
  kiwifyLogsFilterOrder = '';
  kiwifyLogsError = '';
  selectedKiwifyLog: KiwifyWebhookLogItem | null = null;

  creditsPanelMessage = '';
  creditsPanelError = '';
  testingPaymentHub = false;

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private router: Router,
    private pricingPlansService: PricingPlansService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    const token = this.authService.getToken();
    if (!token) {
      console.error('❌ Token não encontrado');
      this.redirectToLogin();
      return;
    }

    const currentUser = this.authService.getCurrentUser();
    this.isAdmin = currentUser?.user_type === 'admin';

    this.authService.verifyToken().subscribe({
      next: (response) => {
        if (!response?.success || !response.user) {
          console.error('❌ Token inválido, redirecionando...');
          this.redirectToLogin();
          return;
        }

        this.authService.setUser(response.user);
        this.isAdmin = response.user.user_type === 'admin';

        if (!this.isAdmin) {
          this.accessDenied = true;
          this.loading = false;
          return;
        }

        console.log('✅ Token verificado, carregando dados...');
        this.loadInitialAdminData();
      },
      error: (error) => {
        console.error('❌ Erro ao verificar token:', error);
        if (error.status === 401 || error.status === 403) {
          this.redirectToLogin();
          return;
        }

        if (this.isAdmin) {
          console.warn('⚠️ Falha ao verificar token, usando sessão local para carregar o admin.');
          this.loadInitialAdminData();
          return;
        }

        this.accessDenied = true;
        this.loading = false;
      }
    });
  }

  private loadInitialAdminData(): void {
    this.accessDenied = false;
    this.loadDashboard();
    this.loadDailyUsage();
    this.loadMonthlyUsage();
    this.loadPaymentProviderSettings();
    this.loadPricingSettings();
    this.loadInterviewConfigSettings();
    this.loadCouponsData();
    this.loadPendingPurchases();
    this.loadManualGrantBuyers();
    this.loadKiwifyWebhookLogs();
    this.loadSales();
    this.loadSalesStatistics();
  }

  private redirectToLogin(): void {
    this.loading = false;
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  loadDashboard(): void {
    this.loading = true;
    this.adminService.getDashboardStats().subscribe({
      next: (response) => {
        console.log('Dashboard response:', response);
        if (response.success) {
          this.stats = response.stats;
          console.log('Stats carregadas:', this.stats);
        } else {
          console.error('Resposta sem sucesso:', response);
        }
        this.loading = false;
      },
      error: (error) => {
        console.error('Erro ao carregar dashboard:', error);
        console.error('Erro completo:', JSON.stringify(error, null, 2));
        
        // Se o erro for 401, tenta renovar o token
        if (error.status === 401) {
          console.log('🔄 Token expirado ou inválido, tentando renovar...');
          const token = this.authService.getToken();
          console.log('Token atual:', token ? token.substring(0, 20) + '...' : 'não encontrado');
          
          this.authService.verifyToken().subscribe({
            next: (response) => {
              if (response.success) {
                console.log('✅ Token renovado, tentando carregar dashboard novamente...');
                // Aguarda um pouco antes de tentar novamente
                setTimeout(() => {
                  this.loadDashboard();
                }, 500);
              } else {
                console.error('❌ Não foi possível renovar o token. Faça logout e login novamente.');
                alert('Sua sessão expirou. Por favor, faça login novamente.');
                this.authService.logout();
                this.router.navigate(['/']);
              }
            },
            error: (verifyError) => {
              console.error('❌ Erro ao verificar token:', verifyError);
              alert('Sua sessão expirou. Por favor, faça login novamente.');
              this.authService.logout();
              this.router.navigate(['/']);
            }
          });
        } else {
          this.loading = false;
        }
      }
    });
  }

  loadDailyUsage(): void {
    this.loadingDaily = true;
    this.adminService.getDailyUsage(this.selectedPeriod).subscribe({
      next: (response) => {
        console.log('Daily usage response:', response);
        if (response.success) {
          this.dailyUsage = response.data;
          console.log('Daily usage data:', this.dailyUsage);
        }
        this.loadingDaily = false;
      },
      error: (error) => {
        console.error('Erro ao carregar uso diário:', error);
        console.error('Erro completo:', JSON.stringify(error, null, 2));
        this.loadingDaily = false;
      }
    });
  }

  loadMonthlyUsage(): void {
    this.loadingMonthly = true;
    this.adminService.getMonthlyUsage(this.selectedMonths).subscribe({
      next: (response) => {
        console.log('Monthly usage response:', response);
        if (response.success) {
          this.monthlyUsage = response.data;
          console.log('Monthly usage data:', this.monthlyUsage);
        }
        this.loadingMonthly = false;
      },
      error: (error) => {
        console.error('Erro ao carregar uso mensal:', error);
        console.error('Erro completo:', JSON.stringify(error, null, 2));
        this.loadingMonthly = false;
      }
    });
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(value);
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return new Intl.DateTimeFormat('pt-BR', {
      day: '2-digit',
      month: '2-digit'
    }).format(date);
  }

  formatMonth(monthString: string): string {
    const [year, month] = monthString.split('-');
    const date = new Date(parseInt(year), parseInt(month) - 1);
    return new Intl.DateTimeFormat('pt-BR', {
      month: 'long',
      year: 'numeric'
    }).format(date);
  }

  onPeriodChange(): void {
    this.loadDailyUsage();
  }

  onMonthsChange(): void {
    this.loadMonthlyUsage();
  }

  refreshAdminData(): void {
    this.loadDashboard();
    this.loadSales();
    this.loadSalesStatistics();
    this.loadDailyUsage();
    this.loadMonthlyUsage();
    this.loadPendingPurchases();
  }

  loadSales(): void {
    this.loadingSales = true;
    this.adminService.getSales(100, 0).subscribe({
      next: (response) => {
        if (response.success) {
          this.recentSales = response.purchases || [];
        }
        this.loadingSales = false;
      },
      error: (error) => {
        console.error('Erro ao carregar vendas:', error);
        this.loadingSales = false;
      }
    });
  }

  loadSalesStatistics(): void {
    this.adminService.getSalesStatistics().subscribe({
      next: (response) => {
        if (response.success) {
          this.salesStats = response.stats;
        }
      },
      error: (error) => {
        console.error('Erro ao carregar estatísticas de vendas:', error);
      }
    });
  }

  formatPaymentMethod(method?: string): string {
    const normalized = (method || '').trim().toLowerCase();
    return (
      {
        stripe: 'Stripe',
        mercadopago: 'Mercado Pago',
        mercado_pago: 'Mercado Pago',
        cakto: 'Cakto',
        kiwify: 'Kiwify',
        pix: 'PIX',
        credit_card: 'Cartão',
        boleto: 'Boleto',
        admin_manual: 'Manual',
        admin_free: 'Admin grátis',
        coupon: 'Cupom'
      }[normalized] || method || '—'
    );
  }

  formatPurchaseStatus(status?: string): string {
    const normalized = (status || '').trim().toLowerCase();
    return (
      {
        concluida: 'Aprovada',
        completed: 'Aprovada',
        paid: 'Aprovada',
        pendente: 'Pendente',
        pending: 'Pendente',
        cancelada: 'Cancelada',
        cancelled: 'Cancelada',
        substituida: 'Substituída'
      }[normalized] || status || '—'
    );
  }

  salesRowClass(status?: string): string {
    const normalized = (status || '').trim().toLowerCase();
    if (normalized === 'concluida' || normalized === 'completed' || normalized === 'paid') {
      return 'sales-row--approved';
    }
    if (normalized === 'pendente' || normalized === 'pending') {
      return 'sales-row--pending';
    }
    return '';
  }

  get recentApprovedSales(): SalesListItem[] {
    return this.recentSales.filter((sale) => {
      const status = (sale.status || '').toLowerCase();
      return status === 'concluida' || status === 'completed' || status === 'paid';
    });
  }

  get recentBuyersCount(): number {
    const keys = new Set(
      this.recentApprovedSales
        .map((sale) => sale.userEmail || sale.userId)
        .filter((value): value is string => !!value)
        .map((value) => value.toLowerCase())
    );
    return keys.size;
  }

  get pendingWithOrderIdCount(): number {
    return this.pendingPurchases.filter((purchase) => this.hasRealPaymentId(purchase.paymentId)).length;
  }

  get pendingWithoutOrderIdCount(): number {
    return this.pendingPurchases.length - this.pendingWithOrderIdCount;
  }

  get averageApprovedTicket(): number {
    const total = this.salesStats?.approvedRevenue
      ?? this.recentApprovedSales.reduce((sum, sale) => sum + (sale.price || 0), 0);
    const count = this.salesStats?.completedPurchases ?? this.recentApprovedSales.length;
    return count > 0 ? total / count : 0;
  }

  get paymentMethodSummary(): Array<{ label: string; count: number; revenue: number; width: number }> {
    const buckets = new Map<string, { label: string; count: number; revenue: number }>();
    for (const sale of this.recentApprovedSales) {
      const label = this.formatPaymentMethod(sale.paymentMethod);
      const current = buckets.get(label) ?? { label, count: 0, revenue: 0 };
      current.count += 1;
      current.revenue += sale.price || 0;
      buckets.set(label, current);
    }

    const items = Array.from(buckets.values()).sort((a, b) => b.count - a.count);
    const max = Math.max(...items.map((item) => item.count), 1);
    return items.map((item) => ({
      ...item,
      width: Math.max(8, Math.round((item.count / max) * 100))
    }));
  }

  get salesTrend(): Array<{ label: string; count: number; revenue: number; height: number }> {
    const days = this.selectedChartDays;
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const buckets = new Map<string, { label: string; count: number; revenue: number }>();
    for (let i = days - 1; i >= 0; i--) {
      const date = new Date(today);
      date.setDate(today.getDate() - i);
      const key = date.toISOString().slice(0, 10);
      buckets.set(key, {
        label: new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit' }).format(date),
        count: 0,
        revenue: 0
      });
    }

    for (const sale of this.recentApprovedSales) {
      if (!sale.createdAt) continue;
      const date = new Date(sale.createdAt);
      if (Number.isNaN(date.getTime())) continue;
      const key = date.toISOString().slice(0, 10);
      const bucket = buckets.get(key);
      if (!bucket) continue;
      bucket.count += 1;
      bucket.revenue += sale.price || 0;
    }

    const items = Array.from(buckets.values());
    const max = Math.max(...items.map((item) => item.count), 1);
    return items.map((item) => ({
      ...item,
      height: Math.max(10, Math.round((item.count / max) * 100))
    }));
  }

  hasRealPaymentId(paymentId?: string): boolean {
    return !!paymentId && !paymentId.startsWith('kiwify_pending_');
  }

  consultPendingPurchase(purchase: PendingPurchaseItem): void {
    this.selectPendingForReconcile(purchase);
    if (!this.hasRealPaymentId(purchase.paymentId)) {
      this.creditsPanelError = 'Essa pendência ainda não tem order_ref/order_id da Kiwify.';
      return;
    }
    this.consultKiwifySale();
  }

  reconcilePendingPurchase(purchase: PendingPurchaseItem): void {
    this.selectPendingForReconcile(purchase);
    if (!this.hasRealPaymentId(purchase.paymentId)) {
      this.creditsPanelError = 'Essa pendência ainda não tem order_ref/order_id da Kiwify para conciliar.';
      return;
    }
    this.reconcileKiwifySale();
  }

  grantPendingManually(purchase: PendingPurchaseItem): void {
    const target = purchase.userEmail || purchase.userId || 'este cliente';
    if (!window.confirm(`Liberar manualmente os créditos de ${target}?`)) {
      return;
    }

    this.grantingManualCredits = true;
    this.creditsPanelMessage = '';
    this.creditsPanelError = '';
    this.adminService.grantManualCredits({
      email: purchase.userEmail || undefined,
      userId: purchase.userId || undefined,
      planId: purchase.planId || undefined,
      price: purchase.price,
      pendingPurchaseId: purchase.id,
      reason: 'Liberação manual de pendência no admin',
      sendEmail: true
    }).subscribe({
      next: (res) => {
        this.grantingManualCredits = false;
        if (res.success) {
          this.creditsPanelMessage = res.message || 'Créditos liberados manualmente.';
          const userId = res.userId || purchase.userId;
          if (userId) {
            this.syncBuyerCreditsLocally(userId, res.credits, !res.alreadyFulfilled);
          }
          this.removePendingPurchaseLocally(purchase.id);
        }
      },
      error: (err) => {
        this.grantingManualCredits = false;
        this.creditsPanelError = err.error?.error || err.error?.message || 'Erro ao liberar pendência manualmente';
      }
    });
  }

  goHome(): void {
    this.router.navigate(['/']);
  }

  loadPaymentProviderSettings(): void {
    this.loadingPaymentSettings = true;
    this.paymentSettingsError = '';
    this.adminService.getPaymentProvider().subscribe({
      next: (response) => {
        if (response.success) {
          this.paymentProvider = response.provider;
          this.paymentProviders = response.providers || ['stripe', 'mercadopago', 'cakto', 'kiwify'];
          if (response.mercadoPagoMode) {
            this.mercadoPagoMode = response.mercadoPagoMode;
          }
          if (response.mercadoPagoModes?.length) {
            this.mercadoPagoModes = response.mercadoPagoModes;
          }
          if (response.mercadoPagoModeLabels) {
            this.mercadoPagoModeLabels = { ...this.mercadoPagoModeLabels, ...response.mercadoPagoModeLabels };
          }
          if (response.mercadoPagoProductionHint) {
            this.mercadoPagoProductionHint = response.mercadoPagoProductionHint;
          }
          if (response.labels) {
            this.paymentProviderLabels = { ...this.paymentProviderLabels, ...response.labels };
          }
        }
        this.loadingPaymentSettings = false;
      },
      error: (error) => {
        this.loadingPaymentSettings = false;
        this.paymentSettingsError = error.error?.message || 'Erro ao carregar meio de pagamento';
      }
    });
  }

  savePaymentProvider(): void {
    this.savingPaymentProvider = true;
    this.paymentSettingsMessage = '';
    this.paymentSettingsError = '';
    this.adminService.updatePaymentProvider(
      this.paymentProvider,
      this.paymentProvider === 'mercadopago' ? this.mercadoPagoMode : undefined
    ).subscribe({
      next: (response) => {
        this.savingPaymentProvider = false;
        if (response.success) {
          this.paymentProvider = response.provider;
          if (response.mercadoPagoMode) {
            this.mercadoPagoMode = response.mercadoPagoMode;
          }
          this.paymentSettingsMessage = response.message || 'Meio de pagamento atualizado.';
          if (response.warning) {
            this.paymentSettingsError = response.warning;
          }
        }
      },
      error: (error) => {
        this.savingPaymentProvider = false;
        this.paymentSettingsError = error.error?.message || error.error?.error || 'Erro ao salvar';
      }
    });
  }

  loadPricingSettings(): void {
    this.loadingPricingSettings = true;
    this.pricingSettingsError = '';
    this.adminService.getPricingSettings().subscribe({
      next: (response) => {
        if (response.success && response.config) {
          this.pricingConfig = {
            ...response.config,
            transactionFeeBRL: response.config.transactionFeeBRL ?? 0
          };
          this.syncPricingDisplayTexts();
        }
        this.loadingPricingSettings = false;
      },
      error: (error) => {
        this.loadingPricingSettings = false;
        this.pricingSettingsError = error.error?.message || 'Erro ao carregar preços';
      }
    });
  }

  savePricingSettings(): void {
    this.savingPricingSettings = true;
    this.pricingSettingsMessage = '';
    this.pricingSettingsError = '';
    this.adminService.updatePricingSettings(this.pricingConfig).subscribe({
      next: (response) => {
        this.savingPricingSettings = false;
        if (response.success) {
          this.pricingConfig = { ...response.config };
          this.syncPricingDisplayTexts();
          this.pricingSettingsMessage = response.message || 'Preços atualizados.';
          this.pricingPlansService.clearCache();
        }
      },
      error: (error) => {
        this.savingPricingSettings = false;
        this.pricingSettingsError = error.error?.message || error.error?.error || 'Erro ao salvar preços';
      }
    });
  }

  loadInterviewConfigSettings(): void {
    this.loadingInterviewConfig = true;
    this.interviewConfigError = '';
    this.adminService.getInterviewConfigSettings().subscribe({
      next: (response) => {
        if (response.success && response.config) {
          this.interviewConfig = { ...response.config };
        }
        this.loadingInterviewConfig = false;
      },
      error: (error) => {
        this.loadingInterviewConfig = false;
        this.interviewConfigError = error.error?.message || 'Erro ao carregar prompts de entrevista';
      }
    });
  }

  saveInterviewConfigSettings(): void {
    this.savingInterviewConfig = true;
    this.interviewConfigMessage = '';
    this.interviewConfigError = '';
    this.adminService.updateInterviewConfigSettings(this.interviewConfig).subscribe({
      next: (response) => {
        this.savingInterviewConfig = false;
        if (response.success) {
          this.interviewConfig = { ...response.config };
          this.interviewConfigMessage = response.message || 'Prompts de entrevista atualizados.';
        }
      },
      error: (error) => {
        this.savingInterviewConfig = false;
        this.interviewConfigError = error.error?.message || error.error?.error || 'Erro ao salvar prompts';
      }
    });
  }

  previewPlanPrice(analyses: number, discountPercent: number): number {
    const base = (this.pricingConfig.creditUnitPriceBRL || 0) * analyses;
    const factor = 1 - (discountPercent || 0) / 100;
    return Math.round(Math.max(0, base * factor) * 100) / 100;
  }

  previewPlanDisplayPrice(analyses: number, discountPercent: number): number {
    const fee = this.pricingConfig.transactionFeeBRL ?? 0;
    return Math.round((this.previewPlanPrice(analyses, discountPercent) + fee) * 100) / 100;
  }

  get creditUnitTotalBRL(): number {
    return Math.round(
      ((this.pricingConfig.creditUnitPriceBRL || 0) + (this.pricingConfig.transactionFeeBRL ?? 0)) * 100
    ) / 100;
  }

  syncPricingDisplayTexts(): void {
    this.creditUnitPriceText = formatBrlDisplay(this.pricingConfig.creditUnitPriceBRL);
    this.transactionFeeText = formatBrlDisplay(this.pricingConfig.transactionFeeBRL ?? 0);
    this.singleDiscountText = formatPercentDisplay(this.pricingConfig.singleDiscountPercent);
    this.pack3DiscountText = formatPercentDisplay(this.pricingConfig.pack3DiscountPercent);
    this.pack5DiscountText = formatPercentDisplay(this.pricingConfig.pack5DiscountPercent);
    this.englishPriceText = formatBrlDisplay(this.pricingConfig.englishPriceBRL);
    this.englishBundlePriceText = formatBrlDisplay(this.pricingConfig.englishBundlePriceBRL);
  }

  onPricingBrlInput(
    field: 'creditUnitPriceBRL' | 'transactionFeeBRL' | 'englishPriceBRL' | 'englishBundlePriceBRL',
    textProp: 'creditUnitPriceText' | 'transactionFeeText' | 'englishPriceText' | 'englishBundlePriceText',
    event: Event
  ): void {
    const input = event.target as HTMLInputElement;
    const masked = maskBrlInput(input.value);
    this[textProp] = masked.text;
    this.pricingConfig[field] = masked.value;
  }

  onPricingPercentInput(
    field: 'singleDiscountPercent' | 'pack3DiscountPercent' | 'pack5DiscountPercent',
    textProp: 'singleDiscountText' | 'pack3DiscountText' | 'pack5DiscountText',
    event: Event
  ): void {
    const input = event.target as HTMLInputElement;
    const masked = maskPercentInput(input.value);
    this[textProp] = masked.text;
    this.pricingConfig[field] = masked.value;
  }

  loadCouponsData(): void {
    this.loadingCoupons = true;
    this.couponSettingsError = '';
    this.adminService.getPartners().subscribe({
      next: (res) => {
        if (res.success) {
          this.partners = (res.partners || []).sort((a, b) => a.nome.localeCompare(b.nome));
        }
      },
      error: () => {}
    });
    this.adminService.getCoupons().subscribe({
      next: (res) => {
        if (res.success) {
          this.coupons = res.coupons || [];
        }
        this.loadingCoupons = false;
      },
      error: (err) => {
        this.loadingCoupons = false;
        this.couponSettingsError = err.error?.error || 'Erro ao carregar cupons';
      }
    });
    this.adminService.getCouponMetrics().subscribe({
      next: (res) => {
        if (res.success) {
          this.couponMetrics = res.metrics;
        }
      },
      error: () => {}
    });
    this.loadPartnerReferrals();
  }

  loadPartnerReferrals(): void {
    this.loadingPartnerReferrals = true;
    this.adminService.getPartnerReferrals().subscribe({
      next: (res) => {
        this.loadingPartnerReferrals = false;
        if (res.success) {
          this.partnerReferrals = res.referrals || [];
        }
      },
      error: () => {
        this.loadingPartnerReferrals = false;
      }
    });
  }

  copyCouponLink(coupon: AdminCoupon): void {
    const link = coupon.linkParceiro;
    if (!link) return;
    navigator.clipboard.writeText(link).then(() => {
      this.copiedCouponLinkId = coupon.id;
      setTimeout(() => (this.copiedCouponLinkId = null), 2500);
    });
  }

  formatDateTime(value?: string): string {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    return date.toLocaleString('pt-BR');
  }

  formatDocument(doc?: string): string {
    const digits = getDocumentDigits(doc || '');
    if (!digits) return '—';
    return formatCpfCnpjDisplay(digits);
  }

  documentLabel(doc?: string): string {
    return partnerDocumentLabel(doc);
  }

  onDiscountPercentInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const masked = maskPercentInput(input.value);
    this.newCouponDiscountText = masked.text;
    this.newCouponDiscount = masked.value;
    input.value = masked.text;
  }

  onPartnerPercentInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const masked = maskPercentInput(input.value);
    this.newCouponPartnerPercentText = masked.text;
    this.newCouponPartnerPercent = masked.value;
    input.value = masked.text;
  }

  openPartnerDialog(): void {
    this.partnerSettingsError = '';
    const ref = this.dialog.open(PartnerFormDialogComponent, {
      width: '440px',
      maxWidth: '95vw',
      panelClass: 'partner-dialog-panel',
      autoFocus: 'first-tabbable',
      restoreFocus: true
    });

    ref.afterClosed().subscribe((partner) => {
      if (partner) {
        this.partners = [...this.partners, partner].sort((a, b) => a.nome.localeCompare(b.nome));
        this.partnerSettingsMessage = `Parceiro "${partner.nome}" incluído. Selecione-o ao criar o cupom.`;
        if (!this.newCouponPartnerId) {
          this.newCouponPartnerId = partner.id;
        }
      }
    });
  }

  createCoupon(): void {
    const code = this.newCouponCode.trim().toUpperCase();
    if (!code) {
      this.couponSettingsError = 'Informe o código do cupom.';
      return;
    }
    if (this.newCouponDiscount < 0 || this.newCouponDiscount > 100) {
      this.couponSettingsError = 'Desconto deve estar entre 0 e 100%.';
      return;
    }
    if (this.linkPartnerToCoupon) {
      if (!this.partners.length) {
        this.couponSettingsError =
          'Cadastre pelo menos um parceiro na seção acima e depois selecione-o aqui.';
        return;
      }
      if (!this.newCouponPartnerId) {
        this.couponSettingsError = 'Selecione o parceiro na lista.';
        return;
      }
    }

    const payload: {
      nome: string;
      porcentagemDesconto: number;
      parceiroId?: string;
      porcentagemParceiro?: number;
    } = {
      nome: code,
      porcentagemDesconto: this.newCouponDiscount
    };

    if (this.linkPartnerToCoupon) {
      payload.parceiroId = this.newCouponPartnerId;
      payload.porcentagemParceiro = this.newCouponPartnerPercent;
    }

    this.savingCoupon = true;
    this.couponSettingsError = '';
    this.couponSettingsMessage = '';

    this.adminService.createCoupon(payload).subscribe({
      next: (res) => {
        this.savingCoupon = false;
        if (res.success) {
          const linkMsg = res.coupon?.linkParceiro ? ` Link: ${res.coupon.linkParceiro}` : '';
          this.couponSettingsMessage = (res.message || 'Cupom criado.') + linkMsg;
          this.newCouponCode = '';
          this.newCouponDiscount = 10;
          this.newCouponDiscountText = formatPercentDisplay(10);
          this.newCouponPartnerPercent = 10;
          this.newCouponPartnerPercentText = formatPercentDisplay(10);
          this.newCouponPartnerId = '';
          this.linkPartnerToCoupon = false;
          this.loadCouponsData();
        }
      },
      error: (err) => {
        this.savingCoupon = false;
        this.couponSettingsError = err.error?.error || 'Erro ao criar cupom';
      }
    });
  }

  toggleCouponActive(coupon: AdminCoupon): void {
    this.adminService.updateCoupon(coupon.id, { ativo: !coupon.ativo }).subscribe({
      next: (res) => {
        if (res.success) {
          coupon.ativo = res.coupon.ativo;
          this.couponSettingsMessage = `Cupom ${coupon.nome} ${coupon.ativo ? 'ativado' : 'desativado'}.`;
        }
      },
      error: (err) => {
        this.couponSettingsError = err.error?.error || 'Erro ao atualizar cupom';
      }
    });
  }

  testPaymentConnection(): void {
    this.testingPaymentConnection = true;
    this.paymentConnectionMessage = '';
    this.paymentConnectionSuccess = false;
    this.adminService.testPaymentProviderConnection(this.paymentProvider).subscribe({
      next: (response) => {
        this.testingPaymentConnection = false;
        this.paymentConnectionSuccess = response.connected;
        this.paymentConnectionMessage = response.message;
      },
      error: (error) => {
        this.testingPaymentConnection = false;
        this.paymentConnectionSuccess = false;
        this.paymentConnectionMessage = error.error?.message || error.error?.error || 'Erro ao testar conexão';
      }
    });
  }

  loadManualGrantBuyers(): void {
    this.loadingManualGrantBuyers = true;
    this.adminService.getPurchaseBuyers(300).subscribe({
      next: (res) => {
        this.loadingManualGrantBuyers = false;
        this.manualGrantBuyers = res.buyers || [];
        if (this.manualGrantBuyerId && !this.manualGrantBuyers.some((buyer) => buyer.id === this.manualGrantBuyerId)) {
          this.manualGrantBuyerId = '';
          this.manualGrantSelectedUser = null;
          this.manualGrantEmail = '';
          this.manualGrantUserId = '';
        }
      },
      error: () => {
        this.loadingManualGrantBuyers = false;
        this.manualGrantBuyers = [];
      }
    });
  }

  formatManualGrantBuyerLabel(buyer: AdminPurchaseBuyerItem): string {
    const identity = buyer.email || buyer.name || buyer.id;
    const namePart = buyer.name && buyer.email ? ` · ${buyer.name}` : '';
    const purchasesLabel = buyer.purchasesCount === 1 ? '1 compra' : `${buyer.purchasesCount} compras`;
    return `${identity}${namePart} · ${purchasesLabel} · ${buyer.credits} crédito(s)`;
  }

  onManualGrantBuyerChange(): void {
    const buyer = this.manualGrantBuyers.find((item) => item.id === this.manualGrantBuyerId) || null;
    this.manualGrantSelectedUser = buyer ? { ...buyer } : null;
    this.manualGrantUserId = buyer?.id || '';
    this.manualGrantEmail = buyer?.email || '';
  }

  selectManualGrantBuyer(buyer: AdminPurchaseBuyerItem): void {
    this.manualGrantBuyerId = buyer.id;
    this.onManualGrantBuyerChange();
  }

  private syncBuyerCreditsLocally(userId: string, credits: number | undefined, incrementPurchases = false): void {
    if (!userId || typeof credits !== 'number') {
      return;
    }

    this.manualGrantBuyers = this.manualGrantBuyers.map((buyer) => {
      if (buyer.id !== userId) {
        return buyer;
      }

      return {
        ...buyer,
        credits,
        purchasesCount: incrementPurchases ? buyer.purchasesCount + 1 : buyer.purchasesCount
      };
    });

    if (this.manualGrantSelectedUser?.id === userId) {
      this.manualGrantSelectedUser = {
        ...this.manualGrantSelectedUser,
        credits,
        purchasesCount: incrementPurchases
          ? this.manualGrantSelectedUser.purchasesCount + 1
          : this.manualGrantSelectedUser.purchasesCount
      };
    }
  }

  private removePendingPurchaseLocally(purchaseId: string): void {
    this.pendingPurchases = this.pendingPurchases.filter((purchase) => purchase.id !== purchaseId);
    if (this.selectedPendingPurchaseId === purchaseId) {
      this.selectedPendingPurchaseId = '';
    }
  }

  loadPendingPurchases(): void {
    this.loadingPendingPurchases = true;
    this.creditsPanelError = '';
    this.adminService.getPendingPurchases().subscribe({
      next: (res) => {
        this.loadingPendingPurchases = false;
        if (res.success) {
          this.pendingPurchases = res.purchases || [];
        }
      },
      error: (err) => {
        this.loadingPendingPurchases = false;
        this.creditsPanelError = err.error?.error || 'Erro ao carregar compras pendentes';
      }
    });
  }

  loadKiwifyWebhookLogs(): void {
    this.loadingKiwifyWebhookLogs = true;
    this.kiwifyLogsError = '';
    const filter = this.kiwifyLogsFilterOrder.trim();
    const looksLikeUuid = /^[0-9a-f-]{36}$/i.test(filter);
    this.adminService.getKiwifyWebhookLogs({
      orderRef: filter && !looksLikeUuid ? filter : undefined,
      orderId: filter && looksLikeUuid ? filter : undefined,
      limit: 100
    }).subscribe({
      next: (res) => {
        this.loadingKiwifyWebhookLogs = false;
        if (res.success) {
          this.kiwifyWebhookLogs = res.logs || [];
          if (this.selectedKiwifyLog) {
            this.selectedKiwifyLog = this.kiwifyWebhookLogs.find(l => l.id === this.selectedKiwifyLog?.id) || null;
          }
        }
      },
      error: (err) => {
        this.loadingKiwifyWebhookLogs = false;
        this.kiwifyLogsError = err.error?.error || err.error?.message || 'Erro ao carregar logs Kiwify';
      }
    });
  }

  selectKiwifyLog(log: KiwifyWebhookLogItem): void {
    this.selectedKiwifyLog = this.selectedKiwifyLog?.id === log.id ? null : log;
  }

  useKiwifyLogInCreditsPanel(log: KiwifyWebhookLogItem): void {
    if (log.orderRef || log.orderId) {
      this.kiwifyOrderId = log.orderRef || log.orderId || '';
    }
    if (log.payloadRecebido?.trim()) {
      this.kiwifyWebhookJson = log.payloadRecebido;
    }
  }

  kiwifyLogStatusLabel(log: KiwifyWebhookLogItem): string {
    if (log.processed) return 'Baixado';
    if (log.alreadyFulfilled) return 'Já existia';
    if (log.failureStage) return 'Falhou';
    return 'Recebido';
  }

  createPendingPurchase(): void {
    if (!this.pendingUserEmail.trim() && !this.pendingUserId.trim()) {
      this.creditsPanelError = 'Informe e-mail ou ID do usuário';
      return;
    }

    this.savingPendingPurchase = true;
    this.creditsPanelMessage = '';
    this.creditsPanelError = '';
    this.adminService.createPendingPurchase({
      email: this.pendingUserEmail.trim() || undefined,
      userId: this.pendingUserId.trim() || undefined,
      planId: this.pendingPlanId,
      kiwifyOrderId: this.pendingKiwifyOrderId.trim() || undefined
    }).subscribe({
      next: (res) => {
        this.savingPendingPurchase = false;
        if (res.success) {
          this.creditsPanelMessage = res.message || 'Solicitação registrada.';
          this.pendingKiwifyOrderId = '';
          this.loadPendingPurchases();
        }
      },
      error: (err) => {
        this.savingPendingPurchase = false;
        this.creditsPanelError = err.error?.error || 'Erro ao registrar solicitação';
      }
    });
  }

  consultKiwifySale(): void {
    if (!this.kiwifyOrderId.trim()) {
      this.creditsPanelError = 'Informe order_ref ou order_id da Kiwify';
      return;
    }

    this.loadingKiwifySale = true;
    this.kiwifySale = null;
    this.creditsPanelError = '';
    this.adminService.getKiwifySale(this.kiwifyOrderId.trim()).subscribe({
      next: (res) => {
        this.loadingKiwifySale = false;
        if (res.success) {
          this.kiwifySale = res.sale;
        }
      },
      error: (err) => {
        this.loadingKiwifySale = false;
        this.creditsPanelError = err.error?.message || err.error?.error || 'Erro ao consultar Kiwify';
      }
    });
  }

  reconcileKiwifySale(): void {
    if (!this.kiwifyOrderId.trim()) {
      this.creditsPanelError = 'Informe order_ref ou order_id da Kiwify';
      return;
    }

    this.reconcilingKiwify = true;
    this.creditsPanelMessage = '';
    this.creditsPanelError = '';
    this.adminService.reconcileKiwifyOrder({
      orderId: this.kiwifyOrderId.trim(),
      pendingPurchaseId: this.selectedPendingPurchaseId.trim() || undefined
    }).subscribe({
      next: (res) => {
        this.reconcilingKiwify = false;
        if (res.success) {
          this.creditsPanelMessage = res.message || (res.processed ? 'Créditos liberados.' : 'Nenhuma ação necessária.');
          if (res.sale) {
            this.kiwifySale = res.sale;
          }
          this.loadPendingPurchases();
          this.loadKiwifyWebhookLogs();
          this.loadSales();
          this.loadSalesStatistics();
          this.loadDashboard();
        }
      },
      error: (err) => {
        this.reconcilingKiwify = false;
        this.creditsPanelError = err.error?.message || err.error?.error || 'Erro ao conciliar venda';
      }
    });
  }

  grantManualCredits(): void {
    if (!this.manualGrantSelectedUser && !this.manualGrantEmail.trim() && !this.manualGrantUserId.trim()) {
      this.creditsPanelError = 'Selecione um cliente na lista';
      return;
    }

    if (!this.manualGrantReason.trim()) {
      this.creditsPanelError = 'Informe o motivo da inclusão';
      return;
    }

    this.grantingManualCredits = true;
    this.creditsPanelMessage = '';
    this.creditsPanelError = '';
    this.adminService.grantManualCredits({
      email: this.manualGrantSelectedUser?.email || this.manualGrantEmail.trim() || undefined,
      userId: this.manualGrantSelectedUser?.id || this.manualGrantUserId.trim() || undefined,
      planId: this.manualGrantPlanId,
      reason: this.manualGrantReason.trim(),
      sendEmail: this.manualGrantSendEmail
    }).subscribe({
      next: (res) => {
        this.grantingManualCredits = false;
        if (res.success) {
          this.creditsPanelMessage = res.message || 'Créditos incluídos.';
          this.manualGrantReason = '';
          const userId = res.userId || this.manualGrantUserId;
          if (userId) {
            this.syncBuyerCreditsLocally(userId, res.credits, !res.alreadyFulfilled);
          }
        }
      },
      error: (err) => {
        this.grantingManualCredits = false;
        this.creditsPanelError = err.error?.error || err.error?.message || 'Erro ao incluir créditos';
      }
    });
  }

  selectPendingForReconcile(purchase: PendingPurchaseItem): void {
    this.selectedPendingPurchaseId = purchase.id;
    if (purchase.paymentId && !purchase.paymentId.startsWith('kiwify_pending_')) {
      this.kiwifyOrderId = purchase.paymentId;
    }
    this.manualGrantPlanId = purchase.planId || 'single';
    this.manualGrantCredits = null;
    if (purchase.userId) {
      this.manualGrantBuyerId = purchase.userId;
      this.onManualGrantBuyerChange();
      if (!this.manualGrantSelectedUser && (purchase.userEmail || purchase.userName)) {
        this.manualGrantSelectedUser = {
          id: purchase.userId,
          email: purchase.userEmail,
          name: purchase.userName,
          credits: purchase.creditsAmount,
          purchasesCount: 1
        };
        this.manualGrantEmail = purchase.userEmail || '';
        this.manualGrantUserId = purchase.userId;
      }
    }
  }

  onKiwifyWebhookJsonChange(): void {
    const raw = this.kiwifyWebhookJson.trim();
    if (!raw) {
      return;
    }

    try {
      const parsed = JSON.parse(raw) as {
        order?: { order_ref?: string; order_id?: string };
        order_ref?: string;
        order_id?: string;
      };
      const orderRef = parsed.order?.order_ref?.trim() || parsed.order_ref?.trim();
      const orderId = parsed.order?.order_id?.trim() || parsed.order_id?.trim();
      if (orderRef || orderId) {
        this.kiwifyOrderId = orderRef || orderId || this.kiwifyOrderId;
      }
    } catch {
      // JSON incompleto enquanto digita/cola
    }
  }

  processKiwifyWebhookJson(): void {
    const payload = this.kiwifyWebhookJson.trim();
    if (!payload) {
      this.creditsPanelError = 'Cole o JSON completo do webhook Kiwify';
      return;
    }

    try {
      JSON.parse(payload);
    } catch {
      this.creditsPanelError = 'JSON inválido — verifique o formato';
      return;
    }

    this.processingKiwifyWebhook = true;
    this.creditsPanelMessage = '';
    this.creditsPanelError = '';
    this.adminService.processKiwifyWebhook({
      payload,
      pendingPurchaseId: this.selectedPendingPurchaseId.trim() || undefined
    }).subscribe({
      next: (res) => {
        this.processingKiwifyWebhook = false;
        if (res.success) {
          this.creditsPanelMessage = res.message
            || (res.processed ? 'Créditos liberados via JSON.' : 'Webhook processado.');
          if (res.sale) {
            this.kiwifySale = res.sale;
          }
          if (res.orderRef || res.orderId) {
            this.kiwifyOrderId = res.orderRef || res.orderId || this.kiwifyOrderId;
          }
          this.loadPendingPurchases();
          this.loadKiwifyWebhookLogs();
          this.loadSales();
          this.loadSalesStatistics();
          this.loadDashboard();
        }
      },
      error: (err) => {
        this.processingKiwifyWebhook = false;
        this.creditsPanelError = err.error?.message || err.error?.error || 'Erro ao processar JSON Kiwify';
      }
    });
  }

  testPaymentHub(): void {
    this.testingPaymentHub = true;
    this.creditsPanelMessage = '';
    this.creditsPanelError = '';
    const userId = this.manualGrantUserId.trim() || this.authService.getCurrentUser()?.id;
    this.adminService.testPaymentHub({ userId, credits: 1, message: 'Teste admin hub' }).subscribe({
      next: (res) => {
        this.testingPaymentHub = false;
        if (res.success) {
          this.creditsPanelMessage = res.message || 'Evento enviado ao hub.';
        }
      },
      error: (err) => {
        this.testingPaymentHub = false;
        this.creditsPanelError = err.error?.error || 'Erro ao testar hub';
      }
    });
  }
}
