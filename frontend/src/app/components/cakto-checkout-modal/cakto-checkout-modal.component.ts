import { firstValueFrom } from 'rxjs';
import {
  Component,
  Inject,
  NgZone,
  OnDestroy,
  OnInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AnalyzerService } from '../../services/analyzer.service';
import { AuthService } from '../../services/auth.service';
import { CpfEnforcementService } from '../../services/cpf-enforcement.service';
import { PaymentCloseResult } from '../../models/payment-close-result';
import { getCpfDigits } from '../../utils/cpf.utils';
import { environment } from '../../../environments/environment';

export interface CaktoCheckoutModalData {
  sdkClientId: string;
  amountBRL: number;
  planName: string;
  planId: string;
  userId: string;
  email: string;
  customerName?: string;
  cpf?: string | null;
  couponCode?: string | null;
  includeEnglish?: boolean;
  analysisId?: string | null;
}

type CaktoTab = 'card' | 'pix';

interface CaktoCardForm {
  holderName: string;
  cardNumber: string;
  cvv: string;
  expMonth: string;
  expYear: string;
}

interface CaktoAuth3DSResult {
  success: boolean;
  cavv?: string;
  eci?: string;
  xid?: string;
  referenceId?: string;
  version?: string;
  trans_status?: string;
  tds_server_trans_id?: string;
  error?: string;
}

interface CaktoSdkInstance {
  initAntifraud(): Promise<void>;
  createToken(card: {
    holderName: string;
    cardNumber: string;
    cvv: string;
    expMonth: string;
    expYear: string;
  }): Promise<{ cardToken: string }>;
  authenticate3DS(params: {
    card: CaktoCardForm;
    provider?: string;
    baseUrl?: string;
    customer: Record<string, unknown>;
  }): Promise<CaktoAuth3DSResult>;
  completeAntifraudProfile(): Promise<void>;
  getAntifraudReference(): string;
  cleanupAntifraud(): void;
}

declare global {
  interface Window {
    Cakto?: {
      CaktoSDK: new (options: { client_id: string }) => CaktoSdkInstance;
    };
  }
}

@Component({
  selector: 'app-cakto-checkout-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './cakto-checkout-modal.component.html',
  styleUrl: './cakto-checkout-modal.component.scss'
})
export class CaktoCheckoutModalComponent implements OnInit, OnDestroy {
  activeTab: CaktoTab = 'card';
  loadingSdk = true;
  processingCard = false;
  generatingPix = false;
  pixPolling = false;
  errorMessage = '';
  successMessage = '';
  pixQrCode: string | null = null;
  pixQrCodeBase64: string | null = null;
  cardForm: CaktoCardForm = {
    holderName: '',
    cardNumber: '',
    cvv: '',
    expMonth: '',
    expYear: ''
  };

