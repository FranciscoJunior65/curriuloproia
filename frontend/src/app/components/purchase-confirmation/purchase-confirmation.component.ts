import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SiteHeaderComponent } from '../site-header/site-header.component';
import { AnalyzerService } from '../../services/analyzer.service';
import { AuthService } from '../../services/auth.service';

export type PurchaseOutcome = 'success' | 'pending' | 'failure' | 'cancelled';

@Component({
  selector: 'app-purchase-confirmation',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    SiteHeaderComponent
  ],
  templateUrl: './purchase-confirmation.component.html',
  styleUrl: './purchase-confirmation.component.scss'
})
export class PurchaseConfirmationComponent implements OnInit {
  outcome: PurchaseOutcome = 'success';
  loading = true;
  verified = false;
  errorMessage: string | null = null;

  userCredits = 0;
  englishCredits = 0;
  analysisId: string | null = null;
  englishPaid = false;
  isFreeCheckout = false;
  alreadyFulfilled = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private analyzerService: AnalyzerService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    const path = this.route.snapshot.routeConfig?.path ?? '';
    if (path.includes('pendente')) {
      this.outcome = 'pending';
    } else if (path.includes('falha')) {
      this.outcome = 'failure';
    } else if (path.includes('cancelada')) {
      this.outcome = 'cancelled';
    } else {
      this.outcome = 'success';
    }

    const params = this.route.snapshot.queryParamMap;
    this.isFreeCheckout = params.get('free') === '1';
    this.analysisId = params.get('analysisId');
    this.englishPaid = params.get('englishPaid') === '1';

    if (this.outcome === 'cancelled') {
      this.loading = false;
      return;
    }

    if (this.outcome === 'failure') {
      this.loading = false;
      this.errorMessage =
        params.get('status_detail') ||
        'Não foi possível concluir o pagamento. Você pode tentar novamente.';
      return;
    }

    if (this.outcome === 'pending') {
      this.loading = false;
      this.tryVerifyPendingPayment(params);
      return;
    }

    this.processSuccessReturn(params);
  }

  private processSuccessReturn(params: { get: (k: string) => string | null }): void {
    if (this.isFreeCheckout) {
      this.analyzerService.getCredits().subscribe({
        next: (res) => this.applyCreditsResponse(res, true),
        error: () => {
          this.loading = false;
          this.verified = true;
          this.errorMessage = null;
        }
      });
      return;
    }

    const provider =
      params.get('provider') ||
      (params.get('payment_id') || params.get('collection_id') ? 'mercadopago' : 'stripe');
    const paymentRef =
      provider === 'mercadopago'
        ? params.get('payment_id') || params.get('collection_id')
        : params.get('session_id');

    const mpStatus = params.get('status') || params.get('collection_status');
    if (provider === 'mercadopago' && mpStatus === 'pending' && !paymentRef) {
      this.outcome = 'pending';
      this.loading = false;
      return;
    }

    if (!paymentRef) {
      this.loading = false;
      this.verified = true;
      this.refreshCreditsOnly();
      return;
    }

    this.analyzerService.verifyPayment(paymentRef, provider).subscribe({
      next: (response: any) => {
        if (response?.success && response.paid) {
          this.verified = true;
          this.alreadyFulfilled = !!response.alreadyFulfilled;
          if (response.user?.credits != null) {
            this.userCredits = response.user.credits;
            this.syncAuthCredits();
          }
          this.refreshCreditsOnly();
        } else if (response?.success && !response.paid) {
          this.outcome = mpStatus === 'pending' ? 'pending' : 'pending';
          this.loading = false;
          this.errorMessage =
            response.statusDetail ||
            'Pagamento em processamento. Os créditos serão liberados após a confirmação.';
        } else {
          this.loading = false;
          this.errorMessage = response?.error || 'Não foi possível confirmar o pagamento.';
        }
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage =
          err.error?.message || err.error?.error || 'Erro ao confirmar pagamento. Tente novamente.';
      }
    });
  }

  private tryVerifyPendingPayment(params: { get: (k: string) => string | null }): void {
    const paymentRef = params.get('payment_id') || params.get('collection_id');
    if (!paymentRef) {
      return;
    }

    this.analyzerService.verifyPayment(paymentRef, 'mercadopago').subscribe({
      next: (response: any) => {
        if (response?.success && response.paid) {
          this.outcome = 'success';
          this.verified = true;
          if (response.user?.credits != null) {
            this.userCredits = response.user.credits;
            this.syncAuthCredits();
          }
          this.refreshCreditsOnly();
        }
      },
      error: () => {}
    });
  }

  private applyCreditsResponse(res: any, markVerified: boolean): void {
    this.loading = false;
    if (markVerified) {
      this.verified = true;
    }
    if (res?.success) {
      if (res.credits != null) {
        this.userCredits = res.credits;
        this.syncAuthCredits();
      }
      this.englishCredits = res.englishCredits ?? 0;
    }
  }

  private refreshCreditsOnly(): void {
    this.analyzerService.getCredits().subscribe({
      next: (res) => {
        this.loading = false;
        this.verified = true;
        this.applyCreditsResponse(res, false);
      },
      error: () => {
        this.loading = false;
        this.verified = true;
      }
    });
  }

  private syncAuthCredits(): void {
    const user = this.authService.getCurrentUser();
    if (user) {
      this.authService.setUser({ ...user, credits: this.userCredits });
    }
  }

  goHome(): void {
    if (this.analysisId && this.englishPaid) {
      this.router.navigate(['/'], {
        queryParams: { analysisId: this.analysisId, action: 'english' }
      });
      return;
    }
    this.router.navigate(['/']);
  }

  goFinanceiro(): void {
    this.router.navigate(['/financeiro']);
  }

  retryPurchase(): void {
    this.router.navigate(['/'], { fragment: 'plans-section' });
  }

  get title(): string {
    switch (this.outcome) {
      case 'success':
        return this.verified ? 'Compra confirmada!' : 'Confirmando compra…';
      case 'pending':
        return 'Pagamento pendente';
      case 'failure':
        return 'Pagamento não concluído';
      case 'cancelled':
        return 'Compra cancelada';
      default:
        return 'Compra';
    }
  }

  get subtitle(): string {
    switch (this.outcome) {
      case 'success':
        if (this.loading) {
          return 'Estamos validando seu pagamento com segurança.';
        }
        if (this.isFreeCheckout) {
          return 'Seus créditos foram liberados gratuitamente.';
        }
        if (this.alreadyFulfilled) {
          return 'Esta compra já havia sido processada. Seus créditos estão disponíveis.';
        }
        if (this.englishPaid && this.analysisId) {
          return 'O currículo em inglês foi liberado para a sua análise.';
        }
        return 'Obrigado! Seus créditos já estão disponíveis na sua conta.';
      case 'pending':
        return 'PIX e boleto podem levar alguns minutos. Você receberá os créditos assim que o Mercado Pago confirmar.';
      case 'failure':
        return this.errorMessage || 'Tente novamente ou escolha outro meio de pagamento.';
      case 'cancelled':
        return 'Você voltou antes de finalizar. Nenhuma cobrança foi feita.';
      default:
        return '';
    }
  }

  get icon(): string {
    switch (this.outcome) {
      case 'success':
        return 'check_circle';
      case 'pending':
        return 'schedule';
      case 'failure':
        return 'error_outline';
      case 'cancelled':
        return 'cancel';
      default:
        return 'shopping_bag';
    }
  }
}
