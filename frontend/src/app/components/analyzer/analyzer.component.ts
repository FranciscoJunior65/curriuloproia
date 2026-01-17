import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { AnalyzerService, AnalysisResult } from '../../services/analyzer.service';
import { AuthService, User } from '../../services/auth.service';

@Component({
  selector: 'app-analyzer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatChipsModule,
    MatMenuModule
  ],
  templateUrl: './analyzer.component.html',
  styleUrl: './analyzer.component.scss'
})
export class AnalyzerComponent implements OnInit {
  selectedFile: File | null = null;
  loading = false;
  generatingResume = false;
  result: AnalysisResult | null = null;
  error: string | null = null;
  
  // Payment/Plans
  plans: any[] = [];
  loadingPlans = false;
  selectedPlan: any = null;
  userId: string = '';
  userCredits: number = 0;
  showPlans = true;
  processingPayment = false;
  includeEnglishResume: { [planId: string]: boolean } = {}; // Checkbox por plano

  // Auth
  currentUser: User | null = null;
  isAuthenticated = false;
  isAdmin = false;

  // Job Sites
  jobSites: any[] = [];
  selectedSiteId: string | null = null;
  loadingSites = false;
  analysisCompleted = false; // Flag para travar após análise
  generatingWord = false;
  generatingPDF = false;
  generatingCoverLetter = false;
  resumeChanges: any = null; // Armazena mudanças após geração
  showInterviewChat = false; // Controla exibição do chat

  constructor(
    private analyzerService: AnalyzerService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Verifica o token primeiro antes de carregar o componente
    const token = this.authService.getToken();
    if (token) {
      // Verifica se o token ainda é válido
      this.authService.verifyToken().subscribe({
        next: (response) => {
          if (response.success && response.user) {
            // Token válido, continua carregando
            this.loadComponent();
          } else {
            // Token inválido, redireciona para login
            console.log('🔐 Token inválido, redirecionando para login');
            this.authService.logout();
            this.router.navigate(['/login']);
          }
        },
        error: (error) => {
          // Erro ao verificar token (provavelmente expirado)
          console.error('🔐 Erro ao verificar token:', error);
          if (error.status === 401 || error.status === 0) {
            console.log('🔐 Token expirado ou inválido, redirecionando para login');
            this.authService.logout();
            this.router.navigate(['/login']);
          } else {
            // Outro erro, tenta carregar mesmo assim
            this.loadComponent();
          }
        }
      });
    } else {
      // Sem token, redireciona para login
      console.log('🔐 Sem token, redirecionando para login');
      this.router.navigate(['/login']);
    }
  }

