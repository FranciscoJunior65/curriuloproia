import { Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { interval, Subscription } from 'rxjs';
import { AuthService } from './services/auth.service';
import { AnalyzerService } from './services/analyzer.service';
import { CpfEnforcementService } from './services/cpf-enforcement.service';
import { PaymentRealtimeService } from './services/payment-realtime.service';
import { PaymentPopupBridgeService } from './services/payment-popup-bridge.service';
import { CreditsHighlightService } from './services/credits-highlight.service';
import { CookieConsentService } from './services/cookie-consent.service';
import { MetaPixelService } from './services/meta-pixel.service';
import { CookieConsentBannerComponent } from './components/cookie-consent-banner/cookie-consent-banner.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CookieConsentBannerComponent],
  template: `
    <router-outlet></router-outlet>
    <app-cookie-consent-banner></app-cookie-consent-banner>
  `
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'CurriculosPro IA';
  private authSub: Subscription | null = null;
  private paymentSub: Subscription | null = null;
  private creditsPollSub: Subscription | null = null;
  private consentSub: Subscription | null = null;
  private routerSub: Subscription | null = null;
  private pixelReady = false;

  constructor(
    private authService: AuthService,
    private analyzerService: AnalyzerService,
    private cpfEnforcement: CpfEnforcementService,
    private paymentRealtime: PaymentRealtimeService,
    private router: Router,
    private paymentPopupBridge: PaymentPopupBridgeService,
    private creditsHighlight: CreditsHighlightService,
    private cookieConsent: CookieConsentService,
    private metaPixel: MetaPixelService
  ) {}

  ngOnInit(): void {
    this.authSub = this.authService.currentUser$.subscribe((user) => {
      if (user && this.authService.getToken()) {
        this.paymentRealtime.ensureSessionConnected();
        this.startCreditsPolling();
      } else {
        this.stopCreditsPolling();
        this.paymentRealtime.endSession();
      }
    });

    if (this.authService.isAuthenticated()) {
      this.paymentRealtime.ensureSessionConnected();
      this.startCreditsPolling();
    }

    this.paymentSub = this.paymentRealtime.watchPaymentConfirmed().subscribe((event) => {
      const currentUser = this.authService.getCurrentUser();
      if (!currentUser || currentUser.id !== event.userId) {
        return;
      }

      this.refreshCreditsFromApi();
    });

    this.routerSub = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.promptCpfIfNeeded(event.urlAfterRedirects || event.url);
        if (this.pixelReady) {
          this.metaPixel.trackPageView(event.urlAfterRedirects || event.url);
        }
      });

    this.consentSub = this.cookieConsent.status$.subscribe((status) => {
      if (status === 'accepted') {
        this.metaPixel.init();
        this.pixelReady = true;
        this.metaPixel.trackPageView(this.router.url);
      } else {
        this.pixelReady = false;
      }
    });

    if (this.authService.isAuthenticated()) {
      this.promptCpfIfNeeded(this.router.url);
    }
  }

  ngOnDestroy(): void {
    this.authSub?.unsubscribe();
    this.paymentSub?.unsubscribe();
    this.consentSub?.unsubscribe();
    this.routerSub?.unsubscribe();
    this.stopCreditsPolling();
    this.paymentRealtime.endSession();
  }

  private startCreditsPolling(): void {
    if (this.creditsPollSub) {
      return;
    }

    this.refreshCreditsFromApi();
    this.creditsPollSub = interval(60000).subscribe(() => {
      this.refreshCreditsFromApi();
    });
  }

  private stopCreditsPolling(): void {
    this.creditsPollSub?.unsubscribe();
    this.creditsPollSub = null;
  }

  private refreshCreditsFromApi(): void {
    if (!this.authService.isAuthenticated()) {
      return;
    }

    this.analyzerService.getCredits().subscribe({
      next: (res: { success?: boolean; credits?: number }) => {
        if (!res?.success || typeof res.credits !== 'number') {
          return;
        }

        const currentUser = this.authService.getCurrentUser();
        if (!currentUser) {
          return;
        }

        const previousCredits = currentUser.credits ?? 0;
        if (currentUser.credits === res.credits) {
          return;
        }

        if (res.credits > previousCredits) {
          this.creditsHighlight.notify(res.credits - previousCredits, res.credits);
        }

        this.authService.updateCurrentUser({ credits: res.credits });
      },
      error: () => {}
    });
  }

  private promptCpfIfNeeded(url: string): void {
    if (this.isPublicRoute(url)) {
      return;
    }

    if (!this.authService.isAuthenticated() || this.cpfEnforcement.hasValidCpf()) {
      return;
    }

    this.cpfEnforcement.ensureCpf({ mandatory: true, context: 'login' }).subscribe();
  }

  private isPublicRoute(url: string): boolean {
    const path = url.split('?')[0];
    return (
      path.startsWith('/login') ||
      path.startsWith('/parceiro') ||
      path.startsWith('/politica-de-privacidade') ||
      path.startsWith('/termos-de-uso') ||
      path.startsWith('/compra/kiwify-popup-retorno') ||
      path.startsWith('/compra/cakto-popup-retorno')
    );
  }
}
