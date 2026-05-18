import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminService, DashboardStats, UsageData, PaymentProvider } from '../../services/admin.service';
import { AuthService } from '../../services/auth.service';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatTabsModule
  ],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss'
})
export class AdminDashboardComponent implements OnInit {
  stats: DashboardStats | null = null;
  dailyUsage: UsageData[] = [];
  monthlyUsage: UsageData[] = [];
  
  loading = true;
  loadingDaily = false;
  loadingMonthly = false;
  
  selectedPeriod = 30; // dias
  selectedMonths = 12; // meses
  isAdmin = false;
  accessDenied = false;

  paymentProvider: PaymentProvider = 'stripe';
  paymentProviders: PaymentProvider[] = ['stripe', 'mercadopago'];
  paymentProviderLabels: Record<PaymentProvider, string> = {
    stripe: 'Stripe',
    mercadopago: 'Mercado Pago'
  };
  loadingPaymentSettings = false;
  savingPaymentProvider = false;
  testingPaymentConnection = false;
  paymentSettingsMessage = '';
  paymentSettingsError = '';
  paymentConnectionMessage = '';
  paymentConnectionSuccess = false;

  constructor(
    private adminService: AdminService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Verifica se o usuário é admin
    if (!this.authService.isAuthenticated()) {
      this.accessDenied = true;
      return;
    }

    this.isAdmin = this.authService.isAdmin();
    
    if (!this.isAdmin) {
      this.accessDenied = true;
      return;
    }

    // Verifica e atualiza o token antes de carregar os dados
    const token = this.authService.getToken();
    if (!token) {
      console.error('❌ Token não encontrado');
      this.authService.logout();
      this.router.navigate(['/']);
      return;
    }

    // Verifica o token primeiro
    this.authService.verifyToken().subscribe({
      next: (response) => {
        if (response.success) {
          console.log('✅ Token verificado, carregando dados...');
          // Se for admin, carrega os dados
          this.loadDashboard();
          this.loadDailyUsage();
          this.loadMonthlyUsage();
          this.loadPaymentProviderSettings();
        } else {
          console.error('❌ Token inválido, redirecionando...');
          alert('Sua sessão expirou. Por favor, faça login novamente.');
          this.authService.logout();
          this.router.navigate(['/']);
        }
      },
      error: (error) => {
        console.error('❌ Erro ao verificar token:', error);
        if (error.status === 401) {
          alert('Sua sessão expirou. Por favor, faça login novamente.');
          this.authService.logout();
          this.router.navigate(['/']);
        } else {
          // Tenta carregar mesmo assim (pode ser um erro de rede)
          this.loadDashboard();
          this.loadDailyUsage();
          this.loadMonthlyUsage();
          this.loadPaymentProviderSettings();
        }
      }
    });
  }

  loadDashboard(): void {
    this.loading = true;
    this.adminService.getDashboardStats().subscribe({
      next: (response) => {
        console.log('Dashboard response:', response);
        if (response.success) {
          this.stats = response.stats;
          console.log('Stats carregadas:', this.stats);
        } else {
          console.error('Resposta sem sucesso:', response);
        }
        this.loading = false;
      },
      error: (error) => {
        console.error('Erro ao carregar dashboard:', error);
        console.error('Erro completo:', JSON.stringify(error, null, 2));
        
        // Se o erro for 401, tenta renovar o token
        if (error.status === 401) {
          console.log('🔄 Token expirado ou inválido, tentando renovar...');
          const token = this.authService.getToken();
          console.log('Token atual:', token ? token.substring(0, 20) + '...' : 'não encontrado');
          
          this.authService.verifyToken().subscribe({
            next: (response) => {
              if (response.success) {
                console.log('✅ Token renovado, tentando carregar dashboard novamente...');
                // Aguarda um pouco antes de tentar novamente
                setTimeout(() => {
                  this.loadDashboard();
                }, 500);
              } else {
                console.error('❌ Não foi possível renovar o token. Faça logout e login novamente.');
                alert('Sua sessão expirou. Por favor, faça login novamente.');
                this.authService.logout();
                this.router.navigate(['/']);
              }
            },
            error: (verifyError) => {
              console.error('❌ Erro ao verificar token:', verifyError);
              alert('Sua sessão expirou. Por favor, faça login novamente.');
              this.authService.logout();
              this.router.navigate(['/']);
            }
          });
        } else {
          this.loading = false;
        }
      }
    });
  }

