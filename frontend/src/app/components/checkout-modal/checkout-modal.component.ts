import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Subscription } from 'rxjs';
import { isCaktoPopupPaidMessage } from '../../utils/cakto-popup-message';
import { AnalyzerService } from '../../services/analyzer.service';
import { PaymentRealtimeService } from '../../services/payment-realtime.service';

export interface CheckoutModalData {
  checkoutUrl: string;
  providerLabel: string;
  /** Incorpora pay.cakto.com.br no modal (sem window.open). */
  embedInModal?: boolean;
  provider?: 'stripe' | 'mercadopago' | 'cakto' | 'kiwify';
}

@Component({
  selector: 'app-checkout-modal',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './checkout-modal.component.html',
  styleUrl: './checkout-modal.component.scss'
})
export class CheckoutModalComponent implements OnInit, OnDestroy {
  popupBlocked = false;
  popupClosed = false;
  iframeLoading = true;
  iframeBlocked = false;
  safeCheckoutUrl: SafeResourceUrl;

  private popup: Window | null = null;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private creditsPollTimer: ReturnType<typeof setInterval> | null = null;
  private paymentRealtimeSub: Subscription | null = null;
  private paidHandled = false;
  private initialCredits: number | null = null;

  private readonly onPaymentMessage = (event: MessageEvent): void => {
    if (event.origin !== window.location.origin || !isCaktoPopupPaidMessage(event.data)) {
      return;
    }

    this.paidHandled = true;
    this.stopPolling();
    this.dialogRef.close({
      completed: true,
      paid: true,
      credits: event.data.credits
    });
  };

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: CheckoutModalData,
    private dialogRef: MatDialogRef<CheckoutModalComponent>,
    private analyzerService: AnalyzerService,
    private paymentRealtime: PaymentRealtimeService,
    sanitizer: DomSanitizer
  ) {
    this.safeCheckoutUrl = sanitizer.bypassSecurityTrustResourceUrl(data.checkoutUrl);
  }

  get embedInModal(): boolean {
    return this.data.embedInModal === true;
  }

  ngOnInit(): void {
    window.addEventListener('message', this.onPaymentMessage);

    if (this.data.provider === 'kiwify') {
      this.paymentRealtimeSub = this.paymentRealtime.connect().subscribe((event) => {
        this.handleKiwifyPaymentConfirmed(event.credits);
      });
    }

    if (this.embedInModal) {
      return;
    }

    this.openPopup();
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.onPaymentMessage);
    this.paymentRealtimeSub?.unsubscribe();
    if (this.data.provider === 'kiwify') {
      this.paymentRealtime.disconnect();
    }
    this.stopPolling();
    this.stopCreditsPolling();
  }

  private handleKiwifyPaymentConfirmed(credits: number): void {
    if (this.paidHandled) {
      return;
    }

    this.paidHandled = true;
    this.stopPolling();
    this.stopCreditsPolling();
    this.dialogRef.close({
      completed: true,
      paid: true,
      credits
    });
  }

  onIframeLoad(): void {
    this.iframeLoading = false;
  }

  useExternalWindow(): void {
    this.iframeBlocked = false;
    this.data = { ...this.data, embedInModal: false };
    this.openPopup();
  }

  openPopup(): void {
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

    this.popup = window.open(this.data.checkoutUrl, 'curriculospro_checkout', features);
    this.popupBlocked = !this.popup;
    this.popupClosed = false;

    if (this.popup) {
      void this.captureInitialCredits();
      this.startPolling();
    }
  }

  private captureInitialCredits(): void {
    this.analyzerService.getCredits().subscribe({
      next: (res) => {
        if (res?.success && res.credits != null) {
          this.initialCredits = res.credits;
        }
      }
    });
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollTimer = setInterval(() => {
      if (!this.popup || this.popup.closed) {
        this.popupClosed = true;
        this.stopPolling();
        if (!this.paidHandled) {
          if (this.data.provider === 'kiwify') {
            this.pollCreditsAfterPayment();
          } else {
            this.dialogRef.close('completed');
          }
        }
      }
    }, 800);
  }

  private pollCreditsAfterPayment(): void {
    let attempts = 0;
    this.stopCreditsPolling();
    this.creditsPollTimer = setInterval(() => {
      attempts += 1;
      this.analyzerService.getCredits().subscribe({
        next: (res) => {
          if (!res?.success || res.credits == null) {
            return;
          }

          const increased =
            this.initialCredits != null ? res.credits > this.initialCredits : res.credits > 0;
          if (increased || attempts >= 12) {
            this.paidHandled = true;
            this.stopCreditsPolling();
            this.dialogRef.close({
              completed: true,
              paid: increased,
              credits: res.credits
            });
          }
        },
        error: () => {
          if (attempts >= 12) {
            this.stopCreditsPolling();
            this.dialogRef.close('completed');
          }
        }
      });
    }, 3000);
  }

  private stopCreditsPolling(): void {
    if (this.creditsPollTimer) {
      clearInterval(this.creditsPollTimer);
      this.creditsPollTimer = null;
    }
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  openInNewTab(): void {
    window.open(this.data.checkoutUrl, '_blank');
  }

  close(): void {
    this.dialogRef.close('cancelled');
  }
}
