import {
  Component,
  Inject,
  NgZone,
  OnDestroy,
  OnInit
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AnalyzerService } from '../../services/analyzer.service';
import { PaymentCloseResult } from '../../models/payment-close-result';

export interface MercadoPagoCheckoutModalData {
  publicKey: string;
  amountBRL: number;
  planName: string;
  planId: string;
  userId: string;
  email: string;
  payerEmail?: string;
  cpf?: string | null;
  couponCode?: string | null;
  includeEnglish?: boolean;
  analysisId?: string | null;
  pixAvailable: boolean;
  liveMode: boolean;
}

type MpTab = 'card' | 'pix';

declare global {
  interface Window {
    MercadoPago?: new (publicKey: string, options?: { locale?: string }) => {
      bricks: () => {
        create: (
          brick: string,
          containerId: string,
          settings: Record<string, unknown>
        ) => Promise<{ unmount: () => void }>;
      };
    };
  }
}

@Component({
  selector: 'app-mercadopago-checkout-modal',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  templateUrl: './mercadopago-checkout-modal.component.html',
  styleUrl: './mercadopago-checkout-modal.component.scss'
})
export class MercadoPagoCheckoutModalComponent implements OnInit, OnDestroy {
  activeTab: MpTab = 'card';
  loadingCard = true;
  generatingPix = false;
  pixPolling = false;
  errorMessage = '';
  successMessage = '';
  pixQrCode: string | null = null;
  pixQrCodeBase64: string | null = null;
  private pixPaymentId: string | null = null;
  private brickController: { unmount: () => void } | null = null;
  private pixPollTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: MercadoPagoCheckoutModalData,
    private dialogRef: MatDialogRef<MercadoPagoCheckoutModalComponent>,
    private analyzerService: AnalyzerService,
    private ngZone: NgZone
  ) {}

  ngOnInit(): void {
    void this.initCardBrick();
  }

  ngOnDestroy(): void {
    this.brickController?.unmount();
    this.stopPixPolling();
  }

  setTab(tab: MpTab): void {
    if (tab === 'pix' && !this.data.pixAvailable) {
      return;
    }
    this.activeTab = tab;
    this.errorMessage = '';
  }

  close(): void {
    this.dialogRef.close('cancelled');
  }

  generatePix(): void {
    this.generatingPix = true;
    this.errorMessage = '';

    this.analyzerService
      .createMercadoPagoPix({
        planId: this.data.planId,
        userId: this.data.userId,
        email: this.data.email,
        couponCode: this.data.couponCode,
        cpf: this.data.cpf,
        includeEnglish: this.data.includeEnglish,
        analysisId: this.data.analysisId
      })
      .subscribe({
        next: (res) => {
          this.generatingPix = false;
          if (!res.success) {
            this.errorMessage = res.error || res.message || 'Erro ao gerar PIX';
            return;
          }
          this.pixPaymentId = res.paymentId;
          this.pixQrCode = res.qrCode;
          this.pixQrCodeBase64 = res.qrCodeBase64;
          this.startPixPolling();
        },
        error: (err) => {
          this.generatingPix = false;
          this.errorMessage =
            err.error?.message || err.error?.error || err.message || 'Erro ao gerar PIX';
        }
      });
  }

  copyPixCode(): void {
    if (!this.pixQrCode) {
      return;
    }
    void navigator.clipboard.writeText(this.pixQrCode);
    this.successMessage = 'Código PIX copiado!';
    setTimeout(() => {
      this.successMessage = '';
    }, 2500);
  }

  private async initCardBrick(): Promise<void> {
    try {
      await this.loadMpSdk();
      if (!window.MercadoPago) {
        throw new Error('SDK do Mercado Pago indisponível');
      }

      const mp = new window.MercadoPago(this.data.publicKey, { locale: 'pt-BR' });
      const bricksBuilder = mp.bricks();

      this.brickController = await bricksBuilder.create(
        'cardPayment',
        'mp-card-payment-container',
        {
          initialization: {
            amount: this.data.amountBRL,
            payer: {
              email: this.data.payerEmail || this.data.email
            }
          },
          customization: {
            visual: { style: { theme: 'default' } }
          },
          callbacks: {
            onReady: () => {
              this.ngZone.run(() => {
                this.loadingCard = false;
              });
            },
            onSubmit: (cardFormData: Record<string, unknown>) => {
              return new Promise<void>((resolve, reject) => {
                this.ngZone.run(() => {
                  this.processCard(cardFormData).then(resolve).catch(reject);
                });
              });
            },
            onError: (error: { message?: string }) => {
              this.ngZone.run(() => {
                this.errorMessage = error?.message || 'Erro no formulário de cartão';
                this.loadingCard = false;
              });
            }
          }
        }
      );
    } catch (err: unknown) {
      this.loadingCard = false;
      const message = err instanceof Error ? err.message : 'Erro ao carregar pagamento';
      this.errorMessage = message;
    }
  }

  private processCard(cardFormData: Record<string, unknown>): Promise<void> {
    this.errorMessage = '';

    return new Promise((resolve, reject) => {
      this.analyzerService
        .processMercadoPagoCard({
          planId: this.data.planId,
          userId: this.data.userId,
          email: this.data.email,
          couponCode: this.data.couponCode,
          cpf: this.data.cpf,
          includeEnglish: this.data.includeEnglish,
          analysisId: this.data.analysisId,
          token: String(cardFormData['token'] ?? ''),
          paymentMethodId: String(cardFormData['payment_method_id'] ?? ''),
          issuerId: cardFormData['issuer_id'] != null ? String(cardFormData['issuer_id']) : undefined,
          installments: Number(cardFormData['installments'] ?? 1)
        })
        .subscribe({
          next: (res) => {
            if (res.paid) {
              this.closePaid(res.user?.credits);
              resolve();
              return;
            }
            const msg = res.message || 'Pagamento não aprovado. Tente outro cartão.';
            this.errorMessage = msg;
            reject(new Error(msg));
          },
          error: (err) => {
            const msg =
              err.error?.message || err.error?.error || err.message || 'Erro ao processar cartão';
            this.errorMessage = msg;
            reject(new Error(msg));
          }
        });
    });
  }

  private startPixPolling(): void {
    if (!this.pixPaymentId) {
      return;
    }
    this.stopPixPolling();
    this.pixPolling = true;

    const check = () => {
      this.analyzerService.verifyPayment(this.pixPaymentId!, 'mercadopago').subscribe({
        next: (res) => {
          if (res.paid) {
            this.stopPixPolling();
            this.closePaid(res.user?.credits);
          }
        },
        error: () => {
          // Mantém polling — PIX pode demorar ou webhook chegar depois.
        }
      });
    };

    check();
    this.pixPollTimer = setInterval(check, 4000);
  }

  private stopPixPolling(): void {
    if (this.pixPollTimer) {
      clearInterval(this.pixPollTimer);
      this.pixPollTimer = null;
    }
    this.pixPolling = false;
  }

  private closePaid(credits?: number): void {
    const result: PaymentCloseResult = { paid: true };
    if (credits != null) {
      result.credits = credits;
    }
    this.dialogRef.close(result);
  }

  private loadMpSdk(): Promise<void> {
    if (window.MercadoPago) {
      return Promise.resolve();
    }

    return new Promise((resolve, reject) => {
      const existing = document.querySelector('script[data-mp-sdk="v2"]');
      if (existing) {
        existing.addEventListener('load', () => resolve());
        existing.addEventListener('error', () => reject(new Error('Falha ao carregar SDK MP')));
        if (window.MercadoPago) {
          resolve();
        }
        return;
      }

      const script = document.createElement('script');
      script.src = 'https://sdk.mercadopago.com/js/v2';
      script.async = true;
      script.dataset['mpSdk'] = 'v2';
      script.onload = () => resolve();
      script.onerror = () => reject(new Error('Falha ao carregar SDK do Mercado Pago'));
      document.body.appendChild(script);
    });
  }
}
