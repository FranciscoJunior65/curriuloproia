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
  PartnerReferral
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
  dailyUsage: UsageData[] = [];
  monthlyUsage: UsageData[] = [];
  
  loading = true;
  loadingDaily = false;
  loadingMonthly = false;
  
  selectedPeriod = 30; // dias
  selectedMonths = 12; // meses
  isAdmin = false;
  accessDenied = false;

  paymentProvider: PaymentProvider = 'stripe';
  mercadoPagoMode: MercadoPagoMode = 'test';
  mercadoPagoModes: MercadoPagoMode[] = ['test', 'production'];
  mercadoPagoModeLabels: Record<MercadoPagoMode, string> = {
    test: 'Teste (sandbox)',
    production: 'Produção (cobrança real)'
  };
  paymentProviders: PaymentProvider[] = ['stripe', 'mercadopago'];
  paymentProviderLabels: Record<PaymentProvider, string> = {
    stripe: 'Stripe',
    mercadopago: 'Mercado Pago'
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
    englishBundlePriceBRL: 5.9
  };
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

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private router: Router,
    private pricingPlansService: PricingPlansService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    // Verifica se o usuário é admin
    if (!this.authService.isAuthenticated()) {
      this.accessDenied = true;
      return;
    }

    this.isAdmin = this.authService.isAdmin();
    
    if (!this.isAdmin) {
      this.accessDenied = true;
      return;
    }

    // Verifica e atualiza o token antes de carregar os dados
    const token = this.authService.getToken();
    if (!token) {
      console.error('❌ Token não encontrado');
      this.authService.logout();
      this.router.navigate(['/']);
      return;
    }

    // Verifica o token primeiro
    this.authService.verifyToken().subscribe({
      next: (response) => {
        if (response.success) {
          console.log('✅ Token verificado, carregando dados...');
          // Se for admin, carrega os dados
          this.loadDashboard();
          this.loadDailyUsage();
          this.loadMonthlyUsage();
          this.loadPaymentProviderSettings();
          this.loadPricingSettings();
          this.loadInterviewConfigSettings();
          this.loadCouponsData();
        } else {
          console.error('❌ Token inválido, redirecionando...');
          alert('Sua sessão expirou. Por favor, faça login novamente.');
          this.authService.logout();
          this.router.navigate(['/']);
        }
      },
      error: (error) => {
        console.error('❌ Erro ao verificar token:', error);
        if (error.status === 401) {
          alert('Sua sessão expirou. Por favor, faça login novamente.');
          this.authService.logout();
          this.router.navigate(['/']);
        } else {
          // Tenta carregar mesmo assim (pode ser um erro de rede)
          this.loadDashboard();
          this.loadDailyUsage();
          this.loadMonthlyUsage();
          this.loadPaymentProviderSettings();
          this.loadPricingSettings();
          this.loadInterviewConfigSettings();
          this.loadCouponsData();
        }
      }
    });
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
          this.paymentProviders = response.providers || ['stripe', 'mercadopago'];
          if (response.mercadoPagoMode) {
            this.mercadoPagoMode = response.mercadoPagoMode;
          }
          if (response.mercadoPagoModes?.length) {
            this.mercadoPagoModes = response.mercadoPagoModes;
          }
          if (response.mercadoPagoModeLabels) {
            this.mercadoPagoModeLabels = { ...this.mercadoPagoModeLabels, ...response.mercadoPagoModeLabels };
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
          this.pricingConfig = { ...response.config };
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
}

