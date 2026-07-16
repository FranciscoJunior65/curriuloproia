import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type CookieConsentStatus = 'pending' | 'accepted' | 'rejected';

const STORAGE_KEY = 'cookie_consent_v1';

@Injectable({ providedIn: 'root' })
export class CookieConsentService {
  private readonly statusSubject = new BehaviorSubject<CookieConsentStatus>(this.readStoredStatus());
  readonly status$ = this.statusSubject.asObservable();

  get status(): CookieConsentStatus {
    return this.statusSubject.value;
  }

  get hasDecision(): boolean {
    return this.status !== 'pending';
  }

  accept(): void {
    this.persist('accepted');
  }

  reject(): void {
    this.persist('rejected');
  }

  private persist(status: Exclude<CookieConsentStatus, 'pending'>): void {
    try {
      localStorage.setItem(STORAGE_KEY, status);
    } catch {
      // ignore storage errors (private mode, etc.)
    }
    this.statusSubject.next(status);
  }

  private readStoredStatus(): CookieConsentStatus {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      if (value === 'accepted' || value === 'rejected') {
        return value;
      }
    } catch {
      // ignore
    }
    return 'pending';
  }
}
