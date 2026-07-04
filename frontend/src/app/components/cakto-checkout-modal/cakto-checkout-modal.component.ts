import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AnalyzerService } from '../../services/analyzer.service';
import { AuthService } from '../../services/auth.service';
import { CpfEnforcementService } from '../../services/cpf-enforcement.service';
import { PaymentCloseResult } from '../../models/payment-close-result';
import { getCpfDigits } from '../../utils/cpf.utils';
import { isCaktoPopupPaidMessage } from '../../utils/cakto-popup-message';

export interface CaktoCheckoutModalData {
  sdkClientId?: string;
  /** Valor exibido ao usuário (base + taxa). */
  amountBRL: number;
  /** Valor base enviado à Cakto (deve bater com a oferta). */
  chargeAmountBRL?: number;
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

@Component({
  selector: 'app-cakto-checkout-modal',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './cakto-checkout-modal.component.html',
  styleUrl: './cakto-checkout-modal.component.scss'
})
export class CaktoCheckoutModalComponent implements OnInit, OnDestroy {
  activeTab: CaktoTab = 'card';
  openingCardCheckout = false;
  cardPopupBlocked = false;
  generatingPix = false;
  pixPolling = false;
  errorMessage = '';
  successMessage = '';
  pixQrCode: string | null = null;
  pixQrCodeBase64: string | null = null;

  private cardPopup: Window | null = null;
  private cardPopupTimer: ReturnType<typeof setInterval> | null = null;
  private hostedPaidHandled = false;
  private readonly onCaktoPopupMessage = (event: MessageEvent): void => {
    if (event.origin !== window.location.origin || !isCaktoPopupPaidMessage(event.data)) {
      return;
    }

    this.hostedPaidHandled = true;
    this.stopCardPopupPolling();
    this.closePaid(event.data.credits);
  };
  private pixPaymentId: string | null = null;
  private pixPollTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: CaktoCheckoutModalData,
    private dialogRef: MatDialogRef<CaktoCheckoutModalComponent>,
    private analyzerService: AnalyzerService,
    private authService: AuthService,
    private cpfEnforcement: CpfEnforcementService
  ) {}

  ngOnInit(): void {
    window.addEventListener('message', this.onCaktoPopupMessage);
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.onCaktoPopupMessage);
    this.stopCardPopupPolling();
    this.stopPixPolling();
  }

  setTab(tab: CaktoTab): void {
    this.activeTab = tab;
    this.errorMessage = '';
  }

  close(): void {
    this.dialogRef.close('cancelled');
  }

  openCardCheckout(): void {
    const cpfDigits = getCpfDigits(this.authService.getCurrentUser()?.cpf ?? this.data.cpf ?? '');
    if (!this.cpfEnforcement.hasValidCpf() || cpfDigits.length !== 11) {
      this.dialogRef.close('cpf_required');
      return;
    }

    this.data.cpf = cpfDigits;
    this.openingCardCheckout = true;
    this.errorMessage = '';
    this.cardPopupBlocked = false;

    this.analyzerService
      .createCaktoCardCheckout({
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
          this.openingCardCheckout = false;
          if (!res.success || !res.checkoutUrl) {
            this.errorMessage = res.error || res.message || 'Erro ao abrir pagamento com cartão';
            return;
          }
          this.launchCardPopup(res.checkoutUrl);
        },
        error: (err) => {
          this.openingCardCheckout = false;
          if (err.error?.code === 'CPF_REQUIRED') {
            this.dialogRef.close('cpf_required');
            return;
          }
          this.errorMessage =
            err.error?.message || err.error?.error || err.message || 'Erro ao abrir pagamento com cartão';
        }
      });
  }

  openCardCheckoutInNewTab(): void {
    const cpfDigits = getCpfDigits(this.authService.getCurrentUser()?.cpf ?? this.data.cpf ?? '');
    if (!this.cpfEnforcement.hasValidCpf() || cpfDigits.length !== 11) {
      this.dialogRef.close('cpf_required');
      return;
    }

    this.openingCardCheckout = true;
    this.errorMessage = '';

    this.analyzerService
      .createCaktoCardCheckout({
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
          this.openingCardCheckout = false;
          if (!res.success || !res.checkoutUrl) {
            this.errorMessage = res.error || res.message || 'Erro ao abrir pagamento com cartão';
            return;
          }
          window.open(res.checkoutUrl, '_blank');
        },
        error: (err) => {
          this.openingCardCheckout = false;
          this.errorMessage =
            err.error?.message || err.error?.error || err.message || 'Erro ao abrir pagamento com cartão';
        }
      });
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

  private launchCardPopup(checkoutUrl: string): void {
    this.stopCardPopupPolling();
    this.hostedPaidHandled = false;

    const width = 520;
    const height = Math.min(820, window.screen.availHeight - 40);
    const left = Math.max(0, Math.round((window.screen.width - width) / 2));
    const top = Math.max(0, Math.round((window.screen.height - height) / 2));
    const features = [
      `width=${width}`,
      `height=${height}`,
      `left=${left}`,
      `top=${top}`,
      'scrollbars=yes',
      'resizable=yes',
      'noopener=no',
      'noreferrer=no'
    ].join(',');

    this.cardPopup = window.open(checkoutUrl, 'curriculospro_cakto_card', features);
    this.cardPopupBlocked = !this.cardPopup;

    if (this.cardPopup) {
      this.startCardPopupPolling();
    }
  }

  private startCardPopupPolling(): void {
    this.stopCardPopupPolling();
    this.cardPopupTimer = setInterval(() => {
      if (!this.cardPopup || this.cardPopup.closed) {
        this.stopCardPopupPolling();
        if (!this.hostedPaidHandled) {
          this.dialogRef.close('hosted_completed');
        }
      }
    }, 800);
  }

  private stopCardPopupPolling(): void {
    if (this.cardPopupTimer) {
      clearInterval(this.cardPopupTimer);
      this.cardPopupTimer = null;
    }
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
}