  loadDailyUsage(): void {
    this.loadingDaily = true;
    this.adminService.getDailyUsage(this.selectedPeriod).subscribe({
      next: (response) => {
        console.log('Daily usage response:', response);
        if (response.success) {
          this.dailyUsage = response.data;
          console.log('Daily usage data:', this.dailyUsage);
        }
        this.loadingDaily = false;
      },
      error: (error) => {
        console.error('Erro ao carregar uso diário:', error);
        console.error('Erro completo:', JSON.stringify(error, null, 2));
        this.loadingDaily = false;
      }
    });
  }

  loadMonthlyUsage(): void {
    this.loadingMonthly = true;
    this.adminService.getMonthlyUsage(this.selectedMonths).subscribe({
      next: (response) => {
        console.log('Monthly usage response:', response);
        if (response.success) {
          this.monthlyUsage = response.data;
          console.log('Monthly usage data:', this.monthlyUsage);
        }
        this.loadingMonthly = false;
      },
      error: (error) => {
        console.error('Erro ao carregar uso mensal:', error);
        console.error('Erro completo:', JSON.stringify(error, null, 2));
        this.loadingMonthly = false;
      }
    });
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(value);
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return new Intl.DateTimeFormat('pt-BR', {
      day: '2-digit',
      month: '2-digit'
    }).format(date);
  }

  formatMonth(monthString: string): string {
    const [year, month] = monthString.split('-');
    const date = new Date(parseInt(year), parseInt(month) - 1);
    return new Intl.DateTimeFormat('pt-BR', {
      month: 'long',
      year: 'numeric'
    }).format(date);
  }

  onPeriodChange(): void {
    this.loadDailyUsage();
  }

  onMonthsChange(): void {
    this.loadMonthlyUsage();
  }

  goHome(): void {
    this.router.navigate(['/']);
  }

  loadPaymentProviderSettings(): void {
    this.loadingPaymentSettings = true;
    this.paymentSettingsError = '';
    this.adminService.getPaymentProvider().subscribe({
      next: (response) => {
        if (response.success) {
          this.paymentProvider = response.provider;
          this.paymentProviders = response.providers || ['stripe', 'mercadopago'];
          if (response.labels) {
            this.paymentProviderLabels = { ...this.paymentProviderLabels, ...response.labels };
          }
        }
        this.loadingPaymentSettings = false;
      },
      error: (error) => {
        this.loadingPaymentSettings = false;
        this.paymentSettingsError = error.error?.message || 'Erro ao carregar meio de pagamento';
      }
    });
  }

  savePaymentProvider(): void {
    this.savingPaymentProvider = true;
    this.paymentSettingsMessage = '';
    this.paymentSettingsError = '';
    this.adminService.updatePaymentProvider(this.paymentProvider).subscribe({
      next: (response) => {
        this.savingPaymentProvider = false;
        if (response.success) {
          this.paymentProvider = response.provider;
          this.paymentSettingsMessage = response.message || 'Meio de pagamento atualizado.';
        }
      },
      error: (error) => {
        this.savingPaymentProvider = false;
        this.paymentSettingsError = error.error?.message || error.error?.error || 'Erro ao salvar';
      }
    });
  }

  testPaymentConnection(): void {
    this.testingPaymentConnection = true;
    this.paymentConnectionMessage = '';
    this.paymentConnectionSuccess = false;
    this.adminService.testPaymentProviderConnection(this.paymentProvider).subscribe({
      next: (response) => {
        this.testingPaymentConnection = false;
        this.paymentConnectionSuccess = response.connected;
        this.paymentConnectionMessage = response.message;
      },
      error: (error) => {
        this.testingPaymentConnection = false;
        this.paymentConnectionSuccess = false;
        this.paymentConnectionMessage = error.error?.message || error.error?.error || 'Erro ao testar conexão';
      }
    });
  }
}

