import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface PaymentSuccessDialogData {
  credits: number;
  delta: number;
}

@Component({
  selector: 'app-payment-success-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './payment-success-dialog.component.html',
  styleUrl: './payment-success-dialog.component.scss'
})
export class PaymentSuccessDialogComponent {
  constructor(
    @Inject(MAT_DIALOG_DATA) public data: PaymentSuccessDialogData,
    private dialogRef: MatDialogRef<PaymentSuccessDialogComponent>
  ) {}

  close(): void {
    this.dialogRef.close();
  }
}
