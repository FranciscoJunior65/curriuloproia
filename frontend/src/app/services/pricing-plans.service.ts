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

export interface PricingConfigPublic {
  creditUnitPriceBRL: number;
  singleDiscountPercent: number;
  pack3DiscountPercent: number;
  pack5DiscountPercent: number;
  englishPriceBRL: number;
  englishBundlePriceBRL: number;
  singlePriceBRL?: number;
  pack3PriceBRL?: number;
  pack5PriceBRL?: number;
}

export interface PlansApiResponse {
  success: boolean;
  config?: PricingConfigPublic;
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

const SHARED_ANALYSIS_FEATURES = [
  'Otimização para sites de vagas (Gupy, LinkedIn, Vagas.com, Trabalhar Brasil, Empregos.com.br, InfoJobs, Catho, Indeed)',
  'Simulador de entrevista com IA',
  'Currículo melhorado em PDF ou WORD',
  'Palavras-chave estratégicas',
  'Pesquisa de vagas ilimitada por currículo analisado'
];

/** Planos exibidos quando a API não responde */
export const FALLBACK_ANALYSIS_PLANS: PublicPlan[] = [
  {
    id: 'single',
    name: 'Análise Única',
    description: '1 análise completa otimizada para sites de vagas',
    analyses: 1,
    priceBRL: 7.9,
    features: [
      '1 análise completa com IA',
      ...SHARED_ANALYSIS_FEATURES,
      'Análise única para um site específico'
    ]
  },
  {
    id: 'pack3',
    name: 'Pacote 3 Análises',
    description: '3 análises completas otimizadas para diferentes sites',
    analyses: 3,
    priceBRL: 27.9,
    savings: 'Melhor custo-benefício',
    features: ['3 análises completas com IA', ...SHARED_ANALYSIS_FEATURES]
  },
  {
    id: 'pack5',
    name: 'Pacote 5 Análises',
    description: '5 análises completas otimizadas para diferentes sites',
    analyses: 5,
    priceBRL: 37.9,
    savings: 'Economize R$ 1,60',
    features: ['5 análises completas com IA', ...SHARED_ANALYSIS_FEATURES]
  }
];

export const FALLBACK_ENGLISH_PLAN: PublicPlan = {
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
    englishPlan: englishPlan ?? undefined,
    englishBundlePriceBRL: Number(
      body['englishBundlePriceBRL'] ?? body['EnglishBundlePriceBRL'] ?? NaN
    ) || undefined,
    englishStandalonePriceBRL: Number(
      body['englishStandalonePriceBRL'] ?? body['EnglishStandalonePriceBRL'] ?? NaN
    ) || undefined,
    creditUnitPriceBRL: Number(body['creditUnitPriceBRL'] ?? body['CreditUnitPriceBRL'] ?? NaN) || undefined
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
  private apiUrl = environment.apiUrl;
  private cache$?: Observable<PlansApiResponse>;

  constructor(private http: HttpClient) {}

  getPlans(): Observable<PlansApiResponse> {
    if (!this.cache$) {
      this.cache$ = this.http
        .get<Record<string, unknown>>(`${this.apiUrl}/analyze/pricing-config`)
        .pipe(
          map(normalizePlansResponse),
          map((response) => {
            if (!response.analysisPlans?.length) {
              return fallbackResponse();
            }
            return response;
          }),
          catchError((err) => {
            console.warn('Planos: API indisponível, usando valores padrão.', err);
            this.cache$ = undefined;
            return of(fallbackResponse());
          }),
          shareReplay(1)
        );
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
