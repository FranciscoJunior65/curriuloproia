import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
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

  analyzeResume(file: File): Observable<AnalysisResult> {
    const formData = new FormData();
    formData.append('file', file);
    
    return this.http.post<AnalysisResult>(`${this.apiUrl}/analyze/upload`, formData, {
      headers: this.getAuthHeaders()
    });
  }

  generateImprovedResume(originalText: string, analysis: any): Observable<Blob> {
    return this.http.post(
      `${this.apiUrl}/analyze/generate-improved`,
      { originalText, analysis },
      { responseType: 'blob' }
    );
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
}


