import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { map, catchError, take } from 'rxjs/operators';
import { of } from 'rxjs';

export const authGuard = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const loginUrlTree = router.createUrlTree(['/login']);

  // Verifica se tem token
  const token = authService.getToken();
  
  console.log('🔐 AuthGuard - Verificando autenticação:', {
    hasToken: !!token,
    tokenPreview: token ? token.substring(0, 20) + '...' : null
  });

  if (!token) {
    console.log('🔐 AuthGuard - Token não encontrado, redirecionando para login');
    authService.logout();
    return loginUrlTree;
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
        return loginUrlTree;
      }
    }),
    catchError(error => {
      console.error('🔐 AuthGuard - Erro ao verificar token:', error);
      console.log('🔐 AuthGuard - Falha na verificação, redirecionando para login');
      authService.logout();
      return of(loginUrlTree);
    })
  );
};

