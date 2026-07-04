import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AnalyzerService } from '../../services/analyzer.service';
import { CAKTO_POPUP_MESSAGE_TYPE } from '../../utils/cakto-popup-message';

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
  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private initialCredits: number | null = null;
  private pollAttempts = 0;

  constructor(private analyzerService: AnalyzerService) {}

  ngOnInit(): void {
    void this.confirmAndNotifyOpener();
  }

  ngOnDestroy(): void {
    this.stopTimers();
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

  private async confirmAndNotifyOpener(): Promise<void> {
    this.initialCredits = await this.fetchCredits();
    if (this.initialCredits != null) {
      this.userCredits = this.initialCredits;
    }
    this.notifyOpener(this.initialCredits ?? undefined);
    this.startCreditsPolling();
    if (window.parent === window) {
      this.startCloseCountdown();
    }
  }

  private fetchCredits(): Promise<number | null> {
    return new Promise((resolve) => {
      this.analyzerService.getCredits().subscribe({
        next: (res) => resolve(res?.success && res.credits != null ? res.credits : null),
        error: () => resolve(null)
      });
    });
  }

  private notifyOpener(credits?: number): void {
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

  private startCreditsPolling(): void {
    this.pollTimer = setInterval(() => {
      this.pollAttempts += 1;
      if (this.pollAttempts > 10) {
        this.loading = false;
        this.verified = true;
        this.stopCreditsPolling();
        return;
      }

      this.analyzerService.getCredits().subscribe({
        next: (res) => {
          if (!res?.success || res.credits == null) {
            return;
          }

          this.userCredits = res.credits;
          if (this.initialCredits != null && res.credits > this.initialCredits) {
            this.loading = false;
            this.verified = true;
            this.notifyOpener(res.credits);
            this.stopCreditsPolling();
          } else if (this.pollAttempts >= 6) {
            this.loading = false;
            this.verified = true;
            this.stopCreditsPolling();
          }
        },
        error: () => {
          if (this.pollAttempts >= 6) {
            this.loading = false;
            this.verified = true;
            this.stopCreditsPolling();
          }
        }
      });
    }, 2500);
  }

  private startCloseCountdown(): void {
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
