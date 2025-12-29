import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-auth-modal',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatIconModule,
    MatTabsModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './auth-modal.component.html',
  styleUrl: './auth-modal.component.scss'
})
export class AuthModalComponent {
  @Output() authSuccess = new EventEmitter<void>();

  loginEmail = '';
  loginPassword = '';
  registerEmail = '';
  registerPassword = '';
  registerName = '';
  verificationCode = '';
  
  loginLoading = false;
  registerLoading = false;
  verifyLoading = false;
  resendLoading = false;
  loginError = '';
  registerError = '';
  verifyError = '';
  
  selectedTab = 0;
  showVerificationStep = false;
  registeredEmail = '';
  
  // Controle de visibilidade de senha
  showLoginPassword = false;
  showRegisterPassword = false;

  constructor(
    private authService: AuthService,
    private dialogRef: MatDialogRef<AuthModalComponent>
  ) {}

  onLogin(): void {
    if (!this.loginEmail || !this.loginPassword) {
      this.loginError = 'Preencha todos os campos';
      return;
    }

    this.loginLoading = true;
    this.loginError = '';

    this.authService.login(this.loginEmail, this.loginPassword).subscribe({
      next: (response) => {
        if (response.success) {
          console.log('Login bem-sucedido:', response.user);
          console.log('user_type após login:', response.user?.user_type);
          this.dialogRef.close();
          this.authSuccess.emit();
        } else {
          this.loginError = 'Erro ao fazer login';
        }
        this.loginLoading = false;
      },
      error: (err) => {
        // Se o erro for email não verificado, mostra mensagem específica
        if (err.error?.requiresVerification) {
          this.loginError = err.error?.message || 'Por favor, verifique seu email antes de fazer login';
          // Opcional: mudar para tab de cadastro e mostrar etapa de verificação
        } else {
          this.loginError = err.error?.error || 'Erro ao fazer login';
        }
        this.loginLoading = false;
      }
    });
  }

  onRegister(): void {
    if (!this.registerEmail || !this.registerPassword) {
      this.registerError = 'Preencha todos os campos obrigatórios';
      return;
    }

    if (this.registerPassword.length < 6) {
      this.registerError = 'Senha deve ter no mínimo 6 caracteres';
      return;
    }

    this.registerLoading = true;
    this.registerError = '';

    this.authService.register(this.registerEmail, this.registerPassword, this.registerName).subscribe({
      next: (response) => {
        if (response.success && response.requiresVerification) {
          // Mostra etapa de verificação
          this.showVerificationStep = true;
          this.registeredEmail = this.registerEmail;
          this.verificationCode = '';
          this.verifyError = '';
        } else if (response.success) {
          // Se não precisa verificação (caso antigo)
          this.dialogRef.close();
          this.authSuccess.emit();
        } else {
          this.registerError = 'Erro ao criar conta';
        }
        this.registerLoading = false;
      },
      error: (err) => {
        this.registerError = err.error?.error || 'Erro ao criar conta';
        this.registerLoading = false;
      }
    });
  }

  onVerifyEmail(): void {
    if (!this.verificationCode || this.verificationCode.length !== 6) {
      this.verifyError = 'Digite o código de 6 dígitos';
      return;
    }

    this.verifyLoading = true;
    this.verifyError = '';

    this.authService.verifyEmail(this.registeredEmail, this.verificationCode).subscribe({
      next: (response) => {
        if (response.success) {
          this.dialogRef.close();
          this.authSuccess.emit();
        } else {
          this.verifyError = 'Erro ao verificar código';
        }
        this.verifyLoading = false;
      },
      error: (err) => {
        this.verifyError = err.error?.error || err.error?.message || 'Código inválido ou expirado';
        this.verifyLoading = false;
      }
    });
  }

  onResendCode(): void {
    this.resendLoading = true;
    this.verifyError = '';

    this.authService.resendVerificationCode(this.registeredEmail).subscribe({
      next: (response) => {
        if (response.success) {
          this.verifyError = '';
          alert('Código reenviado! Verifique seu email.');
        }
        this.resendLoading = false;
      },
      error: (err) => {
        this.verifyError = err.error?.error || 'Erro ao reenviar código';
        this.resendLoading = false;
      }
    });
  }

  close(): void {
    this.dialogRef.close();
  }

  toggleLoginPasswordVisibility(): void {
    this.showLoginPassword = !this.showLoginPassword;
  }

  toggleRegisterPasswordVisibility(): void {
    this.showRegisterPassword = !this.showRegisterPassword;
  }

  onVerificationCodeInput(event: any): void {
    const value = event.target.value;
    // Remove qualquer caractere que não seja número
    this.verificationCode = value.replace(/[^0-9]/g, '');
    // Limita a 6 dígitos
    if (this.verificationCode.length > 6) {
      this.verificationCode = this.verificationCode.substring(0, 6);
    }
  }
}

