import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

@Injectable({
  providedIn: 'root'
})
export class PurchaseService {
  private apiUrl = environment.apiUrl;

  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) {}

  downloadPurchaseExport(format: 'json' | 'csv'): void {
    const token = this.authService.getToken();
    if (!token) {
      return;
    }

    const headers = new HttpHeaders().set('Authorization', `Bearer ${token}`);
    this.http
      .get(`${this.apiUrl}/purchase/export?format=${format}`, {
        headers,
        responseType: 'blob',
        observe: 'response'
      })
      .subscribe({
        next: (response) => {
          const blob = response.body;
          if (!blob) {
            return;
          }

          const disposition = response.headers.get('Content-Disposition') ?? '';
          const match = /filename="?([^";]+)"?/i.exec(disposition);
          const filename =
            match?.[1] ??
            (format === 'csv'
              ? `compras-curriculoproia.${format}`
              : `dados-compras-curriculoproia.${format}`);

          const url = URL.createObjectURL(blob);
          const anchor = document.createElement('a');
          anchor.href = url;
          anchor.download = filename;
          anchor.click();
          URL.revokeObjectURL(url);
        },
        error: () => {
          // Erro tratado pelo componente chamador, se necessário.
        }
      });
  }

  exportPurchasesObservable(format: 'json' | 'csv'): Observable<Blob> {
    const token = this.authService.getToken();
    const headers = new HttpHeaders().set('Authorization', `Bearer ${token ?? ''}`);
    return this.http.get(`${this.apiUrl}/purchase/export?format=${format}`, {
      headers,
      responseType: 'blob'
    });
  }
}
