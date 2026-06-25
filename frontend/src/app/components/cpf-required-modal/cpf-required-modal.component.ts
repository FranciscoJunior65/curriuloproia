import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';
import { formatCpfDisplay, getCpfDigits, isValidCpf } from '../../utils/cpf.utils';

export interface CpfRequiredModalData {
  mandatory?: boolean;
  context?: 'login' | 'payment';
}

@Component({
  selector: 'app-cpf-required-modal',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './cpf-required-modal.component.html',
  styleUrl: './cpf-required-modal.component.scss'
})
export class CpfRequiredModalComponent {
  cpf = '';
  error = '';
  loading = false;
  readonly mandatory: boolean;
  readonly context: 'login' | 'payment';

  constructor(
    private authService: AuthService,
    private dialogRef: MatDialogRef<CpfRequiredModalComponent>,
    @Inject(MAT_DIALOG_DATA) data: CpfRequiredModalData | null
  ) {
    this.mandatory = data?.mandatory !== false;
    this.context = data?.context ?? 'login';

    const user = this.authService.getCurrentUser();
    if (user?.cpf) {
      this.cpf = formatCpfDisplay(user.cpf);
    }
  }

  get subtitle(): string {
    if (this.context === 'payment') {
      return 'Para concluir o pagamento, cadastre seu CPF. Ele ficará salvo no seu perfil.';
    }
    return 'Cadastre o CPF da sua conta para continuar com cupons e pagamento. Ele ficará vinculado ao seu perfil.';
  }

  onCpfInput(): void {
    this.cpf = formatCpfDisplay(this.cpf);
    this.error = '';
  }

  onCpfKeydown(event: KeyboardEvent): void {
    const key = event.key;
    if (key === 'Backspace' || key === 'Delete' || key === 'Tab' || key === 'ArrowLeft' || key === 'ArrowRight') {
      return;
    }
    if (key.length === 1 && !/\d/.test(key)) {
      event.preventDefault();
    }
  }

  onCpfPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const pasted = event.clipboardData?.getData('text') ?? '';
    this.cpf = formatCpfDisplay(pasted);
    this.error = '';
  }

  save(): void {
    const cpfDigits = getCpfDigits(this.cpf);
    if (cpfDigits.length !== 11) {
      this.error = 'Informe um CPF com 11 dígitos.';
      return;
    }

    if (!isValidCpf(cpfDigits)) {
      this.error = 'CPF inválido. Verifique os números digitados.';
      return;
    }

    this.loading = true;
    this.error = '';

    this.authService.updateProfile({ cpf: cpfDigits }).subscribe({
      next: (response) => {
        this.loading = false;
        if (response.success) {
          this.dialogRef.close(true);
          return;
        }
        this.error = response.error || 'Não foi possível salvar o CPF.';
      },
      error: (err) => {
        this.loading = false;
        this.error =
          err.error?.error ||
          err.error?.message ||
          err.message ||
          'Erro ao salvar CPF. Tente novamente.';
      }
    });
  }

  close(): void {
    if (this.mandatory) {
      return;
    }
    this.dialogRef.close(false);
  }
}
