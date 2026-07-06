import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CAKTO_POPUP_MESSAGE_TYPE } from '../../utils/cakto-popup-message';
import {
  buildPaymentReturnReadyMessage,
  isPaymentReturnStatusMessage,
  PaymentReturnStatus
} from '../../utils/payment-return-message';

@Component({
  selector: 'app-kiwify-popup-return',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './kiwify-popup-return.component.html',
  styleUrl: './kiwify-popup-return.component.scss'
})
export class KiwifyPopupReturnComponent implements OnInit, OnDestroy {
  status: PaymentReturnStatus = 'confirming';
  userCredits = 0;
  closeCountdown = 5;

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
      this.status = 'confirming';
      if (credits != null) {
        this.userCredits = credits;
      }
      return;
    }

    if (status === 'success') {
      this.markSuccess(credits ?? this.userCredits);
      return;
    }

    if (status === 'pending') {
      this.markPending();
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

  get inIframe(): boolean {
    return window.parent !== window;
  }

  closeNow(): void {
    if (window.parent !== window) {
      return;
    }
    window.close();
  }

  private notifyOpenerReady(): void {
    const message = buildPaymentReturnReadyMessage('kiwify');
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
      if (this.confirmed || this.status === 'pending') {
        this.stopReadyRetry();
        return;
      }

      this.readyAttempts += 1;
      this.notifyOpenerReady();

      if (this.readyAttempts >= 12 && this.status === 'confirming') {
        this.markPending();
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

  private markSuccess(credits: number): void {
    if (this.confirmed) {
      return;
    }

    this.confirmed = true;
    this.userCredits = credits;
    this.status = 'success';
    this.stopReadyRetry();
    this.notifyOpenerPaid(credits);

    if (window.parent === window) {
      this.startCloseCountdown();
    }
  }

  private markPending(): void {
    if (this.confirmed) {
      return;
    }

    this.status = 'pending';
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
