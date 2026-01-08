import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  isLogin = true;
  nome = '';
  email = '';
  senha = '';
  verificationCode = '';
  error = '';
  success = '';
  loading = false;
  
  // Controle de visibilidade de senha
  showPassword = false;
  
  // Estados para verificação de email
  showVerificationStep = false;
  registeredEmail = '';
  resendLoading = false;
  verifyLoading = false;

  // Estados para recuperação de senha
  showForgotPassword = false;
  showResetPassword = false;
  forgotPasswordEmail = '';
  forgotPasswordLoading = false;
  forgotPasswordSuccess = '';
  newPassword = '';
  confirmPassword = '';
  showNewPassword = false;
  showConfirmPassword = false;
  resetPasswordLoading = false;
  resetToken = '';

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  toggleMode() {
    this.isLogin = !this.isLogin;
    this.error = '';
    this.showVerificationStep = false;
    this.registeredEmail = '';
  }

  submit() {
    this.error = '';
    this.loading = true;
    
    if (this.isLogin) {
      this.authService.login(this.email, this.senha).subscribe({
        next: (response) => {
          if (response.success) {
            this.loading = false;
            // Redireciona para a página principal
            this.router.navigate(['/']);
          } else {
            this.error = response.error || 'Erro ao fazer login';
            this.loading = false;
          }
        },
        error: (err) => {
          // Se o erro for email não verificado, mostra etapa de verificação
          if (err.error?.requiresVerification) {
            this.showVerificationStep = true;
            this.registeredEmail = this.email;
            this.error = err.error?.message || 'Por favor, verifique seu email antes de fazer login';
          } else {
            this.error = err.error?.error || err.error?.message || 'Erro ao fazer login';
          }
          this.loading = false;
        }
      });
    } else {
      if (!this.nome) {
        this.error = 'Nome é obrigatório';
        this.loading = false;
        return;
      }
      
      this.authService.register(this.email, this.senha, this.nome).subscribe({
        next: (response) => {
          if (response.success) {
            // Se precisar verificar email, mostra etapa de verificação
            if (response.requiresVerification) {
              this.showVerificationStep = true;
              this.registeredEmail = this.email;
              this.error = '';
            } else {
              // Se não precisar verificar, redireciona
              this.router.navigate(['/']);
            }
          } else {
            this.error = response.error || 'Erro ao registrar';
          }
          this.loading = false;
        },
        error: (err) => {
          // Se o email já existe mas não está verificado, mostra etapa de verificação
          if (err.error?.requiresVerification) {
            this.showVerificationStep = true;
            this.registeredEmail = this.email;
            this.error = err.error?.message || 'Email já cadastrado. Por favor, verifique seu email.';
          } else {
            this.error = err.error?.error || err.error?.message || 'Erro ao registrar';
          }
          this.loading = false;
        }
      });
    }
  }

  verifyEmail() {
    if (!this.verificationCode || this.verificationCode.length !== 6) {
      this.error = 'Código de verificação deve ter 6 dígitos';
      return;
    }

    this.verifyLoading = true;
    this.error = '';

    this.authService.verifyEmail(this.registeredEmail, this.verificationCode).subscribe({
      next: (response) => {
        if (response.success) {
          this.verifyLoading = false;
          this.router.navigate(['/']);
        } else {
          this.error = response.error || 'Código inválido';
          this.verifyLoading = false;
        }
      },
      error: (err) => {
        this.error = err.error?.error || err.error?.message || 'Erro ao verificar código';
        this.verifyLoading = false;
      }
    });
  }

  resendVerificationCode() {
    this.resendLoading = true;
    this.error = '';

    this.authService.resendVerificationCode(this.registeredEmail).subscribe({
      next: (response) => {
        if (response.success) {
          this.error = 'Código reenviado com sucesso! Verifique seu email.';
        } else {
          this.error = response.error || 'Erro ao reenviar código';
        }
        this.resendLoading = false;
      },
      error: (err) => {
        this.error = err.error?.error || err.error?.message || 'Erro ao reenviar código';
        this.resendLoading = false;
      }
    });
  }

  onVerificationCodeInput(event: any) {
    // Remove caracteres não numéricos e limita a 6 dígitos
    let value = event.target.value.replace(/\D/g, '');
    if (value.length > 6) {
      value = value.substring(0, 6);
    }
    this.verificationCode = value;
    event.target.value = value;
  }

  togglePasswordVisibility() {
    this.showPassword = !this.showPassword;
  }

  requestPasswordReset() {
    if (!this.forgotPasswordEmail) {
      this.error = 'Email é obrigatório';
      return;
    }

    this.forgotPasswordLoading = true;
    this.error = '';
    this.forgotPasswordSuccess = '';

    this.authService.forgotPassword(this.forgotPasswordEmail).subscribe({
      next: (response) => {
        if (response.success) {
          this.forgotPasswordSuccess = 'Link de recuperação enviado! Verifique seu email.';
          this.forgotPasswordLoading = false;
        } else {
          this.error = response.error || 'Erro ao enviar link de recuperação';
          this.forgotPasswordLoading = false;
        }
      },
      error: (err) => {
        this.error = err.error?.error || err.error?.message || 'Erro ao enviar link de recuperação';
        this.forgotPasswordLoading = false;
      }
    });
  }

  resetPassword() {
    this.error = '';
    this.success = '';

    if (!this.newPassword || !this.confirmPassword) {
      this.error = 'Todos os campos são obrigatórios';
      return;
    }

    if (this.newPassword.length < 6) {
      this.error = 'A senha deve ter no mínimo 6 caracteres';
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.error = 'As senhas não coincidem';
      return;
    }

    // Obtém o token da URL ou do estado
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token') || this.resetToken;

    if (!token) {
      this.error = 'Token de recuperação não encontrado. Por favor, solicite um novo link.';
      return;
    }

    this.resetPasswordLoading = true;

    this.authService.resetPassword(token, this.newPassword).subscribe({
      next: (response) => {
        if (response.success) {
          this.error = '';
          this.resetPasswordLoading = false;
          // Redireciona para login após 2 segundos
          setTimeout(() => {
            this.showResetPassword = false;
            this.showForgotPassword = false;
            this.isLogin = true;
            this.error = '';
            this.newPassword = '';
            this.confirmPassword = '';
            // Mostra mensagem de sucesso no login
            this.success = 'Senha redefinida com sucesso! Faça login com sua nova senha.';
            this.error = '';
          }, 2000);
        } else {
          this.error = response.error || 'Erro ao redefinir senha';
          this.resetPasswordLoading = false;
        }
      },
      error: (err) => {
        this.error = err.error?.error || err.error?.message || 'Erro ao redefinir senha. O token pode ter expirado.';
        this.resetPasswordLoading = false;
      }
    });
  }

  ngOnInit() {
    // Verifica se há token de reset na URL
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');
    if (token) {
      this.resetToken = token;
      this.showResetPassword = true;
      this.showForgotPassword = false;
    }
  }
}