  private caktoSdk: CaktoSdkInstance | null = null;
  private pixPaymentId: string | null = null;
  private pixPollTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: CaktoCheckoutModalData,
    private dialogRef: MatDialogRef<CaktoCheckoutModalComponent>,
    private analyzerService: AnalyzerService,
    private authService: AuthService,
    private cpfEnforcement: CpfEnforcementService,
    private ngZone: NgZone
  ) {
    this.cardForm.holderName = data.customerName || '';
  }

  ngOnInit(): void {
    void this.initCaktoSdk();
  }

  ngOnDestroy(): void {
    this.stopPixPolling();
    try {
      this.caktoSdk?.cleanupAntifraud();
    } catch {
      // SDK pode não estar carregado.
    }
  }

  setTab(tab: CaktoTab): void {
    this.activeTab = tab;
    this.errorMessage = '';
  }

  close(): void {
    this.dialogRef.close('cancelled');
  }

  generatePix(): void {
    const cpfDigits = getCpfDigits(this.authService.getCurrentUser()?.cpf ?? this.data.cpf ?? '');
    if (!this.cpfEnforcement.hasValidCpf() || cpfDigits.length !== 11) {
      this.generatingPix = false;
      this.dialogRef.close('cpf_required');
      return;
    }

    this.data.cpf = cpfDigits;
    this.generatingPix = true;
    this.errorMessage = '';

    this.analyzerService
      .createCaktoPix({
        planId: this.data.planId,
        userId: this.data.userId,
        email: this.data.email,
        customerName: this.data.customerName,
        couponCode: this.data.couponCode,
        cpf: cpfDigits,
        includeEnglish: this.data.includeEnglish,
        analysisId: this.data.analysisId
      })
      .subscribe({
        next: (res) => {
          this.generatingPix = false;
          if (!res.success) {
            this.errorMessage = res.error || res.message || 'Erro ao gerar PIX';
            return;
          }
          this.pixPaymentId = res.paymentId;
          this.pixQrCode = res.qrCode;
          this.pixQrCodeBase64 = res.qrCodeBase64;
          this.startPixPolling();
        },
        error: (err) => {
          this.generatingPix = false;
          if (err.error?.code === 'CPF_REQUIRED') {
            this.dialogRef.close('cpf_required');
            return;
          }
          this.errorMessage =
            err.error?.message || err.error?.error || err.message || 'Erro ao gerar PIX';
        }
      });
  }

  submitCard(): void {
    if (!this.caktoSdk) {
      this.errorMessage = 'SDK Cakto indisponível';
      return;
    }

    if (!this.cpfEnforcement.hasValidCpf()) {
      this.dialogRef.close('cpf_required');
      return;
    }

    this.data.cpf = getCpfDigits(this.authService.getCurrentUser()?.cpf ?? this.data.cpf ?? '');

    if (!this.isCardFormValid()) {
      this.errorMessage = 'Preencha todos os campos do cartão.';
      return;
    }

    this.processingCard = true;
    this.errorMessage = '';

    void this.processCardPayment();
  }

  copyPixCode(): void {
    if (!this.pixQrCode) {
      return;
    }
    void navigator.clipboard.writeText(this.pixQrCode);
    this.successMessage = 'Código PIX copiado!';
    setTimeout(() => {
      this.successMessage = '';
    }, 2500);
  }

  onExpMonthInput(): void {
    this.cardForm.expMonth = this.cardForm.expMonth.replace(/\D/g, '').slice(0, 2);
  }

  onExpYearInput(): void {
    this.cardForm.expYear = this.cardForm.expYear.replace(/\D/g, '').slice(0, 2);
  }

  /** Base URL da nossa API (sem /api) — SDK chama {baseUrl}/api/financial/3ds/token/ */
  private getCakto3dsBaseUrl(): string {
    return environment.apiUrl.replace(/\/api\/?$/i, '');
  }

  private async initCaktoSdk(): Promise<void> {
    try {
      await this.loadCaktoSdk();
      if (!window.Cakto?.CaktoSDK) {
        throw new Error('SDK da Cakto indisponível');
      }

      this.caktoSdk = new window.Cakto.CaktoSDK({ client_id: this.data.sdkClientId });
      await this.caktoSdk.initAntifraud();
      this.ngZone.run(() => {
        this.loadingSdk = false;
      });
    } catch (err: unknown) {
      this.ngZone.run(() => {
        this.loadingSdk = false;
        this.errorMessage = err instanceof Error ? err.message : 'Erro ao carregar pagamento Cakto';
      });
    }
  }

  private async processCardPayment(): Promise<void> {
    const sdk = this.caktoSdk;
    if (!sdk) {
      return;
    }

    try {
      const cpfDigits = getCpfDigits(this.authService.getCurrentUser()?.cpf ?? this.data.cpf ?? '');
      const card = {
        holderName: this.cardForm.holderName.trim(),
        cardNumber: this.cardForm.cardNumber.replace(/\D/g, ''),
        cvv: this.cardForm.cvv.trim(),
        expMonth: this.normalizeCardExpMonth(this.cardForm.expMonth.trim()),
        expYear: this.normalizeCardExpYear(this.cardForm.expYear.trim())
      };

      if (card.expMonth.length !== 2 || card.expYear.length !== 2) {
        throw new Error('Informe validade do cartão com 2 dígitos (MM/AA).');
      }

      const cardToken = await firstValueFrom(
        this.analyzerService.createCaktoCardToken({
          holderName: card.holderName,
          cardNumber: card.cardNumber,
          expMonth: card.expMonth,
          expYear: card.expYear,
          cvv: card.cvv
        })
      );

      if (!cardToken.success || !cardToken.cardToken) {
        throw new Error(cardToken.error || cardToken.message || 'Erro ao tokenizar cartão');
      }

      const tokenizedCard = cardToken.cardToken;

      const authResult = await sdk.authenticate3DS({
        card,
        provider: 'cielo',
        baseUrl: this.getCakto3dsBaseUrl(),
        customer: {
          amount: Math.round(this.data.amountBRL * 100),
          currency: 'BRL',
          email: this.data.email,
          name: card.holderName,
          phone: '5511999999999',
          paymentMethod: 'credit',
          address: this.build3DsBillingAddress()
        }
      });

      if (!authResult.success) {
        throw new Error(authResult.error || 'Falha na autenticação 3DS');
      }

      await sdk.completeAntifraudProfile();
      const antifraudReference = sdk.getAntifraudReference()?.trim();
      if (!antifraudReference) {
        throw new Error('Referência antifraude não gerada. Recarregue a página e tente novamente.');
      }

      this.ngZone.run(() => {
        this.analyzerService
          .processCaktoCard({
            planId: this.data.planId,
            userId: this.data.userId,
            email: this.data.email,
            customerName: card.holderName,
            couponCode: this.data.couponCode,
            cpf: cpfDigits,
            includeEnglish: this.data.includeEnglish,
            analysisId: this.data.analysisId,
            cardToken: tokenizedCard,
            antifraudProfilingAttemptReference: antifraudReference,
            cavv: authResult.cavv,
            eci: authResult.eci,
            xid: authResult.xid,
            referenceId: authResult.referenceId,
            version: authResult.version,
            transStatus: authResult.trans_status,
            tdsServerTransId: authResult.tds_server_trans_id
          })
          .subscribe({
            next: (res) => {
              this.processingCard = false;
              if (res.paid) {
                this.closePaid(res.user?.credits);
                return;
              }
              this.errorMessage = res.message || 'Pagamento não aprovado. Tente outro cartão.';
            },
            error: (err) => {
              this.processingCard = false;
              if (err.error?.code === 'CPF_REQUIRED') {
                this.dialogRef.close('cpf_required');
                return;
              }
              this.errorMessage =
                err.error?.message || err.error?.error || err.message || 'Erro ao processar cartão';
            }
          });
      });
    } catch (err: unknown) {
      this.ngZone.run(() => {
        this.processingCard = false;
        this.errorMessage = err instanceof Error ? err.message : 'Erro ao processar cartão';
      });
    }
  }

  private isCardFormValid(): boolean {
    return !!(
      this.cardForm.holderName.trim() &&
      this.cardForm.cardNumber.replace(/\D/g, '').length >= 13 &&
      this.cardForm.cvv.trim().length >= 3 &&
      this.cardForm.expMonth.trim() &&
      this.cardForm.expYear.trim()
    );
  }

  private normalizeCardExpYear(value: string): string {
    const digits = value.replace(/\D/g, '');
    if (digits.length >= 4) {
      return digits.slice(-2);
    }
    return digits.slice(0, 2).padStart(2, '0');
  }

  private normalizeCardExpMonth(value: string): string {
    return value.replace(/\D/g, '').slice(0, 2).padStart(2, '0');
  }

  /** Endereço de cobrança exigido pelo 3DS da Cakto (produto digital, sem entrega física). */
  private build3DsBillingAddress(): {
    street: string;
    number: string;
    city: string;
    state: string;
    zipcode: string;
  } {
    return {
      street: 'Av. Paulista',
      number: '1000',
      city: 'São Paulo',
      state: 'SP',
      zipcode: '01310100'
    };
  }

  private startPixPolling(): void {
    if (!this.pixPaymentId) {
      return;
    }
    this.stopPixPolling();
    this.pixPolling = true;

    const check = () => {
      this.analyzerService.verifyPayment(this.pixPaymentId!, 'cakto').subscribe({
        next: (res) => {
          if (res.paid) {
            this.stopPixPolling();
            this.closePaid(res.user?.credits);
          }
        },
        error: () => {
          // Mantém polling — PIX pode demorar ou webhook chegar depois.
        }
      });
    };

    check();
    this.pixPollTimer = setInterval(check, 4000);
  }

  private stopPixPolling(): void {
    if (this.pixPollTimer) {
      clearInterval(this.pixPollTimer);
      this.pixPollTimer = null;
    }
    this.pixPolling = false;
  }

  private closePaid(credits?: number): void {
    const result: PaymentCloseResult = { paid: true };
    if (credits != null) {
      result.credits = credits;
    }
    this.dialogRef.close(result);
  }

  private loadCaktoSdk(): Promise<void> {
    if (window.Cakto?.CaktoSDK) {
      return Promise.resolve();
    }

    return new Promise((resolve, reject) => {
      const existing = document.querySelector('script[data-cakto-sdk="v1"]');
      if (existing) {
        existing.addEventListener('load', () => resolve());
        existing.addEventListener('error', () => reject(new Error('Falha ao carregar SDK Cakto')));
        if (window.Cakto?.CaktoSDK) {
          resolve();
        }
        return;
      }

      const script = document.createElement('script');
      script.src = 'https://cakto-sdk.pages.dev/cakto-sdk.min.js';
      script.async = true;
      script.dataset['caktoSdk'] = 'v1';
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Falha ao carregar SDK da Cakto'));
      document.body.appendChild(script);
    });
  }
}
