import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';

interface Purchase {
  id: string;
  planName: string;
  planId: string;
  creditsAmount: number;
  price: number;
  status: string;
  createdAt: string;
  paymentMethod?: string;
  serviceType?: string; // 'analise' ou 'curriculo_ingles'
  parentPurchaseId?: string | null; // Para venda casada
  creditsInfo?: {
    total: number;
    used: number;
    available: number;
    credits?: Array<{
      id: string;
      used: boolean;
      usedAt?: string;
      actionType?: string;
      resumeFileName?: string;
    }> | null;
  } | null;
}

@Component({
  selector: 'app-financeiro',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatMenuModule
  ],
  templateUrl: './financeiro.component.html',
  styleUrl: './financeiro.component.scss'
})
export class FinanceiroComponent implements OnInit {
  purchases: Purchase[] = [];
  expandedPurchase: string | null = null;
  loading = true;
  error: string | null = null;
  userId: string | null = null;
  currentUser: any = null;
  userCredits: number = 0;
  totalCreditsUsed: number = 0;
  totalCreditsActive: number = 0;

  constructor(
    private http: HttpClient,
    public authService: AuthService,
    public router: Router
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
      if (user && user.id) {
        this.userId = user.id;
        this.userCredits = user.credits || 0;
        this.loadPurchases();
      } else {
        this.error = 'Usuário não autenticado. Faça login para ver suas compras.';
        this.loading = false;
      }
    });
  }

  getUserDisplayName(): string {
    return this.currentUser?.name || this.currentUser?.email || 'Usuário';
  }

  loadPurchases(): void {
    if (!this.userId) {
      this.error = 'ID do usuário não disponível.';
      this.loading = false;
      return;
    }

    this.loading = true;
    this.error = null;

    const token = localStorage.getItem('curriculospro_token');
    if (!token) {
      console.error('❌ Token não encontrado no localStorage');
      this.error = 'Token de autenticação não encontrado. Faça login novamente.';
      this.loading = false;
      this.authService.logout();
      this.router.navigate(['/login']);
      return;
    }

    console.log('🔑 Token encontrado, enviando requisição...');
    const headers = new HttpHeaders().set('Authorization', `Bearer ${token}`);

    this.http.get<any>(`http://localhost:3000/api/purchase/history`, { headers }).subscribe({
      next: (response) => {
        console.log('✅ Resposta recebida:', response);
        if (response.success) {
          this.purchases = response.purchases || [];
          console.log('✅ Compras carregadas:', this.purchases.length);
          // Calcula totais de créditos utilizados e ativos
          this.calculateCreditTotals();
        } else {
          this.error = response.message || 'Erro ao carregar histórico de compras.';
        }
        this.loading = false;
      },
      error: (err) => {
        console.error('❌ Erro ao carregar compras:', err);
        console.error('Status:', err.status);
        console.error('Error object:', err.error);
        if (err.status === 401) {
          const errorDetails = err.error?.details || '';
          if (errorDetails.includes('invalid signature')) {
            this.error = 'Sessão inválida. Por favor, faça login novamente.';
          } else {
            this.error = 'Sessão expirada. Faça login novamente.';
          }
          console.log('🔐 Token inválido, limpando dados e redirecionando para login');
          this.authService.logout();
          setTimeout(() => {
            this.router.navigate(['/login']);
          }, 1000);
        } else {
          this.error = err.error?.message || err.error?.error || 'Não foi possível carregar o histórico de compras. Tente novamente mais tarde.';
        }
        this.loading = false;
      }
    });
  }

  getTotalPurchases(): number {
    return this.purchases.length;
  }

  getTotalSpent(): number {
    return this.purchases.reduce((sum, p) => sum + p.price, 0);
  }

  getTotalCreditsAcquired(): number {
    return this.purchases.reduce((sum, p) => sum + (p.creditsAmount || 0), 0);
  }

  calculateCreditTotals(): void {
    this.totalCreditsUsed = this.purchases.reduce((sum, p) => {
      return sum + (p.creditsInfo?.used || 0);
    }, 0);
    
    this.totalCreditsActive = this.purchases.reduce((sum, p) => {
      return sum + (p.creditsInfo?.available || 0);
    }, 0);
  }

  togglePurchase(purchaseId: string): void {
    this.expandedPurchase = this.expandedPurchase === purchaseId ? null : purchaseId;
  }

  isExpanded(purchaseId: string): boolean {
    return this.expandedPurchase === purchaseId;
  }

  buyAgain(purchase: Purchase): void {
    // Navega para a página principal com o planId selecionado
    this.router.navigate(['/'], { 
      queryParams: { 
        planId: purchase.planId,
        buyAgain: 'true'
      } 
    });
  }

  isAnalysisPurchase(purchase: Purchase): boolean {
    return purchase.serviceType !== 'curriculo_ingles' && purchase.creditsAmount > 0;
  }

  isEnglishResumePurchase(purchase: Purchase): boolean {
    return purchase.serviceType === 'curriculo_ingles' || purchase.planId === 'english';
  }

  hasCreditsToShow(purchase: Purchase): boolean {
    return !!(purchase.creditsInfo && purchase.creditsInfo.credits && purchase.creditsInfo.credits.length > 0);
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'completed': return 'text-green-600 bg-green-100';
      case 'pending': return 'text-yellow-600 bg-yellow-100';
      case 'cancelled': return 'text-red-600 bg-red-100';
      default: return 'text-gray-600 bg-gray-100';
    }
  }
}

