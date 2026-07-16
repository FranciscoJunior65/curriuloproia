import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CookieConsentService } from '../../services/cookie-consent.service';

@Component({
  selector: 'app-cookie-consent-banner',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './cookie-consent-banner.component.html',
  styleUrl: './cookie-consent-banner.component.css'
})
export class CookieConsentBannerComponent {
  constructor(private cookieConsent: CookieConsentService) {}

  get visible(): boolean {
    return !this.cookieConsent.hasDecision;
  }

  accept(): void {
    this.cookieConsent.accept();
  }

  reject(): void {
    this.cookieConsent.reject();
  }
}
