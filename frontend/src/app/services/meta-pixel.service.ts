import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

declare global {
  interface Window {
    fbq?: (...args: unknown[]) => void;
    _fbq?: (...args: unknown[]) => void;
  }
}

@Injectable({ providedIn: 'root' })
export class MetaPixelService {
  private initialized = false;
  private lastTrackedUrl: string | null = null;

  init(): void {
    const pixelId = environment.metaPixelId;
    if (!pixelId || this.initialized || typeof window === 'undefined') {
      return;
    }

    if (!window.fbq) {
      const fbq = function (...args: unknown[]) {
        const queue = fbq as unknown as {
          callMethod?: (...a: unknown[]) => void;
          queue: unknown[];
        };
        if (queue.callMethod) {
          queue.callMethod(...args);
        } else {
          queue.queue.push(args);
        }
      } as ((...args: unknown[]) => void) & {
        callMethod?: (...args: unknown[]) => void;
        queue: unknown[];
        push: (...args: unknown[]) => void;
        loaded: boolean;
        version: string;
      };

      fbq.push = fbq;
      fbq.loaded = true;
      fbq.version = '2.0';
      fbq.queue = [];

      window.fbq = fbq;
      window._fbq = fbq;

      const script = document.createElement('script');
      script.async = true;
      script.src = 'https://connect.facebook.net/en_US/fbevents.js';
      const firstScript = document.getElementsByTagName('script')[0];
      firstScript?.parentNode?.insertBefore(script, firstScript);
    }

    window.fbq('init', pixelId);
    this.initialized = true;
  }

  trackPageView(url?: string): void {
    if (!this.initialized || !window.fbq) {
      return;
    }

    const key = url ?? (typeof window !== 'undefined' ? window.location.pathname : '');
    if (key && this.lastTrackedUrl === key) {
      return;
    }
    this.lastTrackedUrl = key || null;
    window.fbq('track', 'PageView');
  }
}
