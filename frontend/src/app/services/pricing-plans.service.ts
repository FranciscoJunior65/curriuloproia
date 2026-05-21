import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, shareReplay } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PublicPlan {
  id: string;
  name: string;
  description?: string;
  analyses: number;
  priceBRL: number;
  savings?: string;
  features?: string[];
}

export interface PlansApiResponse {
  success: boolean;
  plans?: PublicPlan[];
  analysisPlans?: PublicPlan[];
  englishPlan?: PublicPlan;
  englishBundlePriceBRL?: number;
  englishStandalonePriceBRL?: number;
  creditUnitPriceBRL?: number;
}

export interface PriceParts {
  reais: string;
  cents: string;
}

@Injectable({ providedIn: 'root' })
export class PricingPlansService {
  private apiUrl = environment.apiUrl;
  private cache$?: Observable<PlansApiResponse>;

  constructor(private http: HttpClient) {}

  getPlans(): Observable<PlansApiResponse> {
    if (!this.cache$) {
      this.cache$ = this.http
        .get<PlansApiResponse>(`${this.apiUrl}/analyze/plans`)
        .pipe(shareReplay(1));
    }
    return this.cache$;
  }

  clearCache(): void {
    this.cache$ = undefined;
  }

  formatPriceParts(priceBRL: number): PriceParts {
    const safe = Number.isFinite(priceBRL) ? priceBRL : 0;
    const [reais, cents] = safe.toFixed(2).split('.');
    return { reais, cents };
  }

  englishBundleSavings(standalone: number, bundle: number): string {
    const diff = Math.max(0, standalone - bundle);
    if (diff < 0.01) return '';
    return diff.toFixed(2).replace('.', ',');
  }
}