  private loadComponent(): void {
    // Observa mudanças no usuário autenticado
    this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
      this.isAuthenticated = !!user;
      this.isAdmin = this.authService.isAdmin();
      
      // Debug: verifica se o user_type está presente
      if (user) {
        console.log('Usuário atual:', user);
        console.log('user_type:', user.user_type);
        console.log('isAdmin:', this.isAdmin);
      }
      
      if (user) {
        this.userId = user.id;
        this.userCredits = user.credits || 0;
        this.showPlans = this.userCredits === 0;
        this.checkCredits();
      } else {
        this.userId = '';
        this.userCredits = 0;
        this.showPlans = true;
        this.isAdmin = false;
      }
    });
    
    // Verifica também no início se já está logado
    const currentUser = this.authService.getCurrentUser();
    if (currentUser) {
      // Se o usuário não tem user_type, força verificação do token
      if (!currentUser.user_type) {
        console.log('Usuário sem user_type, forçando verificação...');
        this.authService.verifyToken().subscribe({
          next: (response) => {
            if (response.success && response.user) {
              console.log('Usuário atualizado com user_type:', response.user.user_type);
              this.isAdmin = response.user.user_type === 'admin';
            }
          }
        });
      } else {
        this.isAdmin = this.authService.isAdmin();
      }
      console.log('Usuário inicial:', currentUser);
      console.log('isAdmin inicial:', this.isAdmin);
    }

    this.loadPlans();
    this.loadJobSites();
    // Verifica se retornou do pagamento
    this.checkPaymentStatus();
  }

  loadJobSites(): void {
    this.loadingSites = true;
    this.analyzerService.getJobSites().subscribe({
      next: (response: any) => {
        this.jobSites = response.sites || [];
        this.loadingSites = false;
      },
      error: (err) => {
        console.error('Erro ao carregar sites de vagas:', err);
        this.loadingSites = false;
        // Se não conseguir carregar, continua sem sites (modo compatibilidade)
        this.jobSites = [];
      }
    });
  }

  loadPlans(): void {
    this.loadingPlans = true;
    this.analyzerService.getPlans().subscribe({
      next: (response: any) => {
        // Filtra o plano de inglês (será adicionado depois)
        this.plans = (response.plans || []).filter((plan: any) => plan.id !== 'english');
        this.loadingPlans = false;
      },
      error: (err) => {
        console.error('Erro ao carregar planos:', err);
        this.loadingPlans = false;
      }
    });
  }

  checkCredits(): void {
    if (!this.isAuthenticated || !this.userId) {
      this.userCredits = 0;
      this.showPlans = true;
      return;
    }

    this.analyzerService.getCredits(this.userId).subscribe({
      next: (response: any) => {
        this.userCredits = response.credits || 0;
        this.showPlans = this.userCredits === 0;
        // Atualiza créditos no usuário atual
        if (this.currentUser) {
          this.currentUser.credits = this.userCredits;
        }
      },
      error: () => {
        // Se não encontrar usuário, mostra planos
        this.userCredits = 0;
        this.showPlans = true;
      }
    });
  }

  selectPlan(plan: any): void {
    this.selectedPlan = plan;
    // Inicializa o checkbox se não existir
    if (!this.includeEnglishResume.hasOwnProperty(plan.id)) {
      this.includeEnglishResume[plan.id] = false;
    }
  }

  purchasePlan(plan: any): void {
    if (!plan) return;

    // Verifica se está autenticado
    if (!this.isAuthenticated) {
      this.openAuthModal();
      return;
    }

    this.processingPayment = true;
    this.error = null;

    // Usa compra mockada (para testes - não redireciona para Stripe)
    console.log('🛒 Iniciando compra mockada...', {
      planId: plan.id,
      planName: plan.name,
      creditsAmount: plan.analyses,
      price: plan.priceBRL
    });

    // Envia userId no body para a compra mockada (permite testar mesmo com token expirado)
    if (!this.userId && this.currentUser?.id) {
      this.userId = this.currentUser.id;
    }
    
    console.log('👤 userId para compra:', this.userId);
    
    if (!this.userId) {
      alert('❌ Erro: ID do usuário não encontrado. Faça login novamente.');
      this.processingPayment = false;
      return;
    }
    
    // Calcula preço total (incluindo currículo em inglês se selecionado)
    let totalPrice = plan.priceBRL;
    let includeEnglish = false;
    
    if (plan.id !== 'english' && this.includeEnglishResume[plan.id]) {
      totalPrice += 5.90; // Preço promocional quando comprado junto
      includeEnglish = true;
    }
    
    this.analyzerService.createMockPurchase(
      plan.id,
      plan.name,
      plan.analyses,
      totalPrice,
      this.userId, // Envia userId explicitamente
      includeEnglish // Flag para incluir currículo em inglês
    ).subscribe({
      next: (response: any) => {
        console.log('📦 Resposta da compra:', response);
        if (response.success) {
          // Atualiza créditos do usuário
          this.userCredits = response.user.credits || 0;
          this.showPlans = false;
          
          // Atualiza créditos no usuário atual
          if (this.currentUser) {
            this.currentUser.credits = this.userCredits;
          }
          
          // Recarrega créditos
          this.checkCredits();
          
          // Reseta o checkbox de inglês para este plano
          if (this.includeEnglishResume.hasOwnProperty(plan.id)) {
            this.includeEnglishResume[plan.id] = false;
          }
          
          // Mostra mensagem de sucesso
          console.log('✅ Compra realizada com sucesso!', response);
          alert(`✅ Compra realizada com sucesso! Você recebeu ${plan.analyses} crédito(s).`);
        } else {
          this.error = response.error || 'Erro ao processar compra';
          console.error('❌ Erro na resposta:', response);
          alert(`❌ Erro: ${this.error}`);
        }
        this.processingPayment = false;
      },
      error: (err) => {
        console.error('❌ Erro completo ao comprar créditos:', err);
        console.error('Status:', err.status);
        console.error('Mensagem:', err.error);
        
        const errorMessage = err.error?.message || err.error?.error || err.message || 'Erro ao processar compra';
        this.error = errorMessage;
        this.processingPayment = false;
        
        // Mostra alerta com detalhes do erro
        if (err.status === 401) {
          alert('❌ Você precisa estar logado para comprar créditos. Faça login e tente novamente.');
        } else if (err.status === 404) {
          alert('❌ Usuário não encontrado. Faça logout e login novamente.');
        } else if (err.status === 500 && err.error?.message?.includes('Tabela')) {
          alert('❌ Erro no servidor: A tabela de compras não foi criada. Entre em contato com o suporte.');
        } else {
          alert(`❌ Erro ao processar compra: ${errorMessage}`);
        }
      }
    });
  }

  openLogin(): void {
    this.router.navigate(['/login']);
  }

  openAuthModal(): void {
    // Redireciona para a tela de login
    this.router.navigate(['/login']);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
    this.userCredits = 0;
    this.showPlans = true;
  }

  getUserDisplayName(): string {
    if (this.currentUser?.name) {
      return this.currentUser.name;
    }
    if (this.currentUser?.email) {
      return this.currentUser.email.split('@')[0];
    }
    return 'Usuário';
  }

  getSelectedSiteName(): string | null {
    if (!this.selectedSiteId) return null;
    if (this.selectedSiteId === 'generic') return 'Análise Genérica';
    const site = this.jobSites.find(s => s.id === this.selectedSiteId);
    return site ? site.nome : null;
  }

  // Método para verificar pagamento após retorno do Stripe
  checkPaymentStatus(): void {
    const urlParams = new URLSearchParams(window.location.search);
    const sessionId = urlParams.get('session_id');
    const userId = urlParams.get('userId');

    if (sessionId && userId) {
      this.analyzerService.verifyPayment(sessionId).subscribe({
        next: (response: any) => {
          if (response.success && response.paid) {
            this.userCredits = response.user.credits;
            this.showPlans = false;
            this.checkCredits();
            // Remove parâmetros da URL
            window.history.replaceState({}, document.title, window.location.pathname);
          }
        },
        error: (err) => {
          console.error('Erro ao verificar pagamento:', err);
        }
      });
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectedFile = input.files[0];
      this.error = null;
      this.result = null;
      this.selectedSiteId = null; // Reseta seleção de site ao trocar arquivo
      this.analysisCompleted = false; // Reseta flag ao trocar arquivo
      this.resumeChanges = null; // Limpa mudanças anteriores
    }
  }

  analyze(): void {
    if (!this.selectedFile) {
      this.error = 'Por favor, selecione um arquivo';
      return;
    }

    // Verifica créditos antes de analisar
    if (this.userCredits === 0) {
      this.error = 'Você não possui créditos. Por favor, adquira um plano primeiro.';
      this.showPlans = true;
      return;
    }

    // Valida se site foi selecionado
    if (!this.selectedSiteId) {
      this.error = 'Por favor, selecione um site de vagas antes de analisar';
      return;
    }

    this.loading = true;
    this.error = null;
    this.result = null;

    this.analyzerService.analyzeResume(this.selectedFile, this.selectedSiteId || undefined).subscribe({
      next: (result) => {
        this.result = result;
        this.analysisCompleted = true; // Trava após análise completa
        this.loading = false;
        // Atualiza créditos após análise
        if (result.creditsRemaining !== null && result.creditsRemaining !== undefined) {
          this.userCredits = result.creditsRemaining;
        } else {
          this.userCredits = Math.max(0, this.userCredits - 1);
        }
        // Atualiza créditos no usuário atual
        if (this.currentUser) {
          this.currentUser.credits = this.userCredits;
        }
        this.checkCredits();
      },
      error: (err) => {
        if (err.status === 401) {
          this.error = 'É necessário estar autenticado para analisar currículos.';
          this.openAuthModal();
        } else if (err.status === 402) {
          this.error = 'Créditos insuficientes. Por favor, adquira um plano.';
          this.showPlans = true;
          // Scroll para planos
          setTimeout(() => {
            document.querySelector('.mb-12')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }, 100);
        } else {
          this.error = err.error?.message || 'Erro ao analisar currículo';
        }
        this.loading = false;
      }
    });
  }

  getScoreColor(score: number): string {
    if (score >= 80) return 'text-green-600';
    if (score >= 60) return 'text-yellow-600';
    return 'text-red-600';
  }

  getScoreColorClass(score: number): string {
    if (score >= 80) return 'text-green-300';
    if (score >= 60) return 'text-yellow-300';
    return 'text-red-300';
  }

  getScoreMessage(score: number): string {
    if (score >= 90) return 'Excelente! Seu currículo está muito bem estruturado.';
    if (score >= 80) return 'Muito bom! Algumas melhorias podem aumentar ainda mais seu impacto.';
    if (score >= 70) return 'Bom! Há oportunidades significativas de melhoria.';
    if (score >= 60) return 'Regular. Aplique as recomendações para melhorar substancialmente.';
    return 'Precisa de melhorias. Siga as recomendações para otimizar seu currículo.';
  }

  generateImprovedResume(format: 'pdf' | 'word' = 'pdf'): void {
    if (!this.result) {
      this.error = 'Nenhuma análise disponível';
      return;
    }

    if (format === 'pdf') {
      this.generatingPDF = true;
    } else {
      this.generatingWord = true;
    }
    this.error = null;

    // TODO: Implementar endpoint que aceita formato e siteId
    this.analyzerService.generateImprovedResume(
      this.result.originalText,
      this.result.analysis,
      format,
      this.selectedSiteId || undefined
    ).subscribe({
      next: (response: any) => {
        // Se retornar blob (arquivo)
        if (response instanceof Blob) {
          const url = window.URL.createObjectURL(response);
          const link = document.createElement('a');
          link.href = url;
          link.download = `curriculo-melhorado.${format === 'pdf' ? 'pdf' : 'docx'}`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);
        } else if (response.changes) {
          // Se retornar mudanças
          this.resumeChanges = response;
          // Se também tiver blob, faz download
          if (response.blob) {
            const url = window.URL.createObjectURL(response.blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `curriculo-melhorado.${format === 'pdf' ? 'pdf' : 'docx'}`;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);
          }
        }
        
        if (format === 'pdf') {
          this.generatingPDF = false;
        } else {
          this.generatingWord = false;
        }
      },
      error: (err) => {
        this.error = err.error?.message || 'Erro ao gerar currículo melhorado';
        if (format === 'pdf') {
          this.generatingPDF = false;
        } else {
          this.generatingWord = false;
        }
      }
    });
  }

  generateCoverLetter(): void {
    if (!this.result) {
      this.error = 'Nenhuma análise disponível';
      return;
    }

    if (!this.selectedSiteId) {
      this.error = 'Por favor, selecione um site de vagas para personalizar a carta';
      return;
    }

    this.generatingCoverLetter = true;
    this.error = null;

    this.analyzerService.generateCoverLetter(
      this.result.originalText,
      this.result.analysis,
      this.selectedSiteId || undefined
    ).subscribe({
      next: (blob: Blob) => {
        // Cria um link temporário para download
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'carta-apresentacao.pdf';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        
        this.generatingCoverLetter = false;
      },
      error: (err) => {
        this.error = err.error?.message || 'Erro ao gerar carta de apresentação';
        this.generatingCoverLetter = false;
        console.error('Erro ao gerar carta de apresentação:', err);
      }
    });
  }

  searchJobs(): void {
    if (!this.result) {
      this.error = 'Nenhuma análise disponível';
      return;
    }

    if (!this.selectedSiteId) {
      this.error = 'Por favor, selecione um site de vagas para pesquisar';
      return;
    }

    this.loading = true;
    this.error = null;

    this.analyzerService.searchJobs(
      this.result.analysis,
      this.selectedSiteId,
      'Brasil' // Pode ser expandido para permitir localização customizada
    ).subscribe({
      next: (response: any) => {
        this.loading = false;
        
        if (response.success && response.jobs && response.jobs.length > 0) {
          // Abre as vagas em uma nova aba ou mostra em modal
          // Por enquanto, vamos abrir a URL de busca
          if (response.url) {
            window.open(response.url, '_blank');
          }
          
          // Mostra mensagem com quantidade de vagas
          alert(`✅ ${response.jobs.length} vaga(s) encontrada(s) no ${response.site}!\n\nA busca foi aberta em uma nova aba.`);
        } else if (response.url) {
          // Se não encontrou vagas automaticamente, abre a URL de busca
          window.open(response.url, '_blank');
          alert(`🔍 Busca realizada no ${response.site}!\n\nA página de busca foi aberta em uma nova aba.`);
        } else {
          this.error = response.message || 'Nenhuma vaga encontrada';
        }
      },
      error: (err) => {
        this.error = err.error?.message || 'Erro ao buscar vagas';
        this.loading = false;
        console.error('Erro ao buscar vagas:', err);
      }
    });
  }

  openInterviewSimulation(): void {
    if (!this.result || !this.selectedSiteId) {
      this.error = 'Análise e site são necessários para simulação';
      return;
    }

    this.showInterviewChat = true;
    // TODO: Implementar chat de simulação
  }
}

