import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss'
})
export class ChangePasswordComponent implements OnInit {
  currentPassword: string = '';
  newPassword: string = '';
  confirmPassword: string = '';
  showCurrentPassword: boolean = false;
  showNewPassword: boolean = false;
  showConfirmPassword: boolean = false;
  loading: boolean = false;
  error: string | null = null;
  success: boolean = false;

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Verifica se o usuário está autenticado
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
    }
  }

  togglePasswordVisibility(field: 'current' | 'new' | 'confirm'): void {
    if (field === 'current') {
      this.showCurrentPassword = !this.showCurrentPassword;
    } else if (field === 'new') {
      this.showNewPassword = !this.showNewPassword;
    } else if (field === 'confirm') {
      this.showConfirmPassword = !this.showConfirmPassword;
    }
  }

  onSubmit(): void {
    this.error = null;
    this.success = false;

    // Validações
    if (!this.currentPassword || !this.newPassword || !this.confirmPassword) {
      this.error = 'Todos os campos são obrigatórios';
      return;
    }

    if (this.newPassword.length < 6) {
      this.error = 'A nova senha deve ter no mínimo 6 caracteres';
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.error = 'As senhas não coincidem';
      return;
    }

    if (this.currentPassword === this.newPassword) {
      this.error = 'A nova senha deve ser diferente da senha atual';
      return;
    }

    this.loading = true;

    const token = this.authService.getToken();
    if (!token) {
      this.error = 'Token não encontrado. Faça login novamente.';
      this.loading = false;
      this.authService.logout();
      this.router.navigate(['/login']);
      return;
    }

    const headers = new HttpHeaders().set('Authorization', `Bearer ${token}`);

    this.http.post<any>('http://localhost:3000/api/auth/change-password', {
      currentPassword: this.currentPassword,
      newPassword: this.newPassword
    }, { headers }).subscribe({
      next: (response) => {
        if (response.success) {
          this.success = true;
          this.currentPassword = '';
          this.newPassword = '';
          this.confirmPassword = '';
          this.loading = false;
          // Redireciona após 2 segundos
          setTimeout(() => {
            // Redireciona usando window.location para evitar problemas com o guard
            window.location.href = '/';
          }, 2000);
        } else {
          this.error = response.error || 'Erro ao alterar senha';
          this.loading = false;
        }
      },
      error: (err) => {
        console.error('Erro ao alterar senha:', err);
        if (err.status === 401) {
          this.error = err.error?.error || 'Senha atual incorreta';
        } else if (err.status === 400) {
          this.error = err.error?.error || 'Dados inválidos';
        } else {
          this.error = err.error?.error || err.error?.message || 'Erro ao alterar senha. Tente novamente.';
        }
        this.loading = false;
      }
    });
  }
}
