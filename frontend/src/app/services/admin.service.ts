import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

export interface DashboardStats {
  totalUsers: number;
  totalCredits: number;
  analysesPerformed: number;
  estimatedRevenue: number;
  activeUsers: number;
}

export interface UsageData {
  date?: string;
  month?: string;
  registrations: number;
  analyses: number;
  revenue: number;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = 'http://localhost:3000/api';

  constructor(private http: HttpClient) {}

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('curriculospro_token');
    console.log('🔑 AdminService - Token do localStorage:', token ? token.substring(0, 20) + '...' : 'não encontrado');
    let headers = new HttpHeaders();
    if (token) {
      headers = headers.set('Authorization', `Bearer ${token}`);
      console.log('✅ AdminService - Header Authorization configurado');
    } else {
      console.warn('⚠️ AdminService - Token não encontrado no localStorage');
    }
    return headers;
  }


  getDashboardStats(): Observable<{ success: boolean; stats: DashboardStats }> {
    return this.http.get<{ success: boolean; stats: DashboardStats }>(
      `${this.apiUrl}/admin/stats`,
      { headers: this.getAuthHeaders() }
    ).pipe(
      catchError((error) => {
        if (error.status === 401) {
          console.error('❌ Erro 401 - Token inválido ou expirado');
          console.error('Por favor, faça logout e login novamente');
        }
        return throwError(() => error);
      })
    );
  }

  getDailyUsage(days: number = 30): Observable<{ success: boolean; data: UsageData[] }> {
    return this.http.get<{ success: boolean; data: UsageData[] }>(
      `${this.apiUrl}/admin/usage/daily?days=${days}`,
      { headers: this.getAuthHeaders() }
    ).pipe(
      catchError((error) => {
        if (error.status === 401) {
          console.error('❌ Erro 401 - Token inválido ou expirado');
        }
        return throwError(() => error);
      })
    );
  }

  getMonthlyUsage(months: number = 12): Observable<{ success: boolean; data: UsageData[] }> {
    return this.http.get<{ success: boolean; data: UsageData[] }>(
      `${this.apiUrl}/admin/usage/monthly?months=${months}`,
      { headers: this.getAuthHeaders() }
    ).pipe(
      catchError((error) => {
        if (error.status === 401) {
          console.error('❌ Erro 401 - Token inválido ou expirado');
        }
        return throwError(() => error);
      })
    );
  }
}

