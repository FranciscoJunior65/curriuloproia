import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of } from 'rxjs';
import { tap, map, catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface User {
  id: string;
  email: string;
  name: string;
  cpf?: string | null;
  date_of_birth?: string | null;
  city?: string | null;
  country?: string | null;
  credits: number;
  plan?: string;
  user_type?: string;
}

export interface AuthResponse {
  success: boolean;
  token?: string;
  user?: User;
  message?: string;
  error?: string;
  requiresVerification?: boolean;
  referralCoupon?: ReferralCoupon | null;
}

export interface ReferralCoupon {
  couponId: string;
  couponCode: string;
  discountPercent: number;
  partnerId?: string;
  partnerName?: string;
  linkedAt?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = environment.apiUrl;
  private tokenKey = 'curriculospro_token';
  private userKey = 'curriculospro_user';
  static readonly partnerCouponKey = 'curriculospro_partner_cupom';
  
  private currentUserSubject = new BehaviorSubject<User | null>(this.getStoredUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor(private http: HttpClient) {
    // Verifica token ao inicializar
    const token = this.getToken();
    if (token) {
      this.verifyToken().subscribe({
        next: (response) => {
          if (response.success) {
            console.log('AuthService - verifyToken response:', response.user);
            console.log('AuthService - user_type do verifyToken:', response.user?.user_type);
            this.setUser(response.user);
          } else {
            this.logout();
          }
        },
        error: () => {
          this.logout();
        }
      });
    } else {
      // Se não tem token, limpa o usuário armazenado
      const storedUser = this.getStoredUser();
      if (storedUser) {
        console.log('AuthService - Token não encontrado, mas há usuário armazenado. Limpando...');
        this.logout();
      }
    }
  }

  register(email: string, password: string, name?: string, cpf?: string, cupomCodigo?: string): Observable<AuthResponse> {
    const body: Record<string, string> = { email, password };
    if (name) body['name'] = name;
    if (cpf) body['cpf'] = cpf;
    if (cupomCodigo?.trim()) body['cupomCodigo'] = cupomCodigo.trim().toUpperCase();
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/register`, body);
  }

  verifyEmail(email: string, code: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/verify-email`, {
      email,
      code
    }).pipe(
      tap(response => {
        if (response.success && response.token && response.user) {
          this.setToken(response.token);
          this.setUser(response.user);
        }
      })
    );
  }

  resendVerificationCode(email: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/auth/resend-verification`, {
      email
    });
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/login`, {
      email,
      password
    }).pipe(
      tap(response => {
        if (response.success && response.token && response.user) {
          this.setToken(response.token);
          this.setUser(response.user);
        }
      })
    );
  }

  verifyToken(): Observable<any> {
    const token = this.getToken();
    if (!token) {
      return new Observable(observer => {
        observer.next({ success: false });
        observer.complete();
      });
    }

    return this.http.get<any>(`${this.apiUrl}/auth/verify`, {
      headers: {
        'Authorization': `Bearer ${token}`
      }
    });
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    this.currentUserSubject.next(null);
  }

  isAuthenticated(): boolean {
    return !!this.getToken() && !!this.getStoredUser();
  }

  isAdmin(): boolean {
    const user = this.getCurrentUser();
    const isAdmin = user?.user_type === 'admin';
    console.log('isAdmin check:', { user, user_type: user?.user_type, isAdmin });
    return isAdmin;
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  // Método público para atualizar usuário do localStorage (usado pelo guard)
  refreshUserFromStorage(): void {
    const stored = this.getStoredUser();
    if (stored) {
      this.currentUserSubject.next(stored);
    }
  }

  public setToken(token: string): void {
    localStorage.setItem(this.tokenKey, token);
  }

  public setUser(user: User): void {
    console.log('AuthService - setUser chamado com:', user);
    localStorage.setItem(this.userKey, JSON.stringify(user));
    this.currentUserSubject.next(user);
    // Verifica se foi salvo corretamente
    const stored = this.getStoredUser();
    console.log('AuthService - Usuário salvo no localStorage:', stored);
  }

  /** Substitui campos do usuário logado (ex.: créditos sempre vêm do banco, nunca somam). */
  public updateCurrentUser(patch: Partial<User>): void {
    const current = this.getCurrentUser();
    if (!current) {
      return;
    }

    this.setUser({ ...current, ...patch });
  }

  private getStoredUser(): User | null {
    const userStr = localStorage.getItem(this.userKey);
    return userStr ? JSON.parse(userStr) : null;
  }

  forgotPassword(email: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/auth/forgot-password`, {
      email
    });
  }

  resetPassword(token: string, newPassword: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/auth/reset-password`, {
      token,
      newPassword
    });
  }

  requestLoginCode(email: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/auth/request-login-code`, {
      email
    });
  }

  verifyLoginCode(email: string, code: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/verify-login-code`, {
      email,
      code
    }).pipe(
      tap(response => {
        if (response.success && response.token && response.user) {
          this.setToken(response.token);
          this.setUser(response.user);
        }
      })
    );
  }

  /**
   * Atualiza dados do perfil (nome, email, cpf)
   */
  updateProfile(data: { name?: string; email?: string; cpf?: string | null; date_of_birth?: string | null; city?: string | null; country?: string | null }): Observable<{ success: boolean; user?: User; error?: string }> {
    const token = this.getToken();
    if (!token) {
      return new Observable(observer => {
        observer.next({ success: false, error: 'Não autenticado' });
        observer.complete();
      });
    }
    return this.http.patch<{ success: boolean; user?: User; error?: string; message?: string }>(
      `${this.apiUrl}/auth/profile`,
      data,
      { headers: { Authorization: `Bearer ${token}` } }
    ).pipe(
      tap(response => {
        if (response.success && response.user) {
          this.setUser(response.user);
        }
      })
    );
  }

  getReferralCoupon(): Observable<{ success: boolean; referralCoupon?: ReferralCoupon | null }> {
    const token = this.getToken();
    if (!token) {
      return new Observable(observer => {
        observer.next({ success: false });
        observer.complete();
      });
    }
    return this.http.get<{ success: boolean; referralCoupon?: ReferralCoupon | null }>(
      `${this.apiUrl}/auth/referral-coupon`,
      { headers: { Authorization: `Bearer ${token}` } }
    );
  }

  linkPartnerCoupon(cupomCodigo: string): Observable<{ success: boolean; message?: string; error?: string; referralCoupon?: ReferralCoupon | null; alreadyLinked?: boolean }> {
    const token = this.getToken();
    if (!token) {
      return new Observable(observer => {
        observer.next({ success: false, error: 'Não autenticado' });
        observer.complete();
      });
    }
    return this.http.post<{ success: boolean; message?: string; error?: string; referralCoupon?: ReferralCoupon | null; alreadyLinked?: boolean }>(
      `${this.apiUrl}/auth/link-partner-coupon`,
      { cupomCodigo: cupomCodigo.trim().toUpperCase() },
      { headers: { Authorization: `Bearer ${token}` } }
    );
  }

  getPendingPartnerCoupon(): string | null {
    return localStorage.getItem(AuthService.partnerCouponKey);
  }

  setPendingPartnerCoupon(code: string): void {
    localStorage.setItem(AuthService.partnerCouponKey, code.trim().toUpperCase());
  }

  clearPendingPartnerCoupon(): void {
    localStorage.removeItem(AuthService.partnerCouponKey);
  }

  tryLinkPendingPartnerCoupon(): Observable<{ success: boolean; linked?: boolean }> {
    const pending = this.getPendingPartnerCoupon();
    if (!pending || !this.getToken()) {
      return of({ success: true, linked: false });
    }

    return this.linkPartnerCoupon(pending).pipe(
      tap(response => {
        if (response.success) {
          this.clearPendingPartnerCoupon();
        }
      }),
      map(response => ({ success: response.success, linked: response.success && !response.alreadyLinked })),
      catchError(() => of({ success: false, linked: false }))
    );
  }

  deleteAccount(password: string | null, confirmation: string): Observable<{ success: boolean; message?: string; error?: string }> {
    const token = this.getToken();
    if (!token) {
      return new Observable(observer => {
        observer.next({ success: false, error: 'Não autenticado' });
        observer.complete();
      });
    }

    const body: { confirmation: string; password?: string } = { confirmation };
    if (password?.trim()) {
      body.password = password.trim();
    }

    return this.http.post<{ success: boolean; message?: string; error?: string }>(
      `${this.apiUrl}/auth/delete-account`,
      body,
      { headers: { Authorization: `Bearer ${token}` } }
    );
  }
}

