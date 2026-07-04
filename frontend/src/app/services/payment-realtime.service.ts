import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export interface PaymentConfirmedEvent {
  userId: string;
  credits: number;
  orderId?: string;
  planId?: string;
  provider: string;
  alreadyFulfilled: boolean;
}

export type PaymentHubConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'error';

@Injectable({
  providedIn: 'root'
})
export class PaymentRealtimeService {
  private connection: signalR.HubConnection | null = null;
  private readonly paymentConfirmed$ = new Subject<PaymentConfirmedEvent>();
  private readonly connectionState$ = new BehaviorSubject<PaymentHubConnectionState>('disconnected');
  private componentListeners = 0;
  private sessionActive = false;
  private connecting: Promise<void> | null = null;

  constructor(private auth: AuthService) {}

  /** Mantém o hub ligado enquanto o usuário estiver logado. */
  ensureSessionConnected(): void {
    this.sessionActive = true;
    void this.ensureConnected();
  }

  /** Encerra a conexão persistente (logout). */
  endSession(): void {
    this.sessionActive = false;
    if (this.componentListeners <= 0) {
      void this.stopConnection();
    }
  }

  /** Observa eventos de pagamento confirmado. */
  connect(): Observable<PaymentConfirmedEvent> {
    this.componentListeners += 1;
    void this.ensureConnected();
    return this.paymentConfirmed$.asObservable();
  }

  /** Remove um listener de componente (ex.: modal fechou). */
  disconnect(): void {
    this.componentListeners = Math.max(0, this.componentListeners - 1);
    if (!this.sessionActive && this.componentListeners <= 0) {
      void this.stopConnection();
    }
  }

  watchConnectionState(): Observable<PaymentHubConnectionState> {
    return this.connectionState$.asObservable();
  }

  getConnectionState(): PaymentHubConnectionState {
    return this.connectionState$.value;
  }

  private hubUrl(): string {
    const base = environment.apiUrl.replace(/\/api\/?$/, '');
    return `${base}/hubs/payment`;
  }

  private shouldStayConnected(): boolean {
    return this.sessionActive || this.componentListeners > 0;
  }

  private async ensureConnected(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (this.connecting) {
      await this.connecting;
      return;
    }

    const token = this.auth.getToken();
    if (!token) {
      return;
    }

    this.connecting = this.startConnection();
    try {
      await this.connecting;
    } finally {
      this.connecting = null;
    }
  }

  private async startConnection(): Promise<void> {
    await this.stopConnection();

    this.connectionState$.next('connecting');

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl(), {
        accessTokenFactory: () => this.auth.getToken() ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(
        environment.production ? signalR.LogLevel.Warning : signalR.LogLevel.Information
      )
      .build();

    this.connection.on('paymentConfirmed', (payload: PaymentConfirmedEvent) => {
      this.paymentConfirmed$.next(payload);
    });

    this.connection.onreconnecting(() => {
      this.connectionState$.next('reconnecting');
      if (!environment.production) {
        console.info('[SignalR] Reconectando hub de pagamento…');
      }
    });

    this.connection.onreconnected(() => {
      this.connectionState$.next('connected');
      if (!environment.production) {
        console.info('[SignalR] Hub de pagamento reconectado.');
      }
    });

    this.connection.onclose(() => {
      if (this.shouldStayConnected()) {
        this.connectionState$.next('reconnecting');
      } else {
        this.connectionState$.next('disconnected');
      }
    });

    try {
      await this.connection.start();
      this.connectionState$.next('connected');
      if (!environment.production) {
        console.info('[SignalR] Hub de pagamento conectado:', this.hubUrl());
      }
    } catch (err) {
      this.connectionState$.next('error');
      console.warn('Hub de pagamento (SignalR) indisponível:', err);
    }
  }

  private async stopConnection(): Promise<void> {
    if (!this.connection) {
      this.connectionState$.next('disconnected');
      return;
    }

    try {
      await this.connection.stop();
    } catch {
      /* ignore */
    }

    this.connection = null;
    this.connectionState$.next('disconnected');
  }
}
