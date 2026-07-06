import { Injectable, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { AnalyzerService } from './analyzer.service';
import { AuthService } from './auth.service';
import { PaymentRealtimeService } from './payment-realtime.service';
import {
  buildPaymentReturnStatusMessage,
  isPaymentReturnReadyMessage,
  PaymentReturnStatus
} from '../utils/payment-return-message';

/**
 * Responde popups de retorno de pagamento (Kiwify/Cakto) usando a sessão da janela principal.
 * A popup perde localStorage após redirect cross-origin; o opener continua autenticado.
 */
@Injectable({
  providedIn: 'root'
})
export class PaymentPopupBridgeService implements OnDestroy {
  private readonly activePopups = new Set<Window>();
  private readonly confirmingPopups = new WeakSet<Window>();
  private readonly maxPollAttempts = 24;
  private readonly pollIntervalMs = 2500;
  private readonly onWindowMessage = (event: MessageEvent): void => {
    if (event.origin !== window.location.origin || !isPaymentReturnReadyMessage(event.data)) {
      return;
    }

    const popup = event.source;
    if (!(popup instanceof Window)) {
      return;
    }

    this.activePopups.add(popup);
    if (this.confirmingPopups.has(popup)) {
      return;
    }

    this.confirmingPopups.add(popup);
    void this.confirmPaymentForPopup(popup);
  };

  constructor(
    private auth: AuthService,
    private analyzer: AnalyzerService,
    private paymentRealtime: PaymentRealtimeService
  ) {
    window.addEventListener('message', this.onWindowMessage);
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.onWindowMessage);
    this.activePopups.clear();
  }

  private postStatus(popup: Window, status: PaymentReturnStatus, credits?: number): void {
    if (popup.closed) {
      this.activePopups.delete(popup);
      return;
    }

    try {
      popup.postMessage(buildPaymentReturnStatusMessage(status, credits), window.location.origin);
    } catch {
      this.activePopups.delete(popup);
    }
  }

  private fetchCredits(): Promise<number | null> {
    return new Promise((resolve) => {
      this.analyzer.getCredits().subscribe({
        next: (res) => resolve(res?.success && res.credits != null ? res.credits : null),
        error: () => resolve(null)
      });
    });
  }

  private async confirmPaymentForPopup(popup: Window): Promise<void> {
    if (!this.auth.getToken()) {
      this.postStatus(popup, 'pending');
      return;
    }

    this.paymentRealtime.ensureSessionConnected();

    let finished = false;
    let pollTimer: ReturnType<typeof setInterval> | null = null;
    let pollAttempts = 0;
    let signalSub: Subscription | null = null;

    const finish = (status: PaymentReturnStatus, credits?: number): void => {
      if (finished || popup.closed) {
        cleanup();
        return;
      }

      finished = true;
      cleanup();
      this.postStatus(popup, status, credits);
      this.activePopups.delete(popup);
    };

    const cleanup = (): void => {
      if (pollTimer) {
        clearInterval(pollTimer);
        pollTimer = null;
      }
      signalSub?.unsubscribe();
      signalSub = null;
    };

    signalSub = this.paymentRealtime.watchPaymentConfirmed().subscribe(() => {
      void this.fetchCredits().then((credits) => {
        if (credits != null) {
          finish('success', credits);
        }
      });
    });

    const initialCredits = await this.fetchCredits();
    this.postStatus(popup, 'confirming', initialCredits ?? undefined);

    pollTimer = setInterval(() => {
      if (popup.closed) {
        finish('pending');
        return;
      }

      pollAttempts += 1;
      void this.fetchCredits().then((credits) => {
        if (credits == null) {
          if (pollAttempts >= this.maxPollAttempts) {
            finish('pending');
          }
          return;
        }

        const increased =
          initialCredits != null ? credits > initialCredits : credits > 0;
        if (increased) {
          finish('success', credits);
        } else if (pollAttempts >= this.maxPollAttempts) {
          finish('pending');
        }
      });
    }, this.pollIntervalMs);
  }
}
