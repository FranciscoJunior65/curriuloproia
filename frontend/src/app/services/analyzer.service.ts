import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpRequest, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';

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
  private apiUrl = 'http://localhost:3000/api';
  // Chave pública do Stripe (pode ser exposta no frontend)
  public readonly stripePublishableKey = 'pk_live_51RyHWoBp8nPpyUbivcdINWnufzpZDcj9c6zILwKmXzCLeilDjAaVA7tPvfMdIkX9sRWVCSCGOGZ5WzPxt72UlUL400yPWSCzdk';

  constructor(private http: HttpClient) {}

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('curriculospro_token');
    let headers = new HttpHeaders();
    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
    }
    return headers;
  }


  generateImprovedResume(originalText: string, analysis: any, format: 'pdf' | 'word' = 'pdf', siteId?: string): Observable<Blob | any> {
    const body: any = { originalText, analysis, format };
    if (siteId) {
      body.siteId = siteId;
    }
    
    const headers = this.getAuthHeaders();
    
    if (format === 'pdf') {
      return this.http.post(
        `${this.apiUrl}/analyze/generate-improved`,
        body,
        { 
          headers,
          responseType: 'blob'
        }
      ) as Observable<Blob>;
    } else {
      return this.http.post(
        `${this.apiUrl}/analyze/generate-improved`,
        body,
        { 
          headers,
          responseType: 'json'
        }
      );
    }
  }

  getPlans(): Observable<any> {
    return this.http.get(`${this.apiUrl}/analyze/plans`);
  }

  createPaymentSession(planId: string, userId: string, email?: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/analyze/payment/create-session`, {
      planId,
      userId,
      email
    }, {
      headers: this.getAuthHeaders()
    });
  }

  /**
   * Cria uma compra mockada (para testes - não usa Stripe)
   * @param userId - ID do usuário (obrigatório para testes sem token válido)
   */
  createMockPurchase(planId: string, planName: string, creditsAmount: number, price: number, userId?: string, includeEnglish?: boolean): Observable<any> {
    const body: any = {
      planId,
      planName,
      creditsAmount,
      price
    };
    
    // SEMPRE adiciona userId ao body (permite testar mesmo com token expirado)
    if (userId) {
      body.userId = userId;
      console.log('✅ userId adicionado ao body:', userId);
    } else {
      console.warn('⚠️ userId não fornecido - a compra pode falhar');
    }
    
    // Adiciona flag para incluir currículo em inglês (venda casada)
    if (includeEnglish) {
      body.includeEnglish = true;
      body.englishPrice = 5.90; // Preço promocional quando comprado junto
    }
    
    // NÃO envia headers de autenticação para evitar problemas com token expirado
    // A rota /mock aceita userId diretamente no body
    return this.http.post(`${this.apiUrl}/purchase/mock`, body);
  }

  verifyPayment(sessionId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/analyze/payment/verify?sessionId=${sessionId}`, {
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

  generateCoverLetter(resumeText: string, analysis: any, siteId?: string): Observable<Blob> {
    const body: any = { resumeText, analysis };
    if (siteId) {
      body.siteId = siteId;
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

  searchJobs(analysis: any, siteId: string, location?: string, resumeText?: string, resumeId?: string): Observable<any> {
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
    
    return this.http.post(
      `${this.apiUrl}/analyze/search-jobs`,
      body,
      { 
        headers: this.getAuthHeaders()
      }
    );
  }

  startInterview(resumeText: string, analysis: any, siteId?: string, resumeId?: string): Observable<any> {
    const body: any = { resumeText, analysis };
    if (siteId) {
      body.siteId = siteId;
    }
    if (resumeId) {
      body.resumeId = resumeId;
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

  finishInterview(simulationId: string, allAnswers: any[]): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/analyze/interview/finish`,
      { simulationId, allAnswers },
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

  downloadInterview(simulationId: string): Observable<Blob> {
    return this.http.get(
      `${this.apiUrl}/analyze/interview/${simulationId}/download`,
      { 
        headers: this.getAuthHeaders(),
        responseType: 'blob'
      }
    ) as Observable<Blob>;
  }
}


