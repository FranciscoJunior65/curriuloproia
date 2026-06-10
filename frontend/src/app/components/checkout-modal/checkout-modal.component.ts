import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface CheckoutModalData {
  checkoutUrl: string;
  providerLabel: string;
}

@Component({
  selector: 'app-checkout-modal',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './checkout-modal.component.html',
  styleUrl: './checkout-modal.component.scss'
})
export class CheckoutModalComponent implements OnInit, OnDestroy {
  popupBlocked = false;
  popupClosed = false;
  private popup: Window | null = null;
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: CheckoutModalData,
    private dialogRef: MatDialogRef<CheckoutModalComponent>
  ) {}

  ngOnInit(): void {
    this.openPopup();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  openPopup(): void {
    const width = 520;
    const height = Math.min(820, window.screen.availHeight - 40);
    const left = Math.max(0, Math.round((window.screen.width - width) / 2));
    const top = Math.max(0, Math.round((window.screen.height - height) / 2));
    const features = [
      `width=${width}`,
      `height=${height}`,
      `left=${left}`,
      `top=${top}`,
      'scrollbars=yes',
      'resizable=yes',
      'noopener=no',
      'noreferrer=no'
    ].join(',');

    this.popup = window.open(this.data.checkoutUrl, 'curriculospro_checkout', features);
    this.popupBlocked = !this.popup;
    this.popupClosed = false;

    if (this.popup) {
      this.startPolling();
    }
  }

  private startPolling(): void {
    this.stopPolling();
    this.pollTimer = setInterval(() => {
      if (!this.popup || this.popup.closed) {
        this.popupClosed = true;
        this.stopPolling();
        this.dialogRef.close('completed');
      }
    }, 800);
  }

  private stopPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  openInNewTab(): void {
    window.open(this.data.checkoutUrl, '_blank');
  }

  close(): void {
    this.dialogRef.close('cancelled');
  }
}
