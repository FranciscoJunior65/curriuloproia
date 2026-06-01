import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { AnalyzerService } from '../../services/analyzer.service';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AccountCreditsInlineComponent } from '../account-credits-inline/account-credits-inline.component';

interface AnalysisServiceItem {
  key: string;
  label: string;
  usado: boolean;
  pendente: boolean;
  ilimitado?: boolean;
}

interface AnalysisServices {
  itens: AnalysisServiceItem[];
  servicos_pendentes: number;
  pacote_concluido: boolean;
  curriculo_ingles_pago?: boolean;
  curriculo_ingles_gerado?: boolean;
}

interface Analysis {
  id: string;
  id_curriculo: string;
  id_usuario: string;
  id_site_vagas: string;
  score_geral: number;
  pontos_fortes: string[];
  pontos_melhorar: string[];
  palavras_chave_sugeridas: string[];
  recomendacoes: string[];
  resultado_completo?: {
    experiencia: string;
    formacao: string;
    habilidades: string[];
    score: number;
    pontosFortes: string[];
    pontosMelhorar: string[];
    recomendacoes: string[];
  } | null;
  criado_em: string;
  servicos?: AnalysisServices;
  curriculos_importados?: {
    id: string;
    nome_arquivo_original: string;
    tipo_arquivo: string;
    criado_em: string;
  };
  sites_vagas?: {
    id: string;
    nome: string;
    url_base: string;
  };
}

type HistoryFilter = 'all' | 'pending';

@Component({
  selector: 'app-analyses-history',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatMenuModule,
    MatSnackBarModule,
    AccountCreditsInlineComponent
  ],
  templateUrl: './analyses-history.component.html',
  styleUrl: './analyses-history.component.scss'
})
export class AnalysesHistoryComponent implements OnInit {
  analyses: Analysis[] = [];
  loading = false;
  error: string | null = null;
  selectedAnalysis: Analysis | null = null;
  currentUser: any = null;
  userCredits = 0;
  filterMode: HistoryFilter = 'all';
  totalServicosPendentes = 0;
  analisesComPendencias = 0;
  adminEnglishLoading = false;

  constructor(
    private analyzerService: AnalyzerService,
    public router: Router,
    public authService: AuthService,
    private snackBar: MatSnackBar
  ) {}

  get isAdmin(): boolean {
    return this.authService.isAdmin();
  }

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
      this.userCredits = user?.credits || 0;
    });
    this.loadAnalyses();
    this.loadPendingSummary();
  }

  get filteredAnalyses(): Analysis[] {
    if (this.filterMode === 'pending') {
      return this.analyses.filter(a => this.getPendingCount(a) > 0);
    }
    return this.analyses;
  }

  loadAnalyses(): void {
    this.loading = true;
    this.error = null;

    this.analyzerService.getUserAnalyses().subscribe({
      next: (response: any) => {
        this.loading = false;
        if (response.success && response.analyses) {
          this.analyses = response.analyses;
          this.analisesComPendencias = this.analyses.filter(a => this.getPendingCount(a) > 0).length;
        } else {
          this.error = 'Nenhuma análise encontrada';
        }
      },
      error: (err: any) => {
        this.loading = false;
        this.error = err.error?.message || 'Erro ao carregar análises';
      }
    });
  }

  loadPendingSummary(): void {
    this.analyzerService.getPendingServices().subscribe({
      next: (res: any) => {
        if (res?.success) {
          this.totalServicosPendentes = res.totalServicosPendentes ?? 0;
          this.analisesComPendencias = res.analisesComPendencias ?? 0;
        }
      },
      error: () => {}
    });
  }

  setFilter(mode: HistoryFilter): void {
    this.filterMode = mode;
  }

  viewAnalysis(analysis: Analysis): void {
    this.selectedAnalysis = analysis;
  }

  closeDetails(): void {
    this.selectedAnalysis = null;
  }

  getPendingCount(analysis: Analysis): number {
    return analysis.servicos?.servicos_pendentes ?? 0;
  }

  isPackageComplete(analysis: Analysis): boolean {
    return analysis.servicos?.pacote_concluido ?? false;
  }

  hasEnglishPaid(analysis: Analysis): boolean {
    return !!analysis.servicos?.curriculo_ingles_pago;
  }

  hasEnglishGenerated(analysis: Analysis): boolean {
    return !!analysis.servicos?.curriculo_ingles_gerado;
  }

  openEnglish(analysis: Analysis, buy: boolean): void {
    const params: Record<string, string> = { analysisId: analysis.id };
    params['action'] = buy ? 'buy-english' : 'english';
    this.router.navigate(['/'], { queryParams: params });
    this.closeDetails();
  }

  adminGrantEnglishFree(analysis: Analysis, event?: Event): void {
    event?.stopPropagation();
    if (!this.isAdmin) return;
    this.adminEnglishLoading = true;
    this.analyzerService.adminFreeCredits('english', { analysisId: analysis.id }).subscribe({
      next: (res: any) => {
        this.adminEnglishLoading = false;
        if (res?.success) {
          if (analysis.servicos) {
            analysis.servicos.curriculo_ingles_pago = true;
          }
          if (this.selectedAnalysis?.id === analysis.id && this.selectedAnalysis.servicos) {
            this.selectedAnalysis.servicos.curriculo_ingles_pago = true;
          }
          this.snackBar.open(res.message || 'Inglês liberado (admin).', 'OK', { duration: 4000 });
        }
      },
      error: () => {
        this.adminEnglishLoading = false;
        this.snackBar.open('Erro ao liberar inglês.', 'OK', { duration: 3000 });
      }
    });
  }

  openService(analysis: Analysis, serviceKey: string): void {
    const actionMap: Record<string, string> = {
      carta_apresentacao: 'cover-letter',
      entrevista: 'interview',
      curriculo_melhorado: 'improved',
      busca_vagas: 'jobs',
      curriculo_ingles: 'english'
    };
    const params: Record<string, string> = { analysisId: analysis.id };
    if (serviceKey !== 'view' && actionMap[serviceKey]) {
      params['action'] = actionMap[serviceKey];
    }
    this.router.navigate(['/'], { queryParams: params });
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleDateString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  getScoreColor(score: number): string {
    if (score >= 80) return 'text-green-600';
    if (score >= 60) return 'text-yellow-600';
    return 'text-red-600';
  }

  getUserDisplayName(): string {
    return this.currentUser?.name || this.currentUser?.email || 'Usuário';
  }

  getScoreGradient(score: number): string {
    if (score >= 80) {
      return 'linear-gradient(135deg, #10b981 0%, #059669 100%)';
    } else if (score >= 60) {
      return 'linear-gradient(135deg, #f59e0b 0%, #f97316 100%)';
    }
    return 'linear-gradient(135deg, #f43f5e 0%, #ec4899 100%)';
  }
}
