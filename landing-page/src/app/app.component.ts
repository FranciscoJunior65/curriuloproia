import { Component, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { Subscription } from 'rxjs';
import { CookieConsentBannerComponent } from './components/cookie-consent-banner/cookie-consent-banner.component';
import { CookieConsentService } from './services/cookie-consent.service';
import { MetaPixelService } from './services/meta-pixel.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CookieConsentBannerComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'CurriculosPro IA';
  private consentSub: Subscription | null = null;
  private routerSub: Subscription | null = null;
  private pixelReady = false;

  constructor(
    private cookieConsent: CookieConsentService,
    private metaPixel: MetaPixelService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.consentSub = this.cookieConsent.status$.subscribe((status) => {
      if (status === 'accepted') {
        this.metaPixel.init();
        this.pixelReady = true;
        this.metaPixel.trackPageView(this.router.url);
      } else {
        this.pixelReady = false;
      }
    });

    this.routerSub = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        window.scrollTo({ top: 0 });
        if (this.pixelReady) {
          this.metaPixel.trackPageView(event.urlAfterRedirects || event.url);
        }
      });
  }

  ngOnDestroy(): void {
    this.consentSub?.unsubscribe();
    this.routerSub?.unsubscribe();
  }
}
