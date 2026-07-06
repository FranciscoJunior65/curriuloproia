import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CAKTO_POPUP_MESSAGE_TYPE } from '../../utils/cakto-popup-message';
import {
  buildPaymentReturnReadyMessage,
  isPaymentReturnStatusMessage,
  PaymentReturnStatus
} from '../../utils/payment-return-message';

@Component({
  selector: 'app-cakto-popup-return',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './cakto-popup-return.component.html',
  styleUrl: './cakto-popup-return.component.scss'
})
export class CaktoPopupReturnComponent implements OnInit, OnDestroy {
  loading = true;
  verified = false;
  userCredits = 0;
  closeCountdown = 4;

  private closeTimer: ReturnType<typeof setInterval> | null = null;
  private readyRetryTimer: ReturnType<typeof setInterval> | null = null;
  private readyAttempts = 0;
  private confirmed = false;

  private readonly onStatusMessage = (event: MessageEvent): void => {
    if (event.origin !== window.location.origin || !isPaymentReturnStatusMessage(event.data)) {
      return;
    }

    const { status, credits } = event.data;
    if (status === 'confirming') {
      this.loading = true;
      this.verified = false;
      if (credits != null) {
        this.userCredits = credits;
      }
      return;
    }

    if (status === 'success') {
      this.applySuccess(credits ?? this.userCredits);
      return;
    }

    if (status === 'pending') {
      this.applyPending();
    }
  };

  ngOnInit(): void {
    window.addEventListener('message', this.onStatusMessage);
    this.notifyOpenerReady();
    this.startReadyRetry();
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.onStatusMessage);
    this.stopReadyRetry();
    this.stopCloseCountdown();
  }

  closeNow(): void {
    if (window.parent !== window) {
      return;
    }
    window.close();
  }

  get inIframe(): boolean {
    return window.parent !== window;
  }

  private notifyOpenerReady(): void {
    const message = buildPaymentReturnReadyMessage('cakto');
    const origin = window.location.origin;

    if (window.opener && !window.opener.closed) {
      try {
        window.opener.postMessage(message, origin);
      } catch {
        // Janela principal pode ter perdido referência.
      }
    }

    if (window.parent && window.parent !== window) {
      try {
        window.parent.postMessage(message, origin);
      } catch {
        // Modal com iframe.
      }
    }
  }

  private startReadyRetry(): void {
    this.readyRetryTimer = setInterval(() => {
      if (this.confirmed) {
        this.stopReadyRetry();
        return;
      }

      this.readyAttempts += 1;
      this.notifyOpenerReady();

      if (this.readyAttempts >= 12 && this.loading) {
        this.applyPending();
        this.stopReadyRetry();
      }
    }, 2000);
  }

  private stopReadyRetry(): void {
    if (this.readyRetryTimer) {
      clearInterval(this.readyRetryTimer);
      this.readyRetryTimer = null;
    }
  }

  private applySuccess(credits: number): void {
    if (this.confirmed) {
      return;
    }

    this.confirmed = true;
    this.loading = false;
    this.verified = true;
    this.userCredits = credits;
    this.stopReadyRetry();
    this.notifyOpenerPaid(credits);

    if (window.parent === window) {
      this.startCloseCountdown();
    }
  }

  private applyPending(): void {
    if (this.confirmed) {
      return;
    }

    this.loading = false;
    this.verified = true;
    this.stopReadyRetry();
  }

  private notifyOpenerPaid(credits: number): void {
    const message = { type: CAKTO_POPUP_MESSAGE_TYPE, credits };
    const origin = window.location.origin;

    if (window.opener && !window.opener.closed) {
      try {
        window.opener.postMessage(message, origin);
      } catch {
        // Popup externa pode ter perdido referência.
      }
    }

    if (window.parent && window.parent !== window) {
      try {
        window.parent.postMessage(message, origin);
      } catch {
        // Modal com iframe.
      }
    }
  }

  private startCloseCountdown(): void {
    this.stopCloseCountdown();
    this.closeTimer = setInterval(() => {
      this.closeCountdown -= 1;
      if (this.closeCountdown <= 0) {
        this.stopCloseCountdown();
        window.close();
      }
    }, 1000);
  }

  private stopCloseCountdown(): void {
    if (this.closeTimer) {
      clearInterval(this.closeTimer);
      this.closeTimer = null;
    }
  }
}
