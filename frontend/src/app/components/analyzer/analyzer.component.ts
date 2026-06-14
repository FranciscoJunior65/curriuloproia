import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subject, takeUntil, distinctUntilChanged } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Router, ActivatedRoute } from '@angular/router';
import { getCpfDigits } from '../../utils/cpf.utils';
import { CheckoutModalComponent } from '../checkout-modal/checkout-modal.component';
import { CpfRequiredModalComponent } from '../cpf-required-modal/cpf-required-modal.component';
import { AnalyzerService, AnalysisResult } from '../../services/analyzer.service';
import { mapPersistedAnalysisToResult } from '../../utils/persisted-analysis.mapper';
import { AuthService, User } from '../../services/auth.service';
import {
  PricingPlansService,
  PublicPlan,
  PriceParts
} from '../../services/pricing-plans.service';
import { SiteHeaderComponent } from '../site-header/site-header.component';
import { VoiceInterviewComponent } from '../voice-interview/voice-interview.component';

@Component({
  selector: 'app-analyzer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    SiteHeaderComponent,
    VoiceInterviewComponent,
    MatCardModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatChipsModule,
    MatMenuModule,
    MatSnackBarModule,
    MatDialogModule
  ],
  templateUrl: './analyzer.component.html',
  styleUrl: './analyzer.component.scss'
})
export class AnalyzerComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private creditsFetchInFlight = false;
  private creditsLoadedForUserId: string | null = null;
  selectedFile: File | null = null;
  loading = false;
  generatingResume = false;
  result: AnalysisResult | null = null;
  error: string | null = null;
  
  // Payment/Plans
  plans: PublicPlan[] = [];
  loadingPlans = false;
  selectedPlan: PublicPlan | null = null;
  userId: string = '';
  userCredits: number = 0;
  englishCredits = 0;
  showPlans = true;
  processingPaymentPlanId: string | null = null;
  checkoutHint: string | null = null;
  adminFreeLoading = false;
  adminFreePlanId: string | null = null;
  includeEnglishResume: { [planId: string]: boolean } = {}; // Checkbox por plano
  englishBundlePriceBRL = 5.9;
  englishStandalonePriceBRL = 17.9;
  couponCode = '';
  validatedCoupon: { nome: string; porcentagem_desconto: number } | null = null;
  couponError = '';
  validatingCoupon = false;
  paymentProvider: 'stripe' | 'mercadopago' = 'stripe';

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
  generatingEnglishPdf = false;
  generatingEnglishWord = false;
  purchasingEnglish = false;
  generatingCoverLetter = false;
  readonly personaImageUrl = 'assets/imagens/avatar.png';
  readonly founderImageUrl = 'assets/imagens/david-oliveira.jpeg';
  readonly supportWhatsapp = '(71) 98309-6865';
  readonly supportWhatsappUrl = 'https://wa.me/5571983096865';
  readonly supportEmail = 'curriculoproia@gmail.com';
  readonly currentYear = new Date().getFullYear();
  resumeChanges: any = null; // Armazena mudanças após geração
  showInterviewChat = false; // Controla exibição do chat (legado texto)
  showVoiceInterview = false; // Entrevista por voz com persona
  voiceInterviewAutoStart = false;
  startingVoiceInterview = false;
  foundJobs: Array<{
    title?: string;
    company?: string;
    url?: string;
    location?: string;
    description?: string;
    site?: string;
    compatibilityScore?: number;
    salary?: string;
    requirements?: string[];
    applyChannels?: Array<{ portal: string; link?: string }>;
    contactHints?: string[];
    postedAt?: string;
    matchedKeywords?: string[];
  }> = [];
  jobSearchMessage: string | null = null;
  searchingJobs = false;
  interviewStarted = false; // Controla se a entrevista foi iniciada
  interviewQuestions: string[] = []; // Perguntas da entrevista
  currentQuestionIndex = 0; // Índice da pergunta atual
  interviewAnswers: any[] = []; // Respostas dadas (histórico completo para exportação)
  currentAnswer = ''; // Resposta atual sendo digitada
  evaluatingAnswer = false; // Flag para loading de avaliação
  waitingForNextQuestion = false; // Flag para controlar se está aguardando próxima pergunta
  simulationId: string | null = null; // ID da simulação no banco
  currentQuestionData: any = null; // Dados da pergunta atual (pergunta, resposta, feedback)
  
  // Propriedades computadas para evitar chamadas repetidas no template
  _canShowAnswerField: boolean = false;
  _canShowNextButton: boolean = false;
  
  // Propriedades para histórico de análises
  interviewSummary: any = null;
  pendingServicesCount = 0;
  pendingAnalysesCount = 0;

  private updateShowPlans(): void {
    if (this.result || this.analysisCompleted) {
      this.showPlans = false;
      return;
    }
    this.showPlans = this.userCredits === 0;
  }

  private scrollToResults(): void {
    setTimeout(() => {
      document.getElementById('results-section')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 200);
  }

  resetAnalysis(): void {
    this.selectedFile = null;
    this.result = null;
    this.error = null;
    this.selectedSiteId = null;
    this.analysisCompleted = false;
    this.resumeChanges = null;
    this.showVoiceInterview = false;
    this.voiceInterviewAutoStart = false;
    this.startingVoiceInterview = false;
    this.foundJobs = [];
    this.jobSearchMessage = null;
    this.searchingJobs = false;
    this.generatingEnglishPdf = false;
    this.generatingEnglishWord = false;
    this.purchasingEnglish = false;
    this.updateShowPlans();
    this.showInterviewChat = false;
    this.interviewStarted = false;
    this.interviewQuestions = [];
    this.currentQuestionIndex = 0;
    this.interviewAnswers = [];
    this.currentAnswer = '';
    this.waitingForNextQuestion = false;
    this.currentQuestionData = null;
    this.simulationId = null;
    this.generatingPDF = false;
    this.generatingWord = false;
    this.generatingCoverLetter = false;
  }

  constructor(
    private analyzerService: AnalyzerService,
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    private pricingPlansService: PricingPlansService,
    private snackBar: MatSnackBar,
    private dialog: MatDialog
  ) {}

  copyApplyLink(link: string | undefined): void {
    if (!link) {
      return;
    }
    navigator.clipboard.writeText(link).then(() => {
      this.snackBar.open('Link copiado', 'OK', { duration: 3000 });
    });
  }

  /** Reduz quebras de linha excessivas vindas dos portais de vagas. */
  normalizeJobText(text: string | undefined): string {
    if (!text) {
      return '';
    }
    return text.replace(/\r\n/g, '\n').replace(/\n{3,}/g, '\n\n').replace(/[ \t]+\n/g, '\n').trim();
  }

  englishBundleSavingsText(): string {
    return this.pricingPlansService.englishBundleSavings(
      this.englishStandalonePriceBRL,
      this.englishBundlePriceBRL
    );
  }

  hasEnglishPaid(): boolean {
    return !!this.result?.servicos?.curriculo_ingles_pago;
  }

  hasEnglishPdfGenerated(): boolean {
    const s = this.result?.servicos;
    if (!s) return false;
    if (s.curriculo_ingles_pdf) return true;
    // Legado: só existia curriculo_ingles_gerado (contava como os dois)
    if (s.curriculo_ingles_gerado && !s.curriculo_ingles_pdf && !s.curriculo_ingles_word) {
      return true;
    }
    return false;
  }

  hasEnglishWordGenerated(): boolean {
    const s = this.result?.servicos;
    if (!s) return false;
    if (s.curriculo_ingles_word) return true;
    if (s.curriculo_ingles_gerado && !s.curriculo_ingles_pdf && !s.curriculo_ingles_word) {
      return true;
    }
    return false;
  }

  hasEnglishGenerated(): boolean {
    return this.hasEnglishPdfGenerated() && this.hasEnglishWordGenerated();
  }

  get interviewAlreadyDone(): boolean {
    const itens = this.result?.servicos?.itens;
    if (!itens?.length) {
      return false;
    }
    const entrevista = itens.find((i: any) => (i.key ?? i.Key) === 'entrevista');
    return !!entrevista?.usado;
  }

  private applyServicos(servicos: any): void {
    if (!this.result || !servicos) {
      return;
    }
    const prev = this.result.servicos;
    const pagoFromApi = servicos.curriculo_ingles_pago ?? servicos.CurriculoInglesPago;
    const pdfFromApi = servicos.curriculo_ingles_pdf ?? servicos.CurriculoInglesPdf;
    const wordFromApi = servicos.curriculo_ingles_word ?? servicos.CurriculoInglesWord;
    const geradoFromApi = servicos.curriculo_ingles_gerado ?? servicos.CurriculoInglesGerado;

    this.result = {
      ...this.result,
      servicos: {
        // Nunca rebaixa "pago" após refresh (API pode vir sem a flag ainda)
        curriculo_ingles_pago: pagoFromApi === true || prev?.curriculo_ingles_pago === true,
        curriculo_ingles_pdf: pdfFromApi === true || prev?.curriculo_ingles_pdf === true,
        curriculo_ingles_word: wordFromApi === true || prev?.curriculo_ingles_word === true,
        curriculo_ingles_gerado:
          geradoFromApi === true ||
          prev?.curriculo_ingles_gerado === true ||
          ((pdfFromApi === true || prev?.curriculo_ingles_pdf) &&
            (wordFromApi === true || prev?.curriculo_ingles_word)),
        itens: servicos.itens ?? servicos.Itens ?? prev?.itens
      }
    };
  }

  private refreshAnalysisServicos(analysisId: string): void {
    this.analyzerService.getAnalysisById(analysisId).subscribe({
      next: (res: any) => {
        if (res?.servicos) {
          this.applyServicos(res.servicos);
        }
      },
      error: () => {}
    });
  }

  formatPriceParts(priceBRL: number): PriceParts {
    return this.pricingPlansService.formatPriceParts(priceBRL);
  }

  planFeatures(plan: PublicPlan): string[] {
    return plan.features?.length ? plan.features : [];
  }

  pricePerAnalysis(plan: PublicPlan): number {
    if (!plan.analyses) return plan.priceBRL;
    return plan.priceBRL / plan.analyses;
  }

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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadComponent(): void {
    // Observa mudanças no usuário autenticado (sem re-buscar créditos a cada setUser)
    this.authService.currentUser$
      .pipe(
        takeUntil(this.destroy$),
        distinctUntilChanged((prev, curr) =>
          prev?.id === curr?.id &&
          prev?.credits === curr?.credits &&
          prev?.user_type === curr?.user_type
        )
      )
      .subscribe(user => {
      this.currentUser = user;
      this.isAuthenticated = !!user;
      this.isAdmin = this.authService.isAdmin();
      
      if (user) {
        this.userId = user.id;
        this.userCredits = user.credits || 0;
        this.updateShowPlans();
        if (this.creditsLoadedForUserId !== user.id) {
          this.creditsLoadedForUserId = user.id;
          this.checkCredits();
        }
      } else {
        this.userId = '';
        this.userCredits = 0;
        this.updateShowPlans();
        this.isAdmin = false;
        this.creditsLoadedForUserId = null;
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
    this.loadPaymentProvider();
    this.loadPendingServices();
    // Verifica se retornou do pagamento
    this.checkPaymentStatus();
    // Verifica se veio do histórico para continuar serviços
    this.checkAnalysisFromHistory();
  }

  loadPaymentProvider(): void {
    this.analyzerService.getPaymentProvider().subscribe({
      next: (res) => {
        if (res.success && res.provider) {
          this.paymentProvider = res.provider === 'mercadopago' ? 'mercadopago' : 'stripe';
        }
      },
      error: () => {
        this.paymentProvider = 'stripe';
      }
    });
  }

  get paymentProviderLabel(): string {
    return this.paymentProvider === 'mercadopago' ? 'Mercado Pago' : 'Stripe';
  }

  loadPendingServices(): void {
    if (!this.isAuthenticated) {
      this.pendingServicesCount = 0;
      this.pendingAnalysesCount = 0;
      return;
    }
    this.analyzerService.getPendingServices().subscribe({
      next: (res: any) => {
        if (res?.success) {
          this.pendingServicesCount = res.totalServicosPendentes ?? 0;
          this.pendingAnalysesCount = res.analisesComPendencias ?? 0;
        }
      },
      error: () => {}
    });
  }

  checkAnalysisFromHistory(): void {
    this.route.queryParams.subscribe(params => {
      const analysisId = params['analysisId'];
      const action = params['action']; // 'cover-letter' ou 'interview'
      
      if (analysisId) {
        console.log('📋 Carregando análise do histórico:', analysisId);
        this.loadAnalysisFromHistory(analysisId, action);
      }
    });
  }

  loadAnalysisFromHistory(analysisId: string, action?: string): void {
    this.loading = true;
    this.error = null;

    this.analyzerService.getAnalysisById(analysisId).subscribe({
      next: (response: any) => {
        this.loading = false;
        if (response.success && response.analysis) {
          const analysis = response.analysis;
          const mapped = mapPersistedAnalysisToResult({
            ...analysis,
            originalText: response.originalText,
            curriculos_importados: analysis.curriculos_importados
          });

          if (response.analysisForServices) {
            mapped.analysis = {
              pontosFortes: response.analysisForServices.pontosFortes ?? mapped.analysis.pontosFortes,
              pontosMelhorar: response.analysisForServices.pontosMelhorar ?? mapped.analysis.pontosMelhorar,
              experiencia: response.analysisForServices.experiencia ?? mapped.analysis.experiencia,
              formacao: response.analysisForServices.formacao ?? mapped.analysis.formacao,
              habilidades: response.analysisForServices.habilidades ?? mapped.analysis.habilidades,
              recomendacoes: response.analysisForServices.recomendacoes ?? mapped.analysis.recomendacoes,
              score: response.analysisForServices.score ?? mapped.analysis.score,
              areaAtuacao: response.analysisForServices.areaAtuacao
            };
          }
          if (response.originalText) {
            mapped.originalText = response.originalText;
          }

          this.result = {
            success: true,
            originalText: mapped.originalText,
            analysis: mapped.analysis,
            resumeId: mapped.resumeId ?? analysis.id_curriculo,
            analysisId: mapped.analysisId ?? analysis.id,
            creditsRemaining: this.userCredits,
            servicos: response.servicos
              ? {
                  curriculo_ingles_pago: response.servicos.curriculo_ingles_pago,
                  curriculo_ingles_gerado: response.servicos.curriculo_ingles_gerado,
                  curriculo_ingles_pdf: response.servicos.curriculo_ingles_pdf,
                  curriculo_ingles_word: response.servicos.curriculo_ingles_word,
                  itens: response.servicos.itens
                }
              : analysis.servicos
                ? {
                    curriculo_ingles_pago: analysis.servicos.curriculo_ingles_pago,
                    curriculo_ingles_gerado: analysis.servicos.curriculo_ingles_gerado,
                    curriculo_ingles_pdf: analysis.servicos.curriculo_ingles_pdf,
                    curriculo_ingles_word: analysis.servicos.curriculo_ingles_word,
                    itens: analysis.servicos.itens
                  }
                : undefined
          };

          this.selectedSiteId = response.siteId ?? analysis.id_site_vagas;
          this.analysisCompleted = true;
          this.updateShowPlans();
          
          // Executa ação solicitada
          if (action === 'cover-letter') {
            setTimeout(() => {
              this.generateCoverLetter();
              this.scrollToResults();
            }, 500);
          } else if (action === 'interview') {
            setTimeout(() => {
              this.openInterviewSimulation();
              this.scrollToResults();
            }, 500);
          } else if (action === 'improved') {
            setTimeout(() => {
              this.generateImprovedResume('pdf');
              this.scrollToResults();
            }, 500);
          } else if (action === 'jobs') {
            setTimeout(() => {
              this.searchJobs();
              this.scrollToResults();
            }, 500);
          } else if (action === 'english' || action === 'buy-english') {
            setTimeout(() => {
              this.scrollToResults();
              if (action === 'english' && this.hasEnglishPaid()) {
                this.generateEnglishResume('pdf');
              }
            }, 500);
          } else {
            this.scrollToResults();
          }
          // voice interview abre via openInterviewSimulation
          
          // Limpa query params
          this.router.navigate([], {
            relativeTo: this.route,
            queryParams: {}
          });
        } else {
          this.error = 'Análise não encontrada';
        }
      },
      error: (err: any) => {
        this.loading = false;
        this.error = err.error?.message || 'Erro ao carregar análise';
        console.error('Erro ao carregar análise:', err);
      }
    });
  }

  loadJobSites(): void {
    this.loadingSites = true;
    this.analyzerService.getJobSites().subscribe({
      next: (response: any) => {
        const sites = response.sites || [];
        this.jobSites = [...sites].sort((a, b) => {
          const aGoogle = /google/i.test(a?.nome || '');
          const bGoogle = /google/i.test(b?.nome || '');
          if (aGoogle && !bGoogle) return -1;
          if (!aGoogle && bGoogle) return 1;
          return (a?.nome || '').localeCompare(b?.nome || '', 'pt-BR');
        });
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
    this.pricingPlansService.getPlans().subscribe({
      next: (response) => {
        const analysis =
          response.analysisPlans?.length
            ? response.analysisPlans
            : (response.plans || []).filter((plan) => plan.id !== 'english');
        this.plans = analysis;
        if (response.englishBundlePriceBRL != null) {
          this.englishBundlePriceBRL = Number(response.englishBundlePriceBRL);
        }
        if (response.englishStandalonePriceBRL != null) {
          this.englishStandalonePriceBRL = Number(response.englishStandalonePriceBRL);
        }
        const english =
          response.englishPlan || (response.plans || []).find((p) => p.id === 'english');
        if (english?.priceBRL != null) {
          this.englishStandalonePriceBRL = Number(english.priceBRL);
        }
        this.loadingPlans = false;
      },
      error: (err) => {
        console.error('Erro ao carregar planos:', err);
        this.loadingPlans = false;
      }
    });
  }

  private syncUserCredits(): void {
    if (!this.currentUser || this.currentUser.credits === this.userCredits) {
      return;
    }
    this.authService.setUser({ ...this.currentUser, credits: this.userCredits });
  }

  checkCredits(): void {
    if (!this.isAuthenticated || !this.userId) {
      this.userCredits = 0;
      this.updateShowPlans();
      return;
    }

    if (this.creditsFetchInFlight) {
      return;
    }

    this.creditsFetchInFlight = true;
    this.analyzerService.getCredits(this.userId).subscribe({
      next: (response: any) => {
        this.creditsFetchInFlight = false;
        const credits = response?.credits ?? 0;
        this.englishCredits = response?.englishCredits ?? 0;
        if (this.userCredits !== credits) {
          this.userCredits = credits;
          this.syncUserCredits();
        }
        this.updateShowPlans();
      },
      error: () => {
        this.creditsFetchInFlight = false;
        this.userCredits = 0;
        this.updateShowPlans();
      }
    });
  }

  selectPlan(plan: PublicPlan): void {
    this.selectedPlan = plan;
    // Inicializa o checkbox se não existir
    if (!this.includeEnglishResume.hasOwnProperty(plan.id)) {
      this.includeEnglishResume[plan.id] = false;
    }
  }

  requestAdminFreeCredits(plan: PublicPlan): void {
    if (!plan || !this.isAdmin) return;
    this.adminFreeLoading = true;
    this.adminFreePlanId = plan.id;
    this.error = null;
    const includeEnglish =
      plan.id !== 'english' && !!this.includeEnglishResume[plan.id];
    this.analyzerService
      .adminFreeCredits(plan.id, { includeEnglish })
      .subscribe({
        next: (response: any) => {
          this.adminFreeLoading = false;
          this.adminFreePlanId = null;
          if (response.success) {
            this.userCredits = response.credits ?? this.userCredits + (plan.analyses || 0);
            if (this.currentUser) this.currentUser.credits = this.userCredits;
            this.authService.setUser({ ...this.currentUser!, credits: this.userCredits });
            this.updateShowPlans();
            this.snackBar.open(
              response.message || 'Compra gratuita aplicada (admin).',
              'OK',
              { duration: 4000 }
            );
          } else {
            this.error = response.error || 'Erro ao adicionar créditos';
          }
        },
        error: (err) => {
          this.adminFreeLoading = false;
          this.adminFreePlanId = null;
          this.error = err.error?.error || err.error?.message || 'Erro ao adicionar créditos';
        }
      });
  }

  adminGrantEnglishFree(): void {
    if (!this.isAdmin || !this.result?.analysisId) {
      return;
    }
    this.adminFreeLoading = true;
    this.adminFreePlanId = 'english';
    this.error = null;
    this.analyzerService
      .adminFreeCredits('english', { analysisId: this.result.analysisId })
      .subscribe({
        next: (response: any) => {
          this.adminFreeLoading = false;
          this.adminFreePlanId = null;
          if (response.success) {
            this.applyServicos({
              ...this.result?.servicos,
              curriculo_ingles_pago: true
            });
            this.snackBar.open(
              response.message || 'Inglês liberado gratuitamente (admin).',
              'OK',
              { duration: 4000 }
            );
          } else {
            this.error = response.error || 'Erro ao liberar inglês';
          }
        },
        error: (err) => {
          this.adminFreeLoading = false;
          this.adminFreePlanId = null;
          this.error = err.error?.error || err.error?.message || 'Erro ao liberar inglês';
        }
      });
  }

  private getUserCpfDigits(): string {
    return getCpfDigits(this.currentUser?.cpf ?? '');
  }

  private promptCpfIfMissing(onReady: () => void, onCancel?: () => void): void {
    if (this.getUserCpfDigits().length === 11) {
      onReady();
      return;
    }

    const ref = this.dialog.open(CpfRequiredModalComponent, {
      width: '100%',
      maxWidth: '440px',
      disableClose: true,
      panelClass: 'cpf-required-modal-panel'
    });

    ref.afterClosed().subscribe((saved) => {
      if (saved && this.getUserCpfDigits().length === 11) {
        onReady();
      } else {
        onCancel?.();
      }
    });
  }

  private openCheckout(checkoutUrl: string): void {
    this.processingPaymentPlanId = null;
    this.checkoutHint = null;

    // Checkout Pro do Mercado Pago: redirect na mesma aba (evita popup + extensões no console).
    if (this.paymentProvider === 'mercadopago') {
      const isSandbox = /sandbox\.mercadopago/i.test(checkoutUrl);
      if (isSandbox) {
        const proceed = window.confirm(
          'Pagamento em SANDBOX (teste)\n\n' +
            '1. E-mail: use SOMENTE o @testuser.com do Comprador de teste (configurado no backend/.env).\n' +
            '   NÃO use Gmail, Outlook ou e-mail real — isso gera "Uma das partes é de teste".\n\n' +
            '2. Cartão DE TESTE (obrigatório): 5031 4332 1540 6351\n' +
            '   CVV 123 | Validade 11/30 | Titular APRO | CPF 12345678909\n' +
            '   Cartão real (ex: terminado em 8720) NÃO funciona no sandbox.\n\n' +
            '3. Aba anônima, sem login na sua conta real do Mercado Pago.\n\n' +
            'Continuar para o checkout?'
        );
        if (!proceed) {
          return;
        }
      } else {
        const proceed = window.confirm(
          'Pagamento REAL (Mercado Pago — produção)\n\n' +
            '• Cobrança de verdade (PIX paga valor real).\n' +
            '• NÃO entre com a conta do VENDEDOR no Mercado Pago — use outro e-mail/convidado.\n' +
            '• CPF cadastrado em Meus dados deve ser válido.\n' +
            '• Erro de CSP no console (TrackBuilder/content.js) é da página do MP — pode ignorar se o QR Code aparecer.\n' +
            '• No localhost os créditos podem não entrar sozinhos (sem webhook); após pagar, volte ao app ou use o servidor publicado.\n\n' +
            'Continuar para o checkout?'
        );
        if (!proceed) {
          return;
        }
      }
      window.location.assign(checkoutUrl);
      return;
    }

    const ref = this.dialog.open(CheckoutModalComponent, {
      data: {
        checkoutUrl,
        providerLabel: this.paymentProviderLabel
      },
      width: '100%',
      maxWidth: '480px',
      disableClose: false,
      panelClass: 'checkout-modal-panel'
    });

    ref.afterClosed().subscribe((result) => {
      if (result === 'completed') {
        this.checkPaymentStatus();
        this.checkCredits();
      }
    });
  }

  purchasePlan(plan: PublicPlan): void {
    if (!plan) return;

    if (!this.isAuthenticated) {
      this.openAuthModal();
      return;
    }

    this.processingPaymentPlanId = plan.id;
    this.checkoutHint = null;
    this.error = null;

    this.promptCpfIfMissing(
      () => this.executePurchasePlan(plan),
      () => {
        this.processingPaymentPlanId = null;
      }
    );
  }

  private executePurchasePlan(plan: PublicPlan): void {
    console.log(`💳 Iniciando pagamento via ${this.paymentProviderLabel}...`, {
      planId: plan.id,
      planName: plan.name,
      creditsAmount: plan.analyses,
      price: plan.priceBRL
    });

    if (!this.userId && this.currentUser?.id) {
      this.userId = this.currentUser.id;
    }

    if (!this.userId) {
      alert('❌ Erro: ID do usuário não encontrado. Faça login novamente.');
      this.processingPaymentPlanId = null;
      return;
    }

    if (plan.id !== 'english' && this.includeEnglishResume[plan.id]) {
      alert('⚠️ Nota: O currículo em inglês será adicionado automaticamente após o pagamento.');
    }

    const userEmail = this.currentUser?.email || '';
    const userCpf = this.getUserCpfDigits();
    const includeEnglish =
      plan.id !== 'english' && !!this.includeEnglishResume[plan.id];

    this.analyzerService.createPaymentSession(
      plan.id,
      this.userId,
      userEmail,
      this.couponCode?.trim() || null,
      userCpf || null,
      includeEnglish,
      plan.id === 'english' ? this.result?.analysisId ?? undefined : undefined
    ).subscribe({
      next: (response: any) => {
        console.log('📦 Resposta da sessão de pagamento:', response);
        if (response.success && response.freeCheckout && response.redirectUrl) {
          console.log('✅ Compra grátis concluída, redirecionando...');
          window.location.href = response.redirectUrl;
          return;
        }
        if (response.success && response.checkoutUrl) {
          console.log(`✅ Abrindo checkout ${this.paymentProviderLabel} em popup...`);
          this.openCheckout(response.checkoutUrl);
        } else {
          this.error = response.error || 'Erro ao criar sessão de pagamento';
          console.error('❌ Erro na resposta:', response);
          alert(`❌ Erro: ${this.error}`);
          this.processingPaymentPlanId = null;
        }
      },
      error: (err) => {
        console.error('❌ Erro completo ao criar sessão de pagamento:', err);
        const errorMessage = err.error?.message || err.error?.error || err.message || 'Erro ao processar compra';
        this.error = errorMessage;
        this.processingPaymentPlanId = null;

        if (err.status === 401) {
          alert('❌ Você precisa estar logado para comprar créditos. Faça login e tente novamente.');
        } else if (err.error?.code === 'CPF_REQUIRED') {
          this.promptCpfIfMissing(() => this.executePurchasePlan(plan));
        } else if (err.status === 404) {
          alert('❌ Usuário não encontrado. Faça logout e login novamente.');
        } else {
          alert(`❌ Erro ao processar compra: ${errorMessage}`);
        }
      }
    });
  }

  validateCoupon(): void {
    if (!this.isAuthenticated) {
      this.openAuthModal();
      return;
    }

    const code = this.couponCode?.trim();
    if (!code) {
      this.couponError = 'Informe o código do cupom.';
      this.validatedCoupon = null;
      return;
    }

    this.promptCpfIfMissing(() => this.runCouponValidation(code));
  }

  private runCouponValidation(code: string): void {
    const cpfDigits = this.getUserCpfDigits();
    if (cpfDigits.length !== 11) {
      this.couponError = 'Cadastre seu CPF no perfil para validar o cupom.';
      this.validatedCoupon = null;
      return;
    }

    this.validatingCoupon = true;
    this.couponError = '';
    this.validatedCoupon = null;

    this.analyzerService.validateCoupon(code, cpfDigits).subscribe({
      next: (res: any) => {
        this.validatingCoupon = false;
        if (res.code === 'CPF_REQUIRED') {
          this.promptCpfIfMissing(() => this.runCouponValidation(code));
          return;
        }
        if (res.valid && res.coupon) {
          this.validatedCoupon = res.coupon;
          this.couponError = '';
        } else {
          this.couponError = res.message || 'Cupom inválido ou já utilizado por este CPF.';
          this.validatedCoupon = null;
        }
      },
      error: () => {
        this.validatingCoupon = false;
        this.couponError = 'Erro ao validar cupom. Tente novamente.';
        this.validatedCoupon = null;
      }
    });
  }

  clearCoupon(): void {
    this.couponCode = '';
    this.validatedCoupon = null;
    this.couponError = '';
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

  /** Redireciona retornos antigos da home para a tela de confirmação de compra. */
  checkPaymentStatus(): void {
    const urlParams = new URLSearchParams(window.location.search);
    if (!urlParams.toString()) {
      return;
    }

    const hasLegacyReturn =
      urlParams.get('free') === '1' ||
      urlParams.get('session_id') ||
      urlParams.get('payment_id') ||
      urlParams.get('collection_id') ||
      urlParams.get('provider') === 'mercadopago' ||
      urlParams.get('status') === 'failure' ||
      urlParams.get('status') === 'pending';

    if (!hasLegacyReturn) {
      return;
    }

    let path = '/compra/sucesso';
    const status = urlParams.get('status') || urlParams.get('collection_status');
    if (status === 'failure' || status === 'rejected') {
      path = '/compra/falha';
    } else if (status === 'pending') {
      path = '/compra/pendente';
    }

    const queryParams: Record<string, string> = {};
    urlParams.forEach((value, key) => {
      queryParams[key] = value;
    });

    this.router.navigate([path], { queryParams, replaceUrl: true });
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
      next: (result: any) => {
        this.result = result;
        if (result.servicos) {
          this.applyServicos(result.servicos);
        }
        this.analysisCompleted = true; // Trava após análise completa
        this.loading = false;
        // Atualiza créditos após análise
        if (result.creditsRemaining !== null && result.creditsRemaining !== undefined) {
          this.userCredits = result.creditsRemaining;
        } else {
          this.userCredits = Math.max(0, this.userCredits - 1);
        }
        this.syncUserCredits();
        this.updateShowPlans();
        this.scrollToResults();
        this.loadPendingServices();
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
      this.selectedSiteId || undefined,
      this.result.analysisId || undefined
    ).subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `curriculo-melhorado.${format === 'pdf' ? 'pdf' : 'docx'}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        if (format === 'pdf') {
          this.generatingPDF = false;
        } else {
          this.generatingWord = false;
        }
      },
      error: (err: any) => {
        this.error = err?.error?.message || 'Erro ao gerar currículo melhorado';
        if (format === 'pdf') {
          this.generatingPDF = false;
        } else {
          this.generatingWord = false;
        }
      }
    });
  }

  purchaseEnglishForAnalysis(): void {
    if (!this.result?.analysisId) {
      this.error = 'É necessário ter uma análise salva para comprar o currículo em inglês.';
      return;
    }
    if (!this.isAuthenticated || !this.userId) {
      this.openAuthModal();
      return;
    }

    this.purchasingEnglish = true;
    this.error = null;

    this.promptCpfIfMissing(
      () => this.executeEnglishPurchase(),
      () => {
        this.purchasingEnglish = false;
      }
    );
  }

  private executeEnglishPurchase(): void {
    if (!this.result?.analysisId || !this.userId) {
      this.purchasingEnglish = false;
      return;
    }

    this.analyzerService
      .createPaymentSession(
        'english',
        this.userId,
        this.currentUser?.email || '',
        null,
        this.getUserCpfDigits() || null,
        false,
        this.result.analysisId
      )
      .subscribe({
        next: (response: any) => {
          this.purchasingEnglish = false;
          if (response.success && response.freeCheckout && response.redirectUrl) {
            window.location.href = response.redirectUrl;
            return;
          }
          if (response.success && response.checkoutUrl) {
            this.openCheckout(response.checkoutUrl);
          } else {
            this.error = response.error || response.message || 'Erro ao iniciar pagamento';
          }
        },
        error: (err: any) => {
          this.purchasingEnglish = false;
          this.error =
            err.error?.message || err.error?.error || 'Erro ao comprar currículo em inglês';
        }
      });
  }

  generateEnglishResume(format: 'pdf' | 'word' = 'pdf'): void {
    if (!this.result) {
      this.error = 'Nenhuma análise disponível';
      return;
    }

    if (!this.hasEnglishPaid()) {
      this.purchaseEnglishForAnalysis();
      return;
    }

    if (format === 'pdf') {
      this.generatingEnglishPdf = true;
    } else {
      this.generatingEnglishWord = true;
    }
    this.error = null;

    this.analyzerService
      .generateEnglishResume(
        this.result.originalText,
        this.result.analysis,
        format,
        this.selectedSiteId || undefined,
        this.result.analysisId || undefined
      )
      .subscribe({
        next: (blob: Blob) => {
          const ext = format === 'pdf' ? 'pdf' : 'docx';
          const url = window.URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `curriculo-ingles.${ext}`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          window.URL.revokeObjectURL(url);
          if (format === 'pdf') {
            this.generatingEnglishPdf = false;
          } else {
            this.generatingEnglishWord = false;
          }
          this.showPlans = false;
          this.analysisCompleted = true;
          if (this.result?.analysisId) {
            const patch: Record<string, boolean> = {
              curriculo_ingles_pago: true
            };
            if (format === 'pdf') {
              patch['curriculo_ingles_pdf'] = true;
            } else {
              patch['curriculo_ingles_word'] = true;
            }
            this.applyServicos({
              ...this.result.servicos,
              ...patch
            });
            if (this.hasEnglishGenerated()) {
              this.applyServicos({
                ...this.result.servicos,
                curriculo_ingles_gerado: true
              });
            }
            this.refreshAnalysisServicos(this.result.analysisId);
          }
        },
        error: (err: any) => {
          if (format === 'pdf') {
            this.generatingEnglishPdf = false;
          } else {
            this.generatingEnglishWord = false;
          }
          if (err.status === 402) {
            this.error =
              err.error?.message ||
              'Compre o currículo em inglês para esta análise antes de gerar.';
            if (!this.isAdmin) {
              this.applyServicos({
                ...this.result?.servicos,
                curriculo_ingles_pago: false
              });
            }
          } else {
            this.error = err?.error?.message || 'Erro ao gerar currículo em inglês';
          }
        }
      });
  }

  downloadInterviewFromServer(): void {
    if (!this.simulationId) {
      alert('ID da simulação não encontrado.');
      return;
    }

    this.loading = true;
    this.analyzerService.downloadInterview(this.simulationId).subscribe({
      next: (blob: Blob) => {
        this.loading = false;
        // Cria link de download
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `entrevista_${new Date().toISOString().split('T')[0]}.txt`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      },
      error: (err: any) => {
        this.loading = false;
        console.error('Erro ao fazer download:', err);
        // Fallback para exportação local
        this.exportInterview();
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
      this.selectedSiteId || undefined,
      this.result.analysisId || undefined
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

    if (!this.result.analysisId) {
      this.error =
        'A busca de vagas exige uma análise paga deste currículo. Conclua a importação e análise ou abra pelo histórico.';
      return;
    }

    if (!this.selectedSiteId) {
      this.error = 'Por favor, selecione um site de vagas para buscar oportunidades';
      return;
    }

    this.searchingJobs = true;
    this.error = null;
    this.foundJobs = [];
    this.jobSearchMessage = null;

    this.analyzerService
      .searchJobs(
        this.result.analysis,
        this.selectedSiteId,
        'Brasil',
        this.result.originalText || undefined,
        this.result.resumeId || undefined,
        this.result.analysisId || undefined
      )
      .subscribe({
        next: (response: any) => {
          this.searchingJobs = false;

          if (!response.success) {
            this.error = response.message || 'Erro ao buscar vagas';
            return;
          }

          const totalFound = response.totalFound ?? response.jobs?.length ?? 0;
          this.foundJobs = response.jobs || [];
          this.jobSearchMessage = response.message || null;

          if (totalFound > 0) {
            this.scrollToResults();
            return;
          }

          this.error = response.message || 'Nenhuma vaga encontrada para o seu perfil.';
        },
        error: (err) => {
          this.searchingJobs = false;
          this.error = err.error?.message || err.error?.error || 'Erro ao buscar vagas';
          console.error('Erro ao buscar vagas:', err);
        }
      });
  }

  openInterviewSimulation(): void {
    if (!this.result) {
      this.error = 'Faça uma análise do currículo antes de simular a entrevista.';
      return;
    }

    if (!this.selectedSiteId) {
      this.error = 'Selecione um site de vagas (ex.: LinkedIn) antes de simular a entrevista.';
      return;
    }

    this.error = null;
    this.startingVoiceInterview = true;
    this.voiceInterviewAutoStart = true;
    this.showVoiceInterview = true;
    this.showInterviewChat = false;
    this.interviewStarted = false;
    this.interviewQuestions = [];
    this.currentQuestionIndex = 0;
    this.interviewAnswers = [];
    this.currentAnswer = '';
    this.waitingForNextQuestion = false;
    this.currentQuestionData = null;
    this.simulationId = null;

    setTimeout(() => {
      document.getElementById('voice-interview-section')?.scrollIntoView({
        behavior: 'smooth',
        block: 'start'
      });
      this.startingVoiceInterview = false;
    }, 200);
  }

  onVoiceInterviewClosed(): void {
    this.showVoiceInterview = false;
    this.voiceInterviewAutoStart = false;
    this.startingVoiceInterview = false;
  }

  onVoiceInterviewCompleted(event: { simulationId: string }): void {
    if (this.result?.servicos?.itens) {
      this.result = {
        ...this.result,
        servicos: {
          ...this.result.servicos,
          itens: this.result.servicos.itens.map((i: any) =>
            (i.key ?? i.Key) === 'entrevista' ? { ...i, usado: true, pendente: false } : i
          )
        }
      };
    }
    if (event.simulationId) {
      this.simulationId = event.simulationId;
    }
  }

  startInterview(): void {
    if (!this.result) {
      this.error = 'Nenhuma análise disponível';
      return;
    }

    this.loading = true;
    this.error = null;

    this.analyzerService.startInterview(
      this.result.originalText,
      this.result.analysis,
      this.selectedSiteId || undefined,
      this.result.resumeId || undefined,
      this.result.analysisId || undefined
    ).subscribe({
      next: (response: any) => {
        this.loading = false;
        
        if (response?.success && response?.questions && response.questions.length > 0) {
          this.interviewQuestions = response.questions;
          this.simulationId = response.simulationId || null;
          this.interviewStarted = true;
          this.currentQuestionIndex = 0;
          this.waitingForNextQuestion = false;
          this.updateUIState();
          console.log(`✅ ${response.questions.length} perguntas geradas`);
          console.log(`🔍 SimulationId recebido: ${this.simulationId}`);
          
          if (!this.simulationId) {
            console.warn('⚠️ ATENÇÃO: simulationId não foi criado. Verifique se userId, resumeId e siteId foram fornecidos.');
          }
        } else {
          this.error = 'Erro ao gerar perguntas da entrevista';
        }
      },
      error: (err: any) => {
        this.error = err?.error?.message || 'Erro ao iniciar entrevista';
        this.loading = false;
        console.error('Erro ao iniciar entrevista:', err);
      }
    });
  }

  submitAnswer(): void {
    if (!this.currentAnswer?.trim()) {
      return;
    }

    if (!this.interviewQuestions || !this.interviewQuestions[this.currentQuestionIndex]) {
      return;
    }

    const currentQuestion = this.interviewQuestions[this.currentQuestionIndex];
    if (!this.result) {
      return;
    }

    this.evaluatingAnswer = true;

    this.analyzerService.evaluateAnswer(
      currentQuestion,
      this.currentAnswer,
      this.result.originalText,
      this.result.analysis,
      this.simulationId || undefined
    ).subscribe({
      next: (response: any) => {
        if (this.evaluatingAnswer !== undefined) {
          this.evaluatingAnswer = false;
        }

        // Salva a resposta no histórico completo
        const answerData = {
          question: currentQuestion,
          answer: this.currentAnswer || '',
          evaluation: response?.evaluation,
          questionIndex: this.currentQuestionIndex || 0
        };
        if (this.interviewAnswers) {
          this.interviewAnswers.push(answerData);
        }

        // Atualiza dados da pergunta atual para exibição
        this.currentQuestionData = answerData;

        // Limpa o campo de resposta
        this.currentAnswer = '';

        // Marca que está aguardando próxima pergunta (não avança automaticamente)
        if (this.waitingForNextQuestion !== undefined) {
          this.waitingForNextQuestion = true;
        }
        this.updateUIState();
      },
      error: (err: any) => {
        if (this.evaluatingAnswer !== undefined) {
          this.evaluatingAnswer = false;
        }
        
        // Se for erro de quota, mostra mensagem amigável e salva resposta mesmo assim
        if (err?.status === 429 || err?.error?.message?.includes('quota') || err?.error?.message?.includes('Quota')) {
          if (this.error !== undefined) {
            this.error = 'Limite de requisições da IA excedido. Aguarde alguns segundos e tente novamente.';
          }
          
          // Salva resposta mesmo sem avaliação completa
          if (this.interviewQuestions && this.currentQuestionIndex !== undefined && this.interviewAnswers) {
            const currentQuestion = this.interviewQuestions[this.currentQuestionIndex];
            const answerData = {
              question: currentQuestion,
              answer: this.currentAnswer || '',
              evaluation: {
                score: 70,
                feedback: 'Resposta recebida. Avaliação completa temporariamente indisponível devido a limite de requisições.',
                strengths: ['Resposta fornecida'],
                improvements: ['Avaliação completa será disponibilizada em breve']
              },
              questionIndex: this.currentQuestionIndex
            };
            this.interviewAnswers.push(answerData);
            this.currentQuestionData = answerData;
            this.currentAnswer = '';
            if (this.waitingForNextQuestion !== undefined) {
              this.waitingForNextQuestion = true;
            }
            this.updateUIState();
          }
        } else {
          if (this.error !== undefined) {
            this.error = err?.error?.message || 'Erro ao avaliar resposta';
          }
          console.error('Erro ao avaliar resposta:', err);
        }
      }
    });
  }

  finishInterview(): void {
    console.log('🔍 Tentando finalizar entrevista:', {
      simulationId: this.simulationId,
      answersCount: this.interviewAnswers.length
    });
    
    if (!this.simulationId) {
      alert('Erro: ID da simulação não encontrado. A entrevista não foi salva no banco de dados.\n\nIsso pode acontecer se você não estiver autenticado ou se faltarem dados necessários.');
      return;
    }
    
    if (this.interviewAnswers.length === 0) {
      alert('Não há respostas para finalizar.');
      return;
    }
    
    this.loading = true;
    this.analyzerService.finishInterview(
      this.simulationId,
      this.interviewAnswers,
      this.result?.analysisId || undefined
    ).subscribe({
      next: (response: any) => {
        this.loading = false;
        console.log('✅ Entrevista finalizada. Score:', response.score);
        
        // Pergunta se quer exportar
        const exportInterview = confirm(`🎉 Entrevista finalizada!\n\nScore médio: ${response.score}/100\n\nTotal de perguntas respondidas: ${this.interviewAnswers.length}\n\nDeseja exportar a entrevista completa agora?`);
        
        if (exportInterview) {
          this.downloadInterviewFromServer();
        } else {
          alert('✅ Entrevista salva! Você pode fazer download depois através do histórico.');
        }
      },
      error: (err: any) => {
        this.loading = false;
        console.error('Erro ao finalizar entrevista:', err);
        this.error = err?.error?.message || 'Erro ao finalizar entrevista';
        alert(`Erro ao finalizar entrevista: ${err?.error?.message || 'Erro desconhecido'}`);
      }
    });
  }

  exportInterview(): void {
    if (this.interviewAnswers.length === 0) {
      alert('Não há dados para exportar.');
      return;
    }

    const summary = this.getInterviewSummary();
    const averageScore = summary ? summary.averageScore : 0;

    // Cria conteúdo do documento
    let content = `========================================\n`;
    content += `SIMULAÇÃO DE ENTREVISTA - RELATÓRIO COMPLETO\n`;
    content += `========================================\n\n`;
    content += `Data: ${new Date().toLocaleString('pt-BR')}\n`;
    content += `Total de Perguntas: ${this.interviewAnswers.length}\n`;
    content += `Score Médio: ${averageScore}/100\n\n`;
    
    if (summary) {
      content += `Estatísticas:\n`;
      content += `- Respostas Boas (≥70): ${summary.goodAnswers}\n`;
      content += `- Respostas Médias (50-69): ${summary.averageAnswers}\n`;
      content += `- Precisam Melhorar (<50): ${summary.poorAnswers}\n\n`;
    }

    content += `========================================\n`;
    content += `PERGUNTAS E RESPOSTAS\n`;
    content += `========================================\n\n`;

    // Adiciona cada pergunta e resposta
    this.interviewAnswers.forEach((answer: any, index: number) => {
      content += `PERGUNTA ${index + 1}:\n`;
      content += `${answer.question}\n\n`;
      content += `RESPOSTA:\n`;
      content += `${answer.answer}\n\n`;
      
      if (answer.evaluation) {
        content += `AVALIAÇÃO:\n`;
        content += `Score: ${answer.evaluation.score}/100\n`;
        content += `Feedback: ${answer.evaluation.feedback}\n`;
        
        if (answer.evaluation.strengths && answer.evaluation.strengths.length > 0) {
          content += `Pontos Fortes:\n`;
          answer.evaluation.strengths.forEach((strength: string) => {
            content += `- ${strength}\n`;
          });
        }
        
        if (answer.evaluation.improvements && answer.evaluation.improvements.length > 0) {
          content += `Pontos a Melhorar:\n`;
          answer.evaluation.improvements.forEach((improvement: string) => {
            content += `- ${improvement}\n`;
          });
        }
      }
      
      content += `\n${'='.repeat(40)}\n\n`;
    });

    // Cria e baixa o arquivo
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `entrevista_${new Date().toISOString().split('T')[0]}.txt`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }

  getCurrentQuestion(): string {
    if (!this.interviewQuestions || this.currentQuestionIndex === undefined) return '';
    return this.interviewQuestions[this.currentQuestionIndex] || '';
  }

  isLastQuestion(): boolean {
    if (!this.interviewQuestions || this.currentQuestionIndex === undefined) return false;
    return this.currentQuestionIndex >= this.interviewQuestions.length - 1;
  }

  getProgress(): number {
    if (!this.interviewQuestions || this.interviewQuestions.length === 0) return 0;
    if (!this.interviewAnswers) return 0;
    // Progresso baseado nas respostas dadas, não na pergunta atual
    return ((this.interviewAnswers.length) / this.interviewQuestions.length) * 100;
  }

  getInterviewSummary(): any {
    if (!this.interviewAnswers || this.interviewAnswers.length === 0) return null;
    
    const scores = this.interviewAnswers.map((a: any) => a.evaluation?.score || 0);
    const averageScore = Math.round(scores.reduce((a: number, b: number) => a + b, 0) / scores.length);
    const minScore = Math.min(...scores);
    const maxScore = Math.max(...scores);
    
    // Conta quantas respostas foram boas (>= 70), médias (50-69) e ruins (< 50)
    const goodAnswers = scores.filter((s: number) => s >= 70).length;
    const averageAnswers = scores.filter((s: number) => s >= 50 && s < 70).length;
    const poorAnswers = scores.filter((s: number) => s < 50).length;
    
    // Encontra a melhor e pior resposta
    const bestAnswerIndex = scores.indexOf(maxScore);
    const worstAnswerIndex = scores.indexOf(minScore);
    
    return {
      totalQuestions: this.interviewAnswers.length,
      averageScore,
      minScore,
      maxScore,
      goodAnswers,
      averageAnswers,
      poorAnswers,
      bestAnswer: this.interviewAnswers[bestAnswerIndex],
      worstAnswer: this.interviewAnswers[worstAnswerIndex]
    };
  }

  nextQuestion(): void {
    // Limpa os dados da pergunta atual (para mostrar apenas a próxima)
    this.currentQuestionData = null;
    
    // Sempre avança para próxima pergunta se ainda houver
    if (this.interviewQuestions && this.currentQuestionIndex !== undefined && 
        this.currentQuestionIndex < this.interviewQuestions.length - 1) {
      this.currentQuestionIndex++;
      this.waitingForNextQuestion = false;
    } else {
      // Se for a última pergunta, apenas marca como não aguardando (mostra resumo)
      this.waitingForNextQuestion = false;
    }
    this.updateUIState();
  }

  updateUIState(): void {
    // Atualiza propriedades computadas para evitar chamadas repetidas no template
    if (this.interviewQuestions && this.currentQuestionIndex !== undefined && this.interviewAnswers) {
      this._canShowAnswerField = !this.waitingForNextQuestion && 
                                 this.currentQuestionIndex < this.interviewQuestions.length &&
                                 this.interviewAnswers.length === this.currentQuestionIndex;
      
      this._canShowNextButton = this.waitingForNextQuestion && 
                                this.interviewAnswers.length < this.interviewQuestions.length;
    } else {
      this._canShowAnswerField = false;
      this._canShowNextButton = false;
    }
  }

  canShowAnswerField(): boolean {
    return this._canShowAnswerField;
  }

  canShowNextButton(): boolean {
    return this._canShowNextButton;
  }
}

