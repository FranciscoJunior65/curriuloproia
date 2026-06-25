import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { map, catchError, take } from 'rxjs/operators';
import { of } from 'rxjs';

export const authGuard = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Verifica se tem token
  const token = authService.getToken();
  
  console.log('🔐 AuthGuard - Verificando autenticação:', {
    hasToken: !!token,
    tokenPreview: token ? token.substring(0, 20) + '...' : null
  });

  if (!token) {
    console.log('🔐 AuthGuard - Token não encontrado, redirecionando para login');
    router.navigate(['/login']);
    return false;
  }

  // Verifica o token no backend para garantir que não está expirado
  return authService.verifyToken().pipe(
    take(1), // Garante que o Observable completa após a primeira emissão
    map(response => {
      if (response && response.success && response.user) {
        console.log('🔐 AuthGuard - Token válido, permitindo acesso');
        authService.setUser(response.user);
        return true;
      } else {
        console.log('🔐 AuthGuard - Token inválido, redirecionando para login');
        authService.logout();
        router.navigate(['/login']);
        return false;
      }
    }),
    catchError(error => {
      console.error('🔐 AuthGuard - Erro ao verificar token:', error);
      // Se o erro for 401 (não autorizado), o token está expirado ou inválido
      if (error.status === 401 || error.status === 0) {
        console.log('🔐 AuthGuard - Token expirado ou inválido, redirecionando para login');
        authService.logout();
        router.navigate(['/login']);
      }
      return of(false);
    })
  );
};

