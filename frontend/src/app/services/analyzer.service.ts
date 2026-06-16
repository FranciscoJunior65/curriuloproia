import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable, from, of, throwError } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface AnalysisResult {
  success: boolean;
  originalText: string;
  analysis: {
    pontosFortes: string[];
    pontosMelhorar: string[];
    experiencia: string;
    formacao: string;
    habilidades: string[];
    recomendacoes: string[];
    score: number;
  };
  creditsRemaining?: number | null;
  resumeId?: string | null; // ID do currículo no banco de dados
  analysisId?: string | null; // ID da análise paga — libera serviços inclusos sem novo crédito
  servicos?: {
    curriculo_ingles_pago?: boolean;
    curriculo_ingles_gerado?: boolean;
    curriculo_ingles_pdf?: boolean;
    curriculo_ingles_word?: boolean;
    itens?: Array<{ key: string; usado: boolean; pendente: boolean }>;
  };
  metadata?: {
    fileName: string;
    fileSize: number;
    textLength: number;
    processingTime: string;
  };
}

@Injectable({
  providedIn: 'root'
})
export class AnalyzerService {
  private apiUrl = environment.apiUrl;
  public readonly stripePublishableKey = environment.stripePublishableKey;

  constructor(private http: HttpClient) {}

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('curriculospro_token');
    let headers = new HttpHeaders();
    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
    }
    return headers;
  }

  /** POST que retorna arquivo; se a API responder JSON de erro, propaga mensagem legível. */
  private postFileDownload(url: string, body: unknown): Observable<Blob> {
    return this.http
      .post(url, body, {
        headers: this.getAuthHeaders(),
        observe: 'response',
        responseType: 'blob'
      })
      .pipe(
        switchMap((response: HttpResponse<Blob>) => {
          const blob = response.body;
          const contentType = (response.headers.get('content-type') || '').toLowerCase();

          if (!blob || response.status >= 400 || contentType.includes('application/json')) {
            return from(blob?.text() ?? Promise.resolve('')).pipe(
              switchMap((text) => {
                let message = 'Erro ao gerar arquivo';
                try {
                  const json = JSON.parse(text);
                  message = json.message || json.error || message;
                } catch {
                  if (text?.trim()) {
                    message = text.trim();
                  }
                }
                return throwError(() => ({
                  status: response.status,
                  error: { message }
                }));
              })
            );
          }

          return of(blob);
        })
      );
  }


  generateEnglishResume(
    originalText: string,
    analysis?: any,
    format: 'pdf' | 'word' = 'pdf',
    siteId?: string,
    analysisId?: string
  ): Observable<Blob> {
    const body: any = { originalText, format };
    if (analysis) {
      body.analysis = analysis;
    }
    if (siteId) {
      body.siteId = siteId;
    }
    if (analysisId) {
      body.analysisId = analysisId;
    }

    return this.postFileDownload(`${this.apiUrl}/analyze/generate-english`, body);
  }

  generateImprovedResume(
    originalText: string,
    analysis: any,
    format: 'pdf' | 'word' = 'pdf',
    siteId?: string,
    analysisId?: string
  ): Observable<Blob> {
    const body: any = { originalText, analysis, format };
    if (siteId) {
      body.siteId = siteId;
    }
    if (analysisId) {
      body.analysisId = analysisId;
    }

    return this.postFileDownload(`${this.apiUrl}/analyze/generate-improved`, body);
  }

  getPlans(): Observable<any> {
    return this.http.get(`${this.apiUrl}/analyze/plans`);
  }

  getPaymentProvider(): Observable<{ success: boolean; provider: string; providers: string[] }> {
    return this.http.get<{ success: boolean; provider: string; providers: string[] }>(
      `${this.apiUrl}/analyze/payment/provider`
    );
  }

  createPaymentSession(
    planId: string,
    userId: string,
    email?: string,
    couponCode?: string | null,
    cpf?: string | null,
    includeEnglish?: boolean,
    analysisId?: string | null
  ): Observable<any> {
    const body: any = { planId, userId, email: email || '' };
    if (couponCode && couponCode.trim()) body.couponCode = couponCode.trim();
    if (cpf != null && String(cpf).trim()) body.cpf = String(cpf).trim();
    if (includeEnglish) body.includeEnglish = true;
    if (analysisId?.trim()) body.analysisId = analysisId.trim();
    return this.http.post(`${this.apiUrl}/analyze/payment/create-session`, body, {
      headers: this.getAuthHeaders()
    });
  }

  processMercadoPagoCard(payload: {
    planId: string;
    userId: string;
    email?: string;
    couponCode?: string | null;
    cpf?: string | null;
    includeEnglish?: boolean;
    analysisId?: string | null;
    token: string;
    paymentMethodId: string;
    issuerId?: string;
    installments?: number;
  }): Observable<any> {
    const body: Record<string, unknown> = {
      planId: payload.planId,
      userId: payload.userId,
      email: payload.email || '',
      token: payload.token,
      paymentMethodId: payload.paymentMethodId,
      installments: payload.installments ?? 1
    };
    if (payload.issuerId) body['issuerId'] = payload.issuerId;
    if (payload.couponCode?.trim()) body['couponCode'] = payload.couponCode.trim();
    if (payload.cpf != null && String(payload.cpf).trim()) body['cpf'] = String(payload.cpf).trim();
    if (payload.includeEnglish) body['includeEnglish'] = true;
    if (payload.analysisId?.trim()) body['analysisId'] = payload.analysisId.trim();

    return this.http.post(`${this.apiUrl}/analyze/payment/mercadopago/card`, body, {
      headers: this.getAuthHeaders()
    });
  }

  createMercadoPagoPix(payload: {
    planId: string;
    userId: string;
    email?: string;
    couponCode?: string | null;
    cpf?: string | null;
    includeEnglish?: boolean;
    analysisId?: string | null;
  }): Observable<any> {
    const body: Record<string, unknown> = {
      planId: payload.planId,
      userId: payload.userId,
      email: payload.email || ''
    };
    if (payload.couponCode?.trim()) body['couponCode'] = payload.couponCode.trim();
    if (payload.cpf != null && String(payload.cpf).trim()) body['cpf'] = String(payload.cpf).trim();
    if (payload.includeEnglish) body['includeEnglish'] = true;
    if (payload.analysisId?.trim()) body['analysisId'] = payload.analysisId.trim();

    return this.http.post(`${this.apiUrl}/analyze/payment/mercadopago/pix`, body, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * Admin: adiciona créditos grátis para testes (sem pagamento).
   */
  adminFreeCredits(
    planId: string,
    options?: { includeEnglish?: boolean; analysisId?: string }
  ): Observable<{
    success: boolean;
    credits?: number;
    message?: string;
    error?: string;
    curriculo_ingles_pago?: boolean;
  }> {
    const body: Record<string, unknown> = { planId };
    if (options?.includeEnglish) body['includeEnglish'] = true;
    if (options?.analysisId?.trim()) body['analysisId'] = options.analysisId.trim();
    return this.http.post<{
      success: boolean;
      credits?: number;
      message?: string;
      error?: string;
      curriculo_ingles_pago?: boolean;
    }>(`${this.apiUrl}/analyze/payment/admin-free-credits`, body, {
      headers: this.getAuthHeaders()
    });
  }

  /** Valida cupom por código e CPF (obrigatório para uso único por CPF). */
  validateCoupon(code: string, cpf?: string | null): Observable<any> {
    let url = `${this.apiUrl}/analyze/coupon/validate?code=${encodeURIComponent(code)}`;
    if (cpf != null && String(cpf).trim()) url += `&cpf=${encodeURIComponent(String(cpf).trim())}`;
    return this.http.get(url);
  }

  /**
   * Cria uma compra mockada (para testes - não usa Stripe)
   * @param userId - ID do usuário (obrigatório para testes sem token válido)
   */
  createMockPurchase(
    planId: string,
    planName: string,
    creditsAmount: number,
    price: number,
    userId?: string,
    includeEnglish?: boolean,
    analysisId?: string
  ): Observable<any> {
    const body: any = {
      planId,
      planName,
      creditsAmount,
      price
    };

    if (userId) {
      body.userId = userId;
    }

    if (includeEnglish) {
      body.includeEnglish = true;
      body.englishPrice = 5.90;
    }

    if (analysisId?.trim()) {
      body.analysisId = analysisId.trim();
    }

    return this.http.post(`${this.apiUrl}/purchase/mock`, body);
  }

  verifyPayment(sessionId: string, provider?: string): Observable<any> {
    let url = `${this.apiUrl}/analyze/payment/verify?sessionId=${encodeURIComponent(sessionId)}`;
    if (provider) {
      url += `&provider=${encodeURIComponent(provider)}`;
    }
    return this.http.get(url, {
      headers: this.getAuthHeaders()
    });
  }

  getCredits(userId?: string): Observable<any> {
    let url = `${this.apiUrl}/analyze/credits`;
    if (userId) {
      url += `?userId=${userId}`;
    }
    return this.http.get(url, {
      headers: this.getAuthHeaders()
    });
  }

  getJobSites(): Observable<any> {
    return this.http.get(`${this.apiUrl}/analyze/job-sites`);
  }

  analyzeResume(file: File, siteId?: string): Observable<AnalysisResult> {
    const formData = new FormData();
    formData.append('file', file);
    if (siteId) {
      formData.append('siteId', siteId);
    }
    
    return this.http.post<AnalysisResult>(`${this.apiUrl}/analyze/upload`, formData, {
      headers: this.getAuthHeaders()
    });
  }

  generateCoverLetter(resumeText: string, analysis: any, siteId?: string, analysisId?: string): Observable<Blob> {
    const body: any = { resumeText, analysis };
    if (siteId) {
      body.siteId = siteId;
    }
    if (analysisId) {
      body.analysisId = analysisId;
    }
    
    return this.http.post(
      `${this.apiUrl}/analyze/generate-cover-letter`,
      body,
      { 
        headers: this.getAuthHeaders(),
        responseType: 'blob'
      }
    ) as Observable<Blob>;
  }

  searchJobs(
    analysis: any,
    siteId: string,
    location?: string,
    resumeText?: string,
    resumeId?: string,
    analysisId?: string
  ): Observable<any> {
    const body: any = { analysis, siteId };
    if (location) {
      body.location = location;
    }
    if (resumeText) {
      body.resumeText = resumeText;
    }
    if (resumeId) {
      body.resumeId = resumeId;
    }
    if (analysisId) {
      body.analysisId = analysisId;
    }
    
    return this.http.post(
      `${this.apiUrl}/analyze/search-jobs`,
      body,
      { 
        headers: this.getAuthHeaders()
      }
    );
  }

  startInterview(
    resumeText: string,
    analysis: any,
    siteId?: string,
    resumeId?: string,
    analysisId?: string
  ): Observable<any> {
    const body: any = { resumeText, analysis };
    if (siteId) {
      body.siteId = siteId;
    }
    if (resumeId) {
      body.resumeId = resumeId;
    }
    if (analysisId) {
      body.analysisId = analysisId;
    }
    
    return this.http.post(
      `${this.apiUrl}/analyze/interview/start`,
      body,
      { 
        headers: this.getAuthHeaders()
      }
    );
  }

  evaluateAnswer(question: string, answer: string, resumeText: string, analysis: any, simulationId?: string): Observable<any> {
    const body: any = { question, answer, resumeText, analysis };
    if (simulationId) {
      body.simulationId = simulationId;
    }
    
    return this.http.post(
      `${this.apiUrl}/analyze/interview/evaluate`,
      body,
      { 
        headers: this.getAuthHeaders()
      }
    );
  }

  startVoiceInterview(
    resumeText: string,
    analysis: any,
    siteId?: string,
    resumeId?: string,
    analysisId?: string
  ): Observable<any> {
    const body: any = { resumeText, analysis };
    if (siteId) body.siteId = siteId;
    if (resumeId) body.resumeId = resumeId;
    if (analysisId) body.analysisId = analysisId;
    return this.http.post(`${this.apiUrl}/analyze/interview/voice/start`, body, {
      headers: this.getAuthHeaders()
    });
  }

  voiceInterviewTurn(
    resumeText: string,
    analysis: any,
    candidateMessage: string,
    history: { role: string; content: string }[],
    turnNumber: number,
    siteId?: string,
    simulationId?: string,
    analysisId?: string
  ): Observable<any> {
    const body: any = { resumeText, analysis, candidateMessage, history, turnNumber };
    if (siteId) body.siteId = siteId;
    if (simulationId) body.simulationId = simulationId;
    if (analysisId) body.analysisId = analysisId;
    return this.http.post(`${this.apiUrl}/analyze/interview/voice/turn`, body, {
      headers: this.getAuthHeaders()
    });
  }

  finishVoiceInterview(
    resumeText: string,
    analysis: any,
    history: { role: string; content: string }[],
    simulationId?: string,
    analysisId?: string
  ): Observable<any> {
    const body: any = { resumeText, analysis, history };
    if (simulationId) body.simulationId = simulationId;
    if (analysisId) body.analysisId = analysisId;
    return this.http.post(`${this.apiUrl}/analyze/interview/voice/finish`, body, {
      headers: this.getAuthHeaders()
    });
  }

  getStructuredInterviewStatus(analysisId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/analyze/interview/structured/status`, {
      params: { analysisId },
      headers: this.getAuthHeaders()
    });
  }

  startStructuredInterview(
    resumeText: string,
    analysis: any,
    siteId?: string,
    resumeId?: string,
    analysisId?: string
  ): Observable<any> {
    const body: any = { resumeText, analysis };
    if (siteId) body.siteId = siteId;
    if (resumeId) body.resumeId = resumeId;
    if (analysisId) body.analysisId = analysisId;
    return this.http.post(`${this.apiUrl}/analyze/interview/structured/start`, body, {
      headers: this.getAuthHeaders()
    });
  }

  beginStructuredVoicePhase(
    resumeText: string,
    analysis: any,
    opts: {
      simulationId?: string;
      analysisId?: string;
      siteId?: string;
      candidateName?: string;
      writtenQuestions?: string[];
      writtenAnswers?: string[];
    }
  ): Observable<any> {
    const body: any = { resumeText, analysis, ...opts };
    return this.http.post(`${this.apiUrl}/analyze/interview/structured/begin-voice`, body, {
      headers: this.getAuthHeaders()
    });
  }

  submitStructuredPhase(
    simulationId: string,
    phaseIndex: number,
    interviewerScript: string,
    candidateAnswer: string,
    analysisId?: string
  ): Observable<any> {
    const body: any = { simulationId, phaseIndex, interviewerScript, candidateAnswer };
    if (analysisId) body.analysisId = analysisId;
    return this.http.post(`${this.apiUrl}/analyze/interview/structured/submit-phase`, body, {
      headers: this.getAuthHeaders()
    });
  }

  finishStructuredInterview(
    resumeText: string,
    analysis: any,
    opts: {
      simulationId?: string;
      analysisId?: string;
      siteId?: string;
      candidateName?: string;
      introScript?: string;
      phase1Answer?: string;
      writtenQuestions?: string[];
      writtenAnswers?: string[];
    }
  ): Observable<any> {
    const body: any = { resumeText, analysis, ...opts };
    return this.http.post(`${this.apiUrl}/analyze/interview/structured/finish`, body, {
      headers: this.getAuthHeaders()
    });
  }

  finishInterview(simulationId: string, allAnswers: any[], analysisId?: string): Observable<any> {
    const body: any = { simulationId, allAnswers };
    if (analysisId) body.analysisId = analysisId;
    return this.http.post(
      `${this.apiUrl}/analyze/interview/finish`,
      body,
      { 
        headers: this.getAuthHeaders()
      }
    );
  }

  getInterview(simulationId: string): Observable<any> {
    return this.http.get(
      `${this.apiUrl}/analyze/interview/${simulationId}`,
      { 
        headers: this.getAuthHeaders()
      }
    );
  }

  listUserInterviews(): Observable<any> {
    return this.http.get(
      `${this.apiUrl}/analyze/interview/user/list`,
      { 
        headers: this.getAuthHeaders()
      }
    );
  }

  downloadInterview(
    simulationId: string,
    format: 'txt' | 'pdf' | 'docx' = 'txt'
  ): Observable<Blob> {
    return this.http.get(
      `${this.apiUrl}/analyze/interview/${simulationId}/download`,
      {
        headers: this.getAuthHeaders(),
        params: { format },
        responseType: 'blob'
      }
    ) as Observable<Blob>;
  }

  // Histórico de análises
  getUserAnalyses(limit: number = 50, offset: number = 0): Observable<any> {
    return this.http.get(
      `${this.apiUrl}/analyze/analyses?limit=${limit}&offset=${offset}`,
      {
        headers: this.getAuthHeaders()
      }
    );
  }

  getPendingServices(): Observable<any> {
    return this.http.get(`${this.apiUrl}/analyze/pending-services`, {
      headers: this.getAuthHeaders()
    });
  }

  getAnalysisById(analysisId: string): Observable<any> {
    return this.http.get(
      `${this.apiUrl}/analyze/analyses/${analysisId}`,
      {
        headers: this.getAuthHeaders()
      }
    );
  }
}


