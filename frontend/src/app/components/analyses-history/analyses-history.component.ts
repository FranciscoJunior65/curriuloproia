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
    MatMenuModule
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

  constructor(
    private analyzerService: AnalyzerService,
    public router: Router,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
      this.userCredits = user?.credits || 0;
    });
    this.loadAnalyses();
  }

  loadAnalyses(): void {
    this.loading = true;
    this.error = null;

    this.analyzerService.getUserAnalyses().subscribe({
      next: (response: any) => {
        this.loading = false;
        if (response.success && response.analyses) {
          this.analyses = response.analyses;
        } else {
          this.error = 'Nenhuma análise encontrada';
        }
      },
      error: (err: any) => {
        this.loading = false;
        this.error = err.error?.message || 'Erro ao carregar análises';
        console.error('Erro ao carregar análises:', err);
      }
    });
  }

  viewAnalysis(analysis: Analysis): void {
    this.selectedAnalysis = analysis;
  }

  closeDetails(): void {
    this.selectedAnalysis = null;
  }

  generateCoverLetter(analysis: Analysis): void {
    // Navega para a página de análise com os dados carregados
    this.router.navigate(['/'], {
      queryParams: {
        analysisId: analysis.id,
        action: 'cover-letter'
      }
    });
  }

  startInterview(analysis: Analysis): void {
    // Navega para a página de análise com os dados carregados
    this.router.navigate(['/'], {
      queryParams: {
        analysisId: analysis.id,
        action: 'interview'
      }
    });
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
      return 'linear-gradient(135deg, #10b981 0%, #059669 100%)'; // verde
    } else if (score >= 60) {
      return 'linear-gradient(135deg, #f59e0b 0%, #f97316 100%)'; // amarelo/laranja
    } else {
      return 'linear-gradient(135deg, #f43f5e 0%, #ec4899 100%)'; // vermelho/rosa
    }
  }
}
