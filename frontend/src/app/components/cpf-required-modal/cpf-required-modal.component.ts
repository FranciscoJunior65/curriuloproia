import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../services/auth.service';
import { formatCpfDisplay, getCpfDigits } from '../../utils/cpf.utils';

@Component({
  selector: 'app-cpf-required-modal',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './cpf-required-modal.component.html',
  styleUrl: './cpf-required-modal.component.scss'
})
export class CpfRequiredModalComponent {
  cpf = '';
  error = '';
  loading = false;

  constructor(
    private authService: AuthService,
    private dialogRef: MatDialogRef<CpfRequiredModalComponent>
  ) {
    const user = this.authService.getCurrentUser();
    if (user?.cpf) {
      this.cpf = formatCpfDisplay(user.cpf);
    }
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

  save(): void {
    const cpfDigits = getCpfDigits(this.cpf);
    if (cpfDigits.length !== 11) {
      this.error = 'Informe um CPF válido com 11 dígitos.';
      return;
    }

    this.loading = true;
    this.error = '';

    this.authService.updateProfile({ cpf: cpfDigits }).subscribe({
      next: (response) => {
        this.loading = false;
        if (response.success) {
          this.dialogRef.close(true);
        } else {
          this.error = response.error || 'Não foi possível salvar o CPF.';
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.error || err.error?.message || 'Erro ao salvar CPF. Tente novamente.';
      }
    });
  }

  close(): void {
    this.dialogRef.close(false);
  }
}
