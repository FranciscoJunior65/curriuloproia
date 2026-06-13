import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
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
}

export interface PriceParts {
  reais: string;
  cents: string;
}

const FALLBACK_ANALYSIS_PLANS: PublicPlan[] = [
  { id: 'single', name: 'Análise Única', analyses: 1, priceBRL: 7.9 },
  { id: 'pack3', name: 'Pacote 3 Análises', analyses: 3, priceBRL: 27.9, savings: 'Melhor custo-benefício' },
  { id: 'pack5', name: 'Pacote 5 Análises', analyses: 5, priceBRL: 37.9 }
];

const FALLBACK_ENGLISH_PLAN: PublicPlan = {
  id: 'english',
  name: 'Currículo em Inglês',
  analyses: 0,
  priceBRL: 17.9
};

function normalizePlan(raw: Record<string, unknown>): PublicPlan | null {
  const id = (raw['id'] ?? raw['Id']) as string | undefined;
  if (!id) return null;

  const savings = raw['savings'] ?? raw['Savings'];
  return {
    id,
    name: String(raw['name'] ?? raw['Name'] ?? ''),
    description: (raw['description'] ?? raw['Description']) as string | undefined,
    analyses: Number(raw['analyses'] ?? raw['Analyses'] ?? 0),
    priceBRL: Number(raw['priceBRL'] ?? raw['PriceBRL'] ?? 0),
    savings: savings != null ? String(savings) : undefined,
    features: (raw['features'] ?? raw['Features']) as string[] | undefined
  };
}

function normalizePlansResponse(body: Record<string, unknown>): PlansApiResponse {
  const plansRaw = (body['plans'] ?? body['Plans']) as Record<string, unknown>[] | undefined;
  const analysisRaw =
    (body['analysisPlans'] ?? body['AnalysisPlans']) as Record<string, unknown>[] | undefined;

  const plans = (plansRaw ?? [])
    .map(normalizePlan)
    .filter((p): p is PublicPlan => p != null);

  let analysisPlans = (analysisRaw ?? [])
    .map(normalizePlan)
    .filter((p): p is PublicPlan => p != null);

  if (!analysisPlans.length && plans.length) {
    analysisPlans = plans.filter((p) => p.id !== 'english');
  }

  const englishFromBody = body['englishPlan'] ?? body['EnglishPlan'];
  let englishPlan =
    englishFromBody && typeof englishFromBody === 'object'
      ? normalizePlan(englishFromBody as Record<string, unknown>)
      : null;

  if (!englishPlan) {
    englishPlan = plans.find((p) => p.id === 'english') ?? null;
  }

  return {
    success: Boolean(body['success'] ?? body['Success'] ?? true),
    plans,
    analysisPlans,
    englishPlan: englishPlan ?? undefined
  };
}

function fallbackResponse(): PlansApiResponse {
  return {
    success: true,
    plans: [...FALLBACK_ANALYSIS_PLANS, FALLBACK_ENGLISH_PLAN],
    analysisPlans: [...FALLBACK_ANALYSIS_PLANS],
    englishPlan: FALLBACK_ENGLISH_PLAN
  };
}

@Injectable({ providedIn: 'root' })
export class PricingPlansService {
  private cache$?: Observable<PlansApiResponse>;

  constructor(private http: HttpClient) {}

  getPlans(): Observable<PlansApiResponse> {
    if (!this.cache$) {
      this.cache$ = this.http
        .get<Record<string, unknown>>(`${environment.apiUrl}/analyze/pricing-config`)
        .pipe(
          map(normalizePlansResponse),
          map((response) =>
            response.analysisPlans?.length ? response : fallbackResponse()
          ),
          catchError(() => {
            this.cache$ = undefined;
            return of(fallbackResponse());
          }),
          shareReplay(1)
        );
    }
    return this.cache$;
  }

  formatPriceParts(priceBRL: number): PriceParts {
    const safe = Number.isFinite(priceBRL) ? priceBRL : 0;
    const [reais, cents] = safe.toFixed(2).split('.');
    return { reais, cents };
  }
}
