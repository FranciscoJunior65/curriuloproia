import { Component, OnDestroy, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Subscription } from 'rxjs';
import { NavbarComponent } from './components/navbar/navbar.component';
import { HeroComponent } from './components/hero/hero.component';
import { FeaturesComponent } from './components/features/features.component';
import { PricingComponent } from './components/pricing/pricing.component';
import { TestimonialsComponent } from './components/testimonials/testimonials.component';
import { FooterComponent } from './components/footer/footer.component';
import { ProcessComponent } from './components/process/process.component';
import { CtaComponent } from './components/cta/cta.component';
import { FaqComponent } from './components/faq/faq.component';
import { TeamComponent } from './components/team/team.component';
import { CookieConsentBannerComponent } from './components/cookie-consent-banner/cookie-consent-banner.component';
import { CookieConsentService } from './services/cookie-consent.service';
import { MetaPixelService } from './services/meta-pixel.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    NavbarComponent,
    HeroComponent,
    ProcessComponent,
    FeaturesComponent,
    PricingComponent,
    TeamComponent,
    TestimonialsComponent,
    FaqComponent,
    CtaComponent,
    FooterComponent,
    CookieConsentBannerComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'CurriculosPro IA';
  private consentSub: Subscription | null = null;

  constructor(
    private cookieConsent: CookieConsentService,
    private metaPixel: MetaPixelService
  ) {}

  ngOnInit(): void {
    this.consentSub = this.cookieConsent.status$.subscribe((status) => {
      if (status === 'accepted') {
        this.metaPixel.init();
        this.metaPixel.trackPageView();
      }
    });
  }

  ngOnDestroy(): void {
    this.consentSub?.unsubscribe();
  }
}
