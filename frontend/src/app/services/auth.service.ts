import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { tap } from 'rxjs/operators';

export interface User {
  id: string;
  email: string;
  name: string;
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
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = 'http://localhost:3000/api';
  private tokenKey = 'curriculospro_token';
  private userKey = 'curriculospro_user';
  
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

  register(email: string, password: string, name?: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/register`, {
      email,
      password,
      name
    });
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
    // Se o usuário não tem user_type, tenta buscar do servidor
    if (user && !user.user_type) {
      console.log('Usuário sem user_type, verificando token...');
      this.verifyToken().subscribe({
        next: (response) => {
          if (response.success && response.user) {
            console.log('Atualizando usuário com user_type:', response.user.user_type);
            this.setUser(response.user);
          }
        }
      });
    }
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
}

