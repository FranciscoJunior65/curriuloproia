import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subscription } from 'rxjs';
import { AnalyzerService } from '../../services/analyzer.service';
import { PaymentRealtimeService } from '../../services/payment-realtime.service';
import { CAKTO_POPUP_MESSAGE_TYPE } from '../../utils/cakto-popup-message';

type KiwifyReturnStatus = 'confirming' | 'success' | 'pending';

@Component({
  selector: 'app-kiwify-popup-return',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './kiwify-popup-return.component.html',
  styleUrl: './kiwify-popup-return.component.scss'
})
export class KiwifyPopupReturnComponent implements OnInit, OnDestroy {
  status: KiwifyReturnStatus = 'confirming';
  userCredits = 0;
  closeCountdown = 5;
  showSuccessOverlay = false;

  private closeTimer: ReturnType<typeof setInterval> | null = null;
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private initialCredits: number | null = null;
  private pollAttempts = 0;
  private confirmed = false;
  private paymentRealtimeSub: Subscription | null = null;

  private readonly maxPollAttempts = 24;
  private readonly pollIntervalMs = 2500;

  constructor(
    private analyzerService: AnalyzerService,
    private paymentRealtime: PaymentRealtimeService
  ) {}

  ngOnInit(): void {
    this.paymentRealtimeSub = this.paymentRealtime.connect().subscribe((event) => {
      if (this.confirmed) {
        return;
      }

      if (event.provider === 'kiwify') {
        void this.confirmFromWebhook(event.credits);
      }
    });

    void this.startConfirmation();
  }

  ngOnDestroy(): void {
    this.paymentRealtimeSub?.unsubscribe();
    this.paymentRealtime.disconnect();
    this.stopTimers();
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

  private async startConfirmation(): Promise<void> {
    this.status = 'confirming';
    this.initialCredits = await this.fetchCredits();
    if (this.initialCredits != null) {
      this.userCredits = this.initialCredits;
    }
    this.startCreditsPolling();
  }

  private fetchCredits(): Promise<number | null> {
    return new Promise((resolve) => {
      this.analyzerService.getCredits().subscribe({
        next: (res) => resolve(res?.success && res.credits != null ? res.credits : null),
        error: () => resolve(null)
      });
    });
  }

  private async confirmFromWebhook(creditsFromHub?: number): Promise<void> {
    if (this.confirmed) {
      return;
    }

    const credits = await this.fetchCredits();
    const resolvedCredits = credits ?? creditsFromHub ?? this.userCredits;
    this.markSuccess(resolvedCredits);
  }

  private markSuccess(credits: number): void {
    if (this.confirmed) {
      return;
    }

    this.confirmed = true;
    this.userCredits = credits;
    this.status = 'success';
    this.showSuccessOverlay = true;
    this.stopCreditsPolling();
    this.notifyOpener(credits);

    if (window.parent === window) {
      this.startCloseCountdown();
    }
  }

  private markPending(): void {
    if (this.confirmed) {
      return;
    }

    this.status = 'pending';
    this.stopCreditsPolling();
  }

  private notifyOpener(credits: number): void {
    const message = { type: CAKTO_POPUP_MESSAGE_TYPE, credits };
    const origin = window.location.origin;

    if (window.opener && !window.opener.closed) {
      try {
        window.opener.postMessage(message, origin);
      } catch {
        // Popup pode ter perdido referência com a janela principal.
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

  private startCreditsPolling(): void {
    this.pollTimer = setInterval(() => {
      this.pollAttempts += 1;

      this.analyzerService.getCredits().subscribe({
        next: (res) => {
          if (!res?.success || res.credits == null) {
            return;
          }

          this.userCredits = res.credits;
          const increased =
            this.initialCredits != null ? res.credits > this.initialCredits : res.credits > 0;

          if (increased) {
            this.markSuccess(res.credits);
          } else if (this.pollAttempts >= this.maxPollAttempts) {
            this.markPending();
          }
        },
        error: () => {
          if (this.pollAttempts >= this.maxPollAttempts) {
            this.markPending();
          }
        }
      });
    }, this.pollIntervalMs);
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

  private stopTimers(): void {
    this.stopCreditsPolling();
    this.stopCloseCountdown();
  }

  private stopCreditsPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private stopCloseCountdown(): void {
    if (this.closeTimer) {
      clearInterval(this.closeTimer);
      this.closeTimer = null;
    }
  }
}
