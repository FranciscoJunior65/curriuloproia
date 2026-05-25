import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AdminPartner, AdminService } from '../../services/admin.service';
import {
  formatCpfCnpjDisplay,
  getDocumentDigits,
  isValidPartnerDocument
} from '../../utils/documento.utils';

@Component({
  selector: 'app-partner-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './partner-form-dialog.component.html',
  styleUrl: './partner-form-dialog.component.scss'
})
export class PartnerFormDialogComponent {
  nome = '';
  documento = '';
  descricao = '';
  saving = false;
  error = '';

  constructor(
    private adminService: AdminService,
    private dialogRef: MatDialogRef<PartnerFormDialogComponent, AdminPartner | undefined>
  ) {}

  onDocumentoInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = getDocumentDigits(input.value);
    this.documento = digits;
    input.value = formatCpfCnpjDisplay(digits);
  }

  cancel(): void {
    this.dialogRef.close(undefined);
  }

  submit(): void {
    const nome = this.nome.trim();
    const doc = getDocumentDigits(this.documento);
    if (!nome) {
      this.error = 'Informe o nome do parceiro.';
      return;
    }
    if (!isValidPartnerDocument(doc)) {
      this.error = 'Informe um CPF (11 dígitos) ou CNPJ (14 dígitos) válido.';
      return;
    }

    this.saving = true;
    this.error = '';
    this.adminService
      .createPartner({
        nome,
        cpf: doc,
        descricao: this.descricao.trim() || undefined
      })
      .subscribe({
        next: (res) => {
          this.saving = false;
          if (res.success && res.partner) {
            this.dialogRef.close(res.partner);
          } else {
            this.error = 'Não foi possível incluir o parceiro.';
          }
        },
        error: (err) => {
          this.saving = false;
          this.error = err.error?.error || 'Erro ao incluir parceiro';
        }
      });
  }
}
