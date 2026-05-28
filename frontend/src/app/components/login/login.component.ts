import {
  AfterViewInit,
  Component,
  HostListener,
  OnInit,
  ViewChild,
  ElementRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';
import {
  PriceParts,
  PricingPlansService,
  PublicPlan
} from '../../services/pricing-plans.service';
import { formatCpfDisplay, getCpfDigits } from '../../utils/cpf.utils';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit, AfterViewInit {
  isLogin = true;
  nome = '';
  cpf = '';
  email = '';
  senha = '';
  verificationCode = '';
  error = '';
  success = '';
  loading = false;
  
  // Controle de visibilidade de senha
  showPassword = false;
  
  // Estados para verificação de email
  showVerificationStep = false;
  registeredEmail = '';
  resendLoading = false;
  verifyLoading = false;

  // Estados para recuperação de senha
  showForgotPassword = false;
  showResetPassword = false;
  forgotPasswordEmail = '';
  forgotPasswordLoading = false;
  forgotPasswordSuccess = '';
  newPassword = '';
  confirmPassword = '';
  showNewPassword = false;
  showConfirmPassword = false;
  resetPasswordLoading = false;
  resetToken = '';

  // Estados para login com código
  loginMethod: 'password' | 'code' | 'google' = 'password';
  showLoginCodeStep = false;
  loginCodeEmail = '';
  loginCode = '';
  loginCodeDigits: string[] = ['', '', '', '', '', '']; // Array para os 6 dígitos
  loginCodeLoading = false;
  requestCodeLoading = false;
  codeSent = false; // Flag para controlar se o código foi enviado

  // Proteção de dados
  acceptPrivacyPolicy = false;

  @ViewChild('loginVideo') loginVideoRef?: ElementRef<HTMLVideoElement>;
  @ViewChild('loginVideoMedia') loginVideoMediaRef?: ElementRef<HTMLElement>;
  videoPlaying = false;
  videoExpanded = false;
  wideLayout = false;
  readonly loginVideoSrc = 'assets/videos/video.mp4';
  private static readonly WIDE_LAYOUT_MIN_PX = 768;

  analysisPlans: PublicPlan[] = [];
  englishPlan: PublicPlan | null = null;
  loadingPricing = true;
  readonly apiBaseUrl = environment.apiUrl.replace(/\/api\/?$/, '');

  constructor(
    private authService: AuthService,
    private router: Router,
    private pricingPlansService: PricingPlansService
  ) {}

  priceParts(priceBRL: number): PriceParts {
    return this.pricingPlansService.formatPriceParts(priceBRL);
  }

  planTagline(plan: PublicPlan): string {
    if (plan.savings) return plan.savings;
    if (plan.id === 'single') return 'Perfeito para começar';
    if (plan.id === 'pack3') return 'Melhor custo-benefício';
    return '';
  }

  isPopularPlan(plan: PublicPlan): boolean {
    return plan.id === 'pack3';
  }

  private loadPricingPlans(): void {
    this.loadingPricing = true;
    this.pricingPlansService.clearCache();
    this.pricingPlansService.getPlans().subscribe({
      next: (response) => {
        const fromApi =
          response.analysisPlans?.length
            ? response.analysisPlans
            : (response.plans || []).filter((p) => p.id !== 'english');
        this.analysisPlans = fromApi.length ? fromApi : [];
        this.englishPlan =
          response.englishPlan ||
          (response.plans || []).find((p) => p.id === 'english') ||
          null;
        this.loadingPricing = false;
      },
      error: () => {
        this.loadingPricing = false;
      }
    });
  }

  toggleLoginVideo(): void {
    const video = this.loginVideoRef?.nativeElement;
    if (!video) return;

    if (video.paused) {
      video.muted = false;
      void video.play();
    } else {
      video.pause();
    }
  }

  onLoginVideoReady(): void {
    if (!this.videoPlaying) {
      this.initLoginVideo();
    }
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.updateWideLayout();
    setTimeout(() => this.initLoginVideo(), 100);
  }

  @HostListener('document:fullscreenchange')
  @HostListener('document:webkitfullscreenchange')
  onDocumentFullscreenChange(): void {
    const active = this.isNativeFullscreenActive();
    if (!active) {
      this.videoExpanded = false;
      document.body.style.overflow = '';
    } else {
      this.videoExpanded = true;
    }
  }

  toggleVideoFullscreen(): void {
    if (this.videoExpanded || this.isNativeFullscreenActive()) {
      this.exitVideoFullscreen();
      return;
    }
    this.enterVideoFullscreen();
  }

  exitVideoFullscreen(): void {
    const video = this.loginVideoRef?.nativeElement as
      | (HTMLVideoElement & { webkitDisplayingFullscreen?: boolean })
      | undefined;

    if (document.fullscreenElement) {
      void document.exitFullscreen?.();
    }

    if (video?.webkitDisplayingFullscreen && (video as HTMLVideoElement & { webkitExitFullscreen?: () => void }).webkitExitFullscreen) {
      (video as HTMLVideoElement & { webkitExitFullscreen: () => void }).webkitExitFullscreen();
    }

    this.videoExpanded = false;
    document.body.style.overflow = '';
  }

  private enterVideoFullscreen(): void {
    const media = this.loginVideoMediaRef?.nativeElement;
    const video = this.loginVideoRef?.nativeElement;
    if (!media || !video) {
      return;
    }

    if (!video.paused) {
      void video.play();
    }

    const iosVideo = video as HTMLVideoElement & { webkitEnterFullscreen?: () => void };
    if (typeof iosVideo.webkitEnterFullscreen === 'function') {
      try {
        iosVideo.webkitEnterFullscreen();
        this.videoExpanded = true;
        document.body.style.overflow = 'hidden';
        return;
      } catch {
        // fallback abaixo
      }
    }

    const mediaEl = media as HTMLElement & {
      requestFullscreen?: () => Promise<void>;
      webkitRequestFullscreen?: () => void;
    };

    if (mediaEl.requestFullscreen) {
      void mediaEl.requestFullscreen().then(() => {
        this.videoExpanded = true;
        document.body.style.overflow = 'hidden';
      }).catch(() => {
        this.videoExpanded = true;
        document.body.style.overflow = 'hidden';
      });
      return;
    }

    if (mediaEl.webkitRequestFullscreen) {
      mediaEl.webkitRequestFullscreen();
      this.videoExpanded = true;
      document.body.style.overflow = 'hidden';
      return;
    }

    this.videoExpanded = true;
    document.body.style.overflow = 'hidden';
  }

  private isNativeFullscreenActive(): boolean {
    const media = this.loginVideoMediaRef?.nativeElement;
    const video = this.loginVideoRef?.nativeElement as
      | (HTMLVideoElement & { webkitDisplayingFullscreen?: boolean })
      | undefined;

    return (
      !!document.fullscreenElement ||
      document.fullscreenElement === media ||
      !!video?.webkitDisplayingFullscreen
    );
  }

  private updateWideLayout(): void {
    this.wideLayout =
      typeof window !== 'undefined' &&
      window.innerWidth >= LoginComponent.WIDE_LAYOUT_MIN_PX;
  }

  private initLoginVideo(): void {
    const video = this.loginVideoRef?.nativeElement;
    if (!video) {
      return;
    }

    video.muted = true;
    video.playsInline = true;
    video.setAttribute('playsinline', '');
    video.setAttribute('webkit-playsinline', '');

    const playPromise = video.play();
    if (playPromise) {
      playPromise
        .then(() => {
          this.videoPlaying = !video.paused;
        })
        .catch(() => {
          this.videoPlaying = false;
        });
    }
  }

  toggleMode() {
    this.isLogin = !this.isLogin;
    this.error = '';
    this.showVerificationStep = false;
    this.registeredEmail = '';
    this.cpf = '';
    this.acceptPrivacyPolicy = false; // Reseta o checkbox ao alternar entre login e cadastro
  }

  onCpfInput(): void {
    this.cpf = formatCpfDisplay(this.cpf);
  }

  onCpfKeydown(event: KeyboardEvent): void {
    const key = event.key;
    if (key === 'Backspace' || key === 'Delete' || key === 'Tab' || key === 'ArrowLeft' || key === 'ArrowRight') return;
    if (key.length === 1 && !/\d/.test(key)) {
      event.preventDefault();
    }
  }

  submit() {
    this.error = '';
    this.loading = true;
    
    if (this.isLogin) {
      this.authService.login(this.email, this.senha).subscribe({
        next: (response) => {
          if (response.success) {
            this.loading = false;
            // Redireciona para a página principal
            this.router.navigate(['/']);
          } else {
            this.error = response.error || 'Erro ao fazer login';
            this.loading = false;
          }
        },
        error: (err: HttpErrorResponse) => {
          this.loading = false;
          
          // Erro de conexão/rede (não conseguiu conectar na API)
          if (err.status === 0 || err.status === undefined || err.error instanceof ProgressEvent) {
            this.error = 'Erro ao conectar com o servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
            return;
          }
          
          // Erro de autenticação (credenciais incorretas)
          if (err.status === 401) {
            this.error = 'Email ou senha incorretos. Verifique suas credenciais e tente novamente.';
            return;
          }
          
          // Se o erro for email não verificado, mostra etapa de verificação
          if (err.error?.requiresVerification) {
            this.showVerificationStep = true;
            this.registeredEmail = this.email;
            this.error = err.error?.message || 'Por favor, verifique seu email antes de fazer login';
            return;
          }
          
          // Outros erros (500, etc) - problema interno
          if (err.status >= 500) {
            this.error = 'Erro interno do servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
            return;
          }
          
          // Erro genérico
          this.error = err.error?.error || err.error?.message || 'Erro ao fazer login. Tente novamente.';
        }
      });
    } else {
      if (!this.nome) {
        this.error = 'Nome é obrigatório';
        this.loading = false;
        return;
      }

      const cpfDigits = getCpfDigits(this.cpf);
      if (cpfDigits.length !== 11) {
        this.error = 'CPF é obrigatório e deve conter 11 dígitos';
        this.loading = false;
        return;
      }
      
      if (!this.acceptPrivacyPolicy) {
        this.error = 'Você precisa aceitar a Política de Privacidade e os Termos de Uso para criar uma conta';
        this.loading = false;
        return;
      }
      
      this.authService.register(this.email, this.senha, this.nome, cpfDigits).subscribe({
        next: (response) => {
          if (response.success) {
            // Se precisar verificar email, mostra etapa de verificação
            if (response.requiresVerification) {
              this.showVerificationStep = true;
              this.registeredEmail = this.email;
              this.error = '';
            } else {
              // Se não precisar verificar, redireciona
              this.router.navigate(['/']);
            }
          } else {
            this.error = response.error || 'Erro ao registrar';
          }
          this.loading = false;
        },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        
        // Erro de conexão/rede (não conseguiu conectar na API)
        if (err.status === 0 || err.status === undefined || err.error instanceof ProgressEvent) {
          this.error = 'Erro ao conectar com o servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Se o email já existe mas não está verificado, mostra etapa de verificação
        if (err.error?.requiresVerification) {
          this.showVerificationStep = true;
          this.registeredEmail = this.email;
          this.error = err.error?.message || 'Email já cadastrado. Por favor, verifique seu email.';
          return;
        }
        
        // Outros erros (500, etc) - problema interno
        if (err.status >= 500) {
          this.error = 'Erro interno do servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro genérico
        this.error = err.error?.error || err.error?.message || 'Erro ao registrar. Tente novamente.';
      }
      });
    }
  }

  // Métodos para login com código
  requestLoginCode() {
    this.error = '';
    this.success = '';
    
    if (!this.loginCodeEmail) {
      this.error = 'Email é obrigatório';
      return;
    }

    this.requestCodeLoading = true;
    this.authService.requestLoginCode(this.loginCodeEmail).subscribe({
      next: (response) => {
        if (response.success) {
          this.success = response.message || 'Código enviado! Verifique seu email.';
          this.loginCode = ''; // Limpa para mostrar o campo de código
          this.loginCodeDigits = ['', '', '', '', '', '']; // Limpa os dígitos
          this.error = ''; // Limpa erros anteriores
          this.codeSent = true; // Marca que o código foi enviado
        } else {
          this.error = response.error || 'Erro ao solicitar código de login';
        }
        this.requestCodeLoading = false;
      },
      error: (err: HttpErrorResponse) => {
        this.requestCodeLoading = false;
        
        // Erro de conexão/rede (não conseguiu conectar na API)
        if (err.status === 0 || err.status === undefined || err.error instanceof ProgressEvent) {
          this.error = 'Erro ao conectar com o servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro de autenticação (email não encontrado ou não verificado)
        if (err.status === 401 || err.status === 403) {
          this.error = err.error?.error || err.error?.message || 'Email não encontrado ou não verificado. Verifique seu email e tente novamente.';
          return;
        }
        
        // Outros erros (500, etc) - problema interno
        if (err.status >= 500) {
          this.error = 'Erro interno do servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro genérico
        this.error = err.error?.error || err.error?.message || 'Erro ao solicitar código de login. Tente novamente.';
      }
    });
  }

  verifyLoginCode() {
    this.error = '';
    this.success = '';
    
    // Atualiza o código completo a partir dos dígitos
    this.loginCode = this.loginCodeDigits.join('');
    
    if (!this.loginCodeEmail || !this.loginCode) {
      this.error = 'Email e código são obrigatórios';
      return;
    }

    if (this.loginCode.length !== 6) {
      this.error = 'O código deve ter 6 dígitos';
      return;
    }

    this.loginCodeLoading = true;
    this.authService.verifyLoginCode(this.loginCodeEmail, this.loginCode).subscribe({
      next: (response) => {
        if (response.success) {
          this.success = 'Login realizado com sucesso!';
          setTimeout(() => {
            this.router.navigate(['/']);
          }, 1000);
        } else {
          this.error = response.error || 'Código inválido ou expirado';
          this.loginCodeLoading = false;
        }
      },
      error: (err: HttpErrorResponse) => {
        this.loginCodeLoading = false;
        
        // Erro de conexão/rede (não conseguiu conectar na API)
        if (err.status === 0 || err.status === undefined || err.error instanceof ProgressEvent) {
          this.error = 'Erro ao conectar com o servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro de autenticação (código inválido ou expirado)
        if (err.status === 401 || err.status === 400) {
          this.error = err.error?.error || err.error?.message || 'Código inválido ou expirado. Solicite um novo código.';
          return;
        }
        
        // Outros erros (500, etc) - problema interno
        if (err.status >= 500) {
          this.error = 'Erro interno do servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro genérico
        this.error = err.error?.error || err.error?.message || 'Erro ao verificar código. Tente novamente.';
      }
    });
  }

  // Método para login com Google
  loginWithGoogle() {
    this.error = '';
    this.success = '';
    
    // Redireciona para o endpoint de OAuth do Google
    const googleAuthUrl = `${environment.apiUrl}/auth/google`;
    window.location.href = googleAuthUrl;
  }

  verifyEmail() {
    if (!this.verificationCode || this.verificationCode.length !== 6) {
      this.error = 'Código de verificação deve ter 6 dígitos';
      return;
    }

    this.verifyLoading = true;
    this.error = '';

    this.authService.verifyEmail(this.registeredEmail, this.verificationCode).subscribe({
      next: (response) => {
        if (response.success) {
          this.verifyLoading = false;
          this.router.navigate(['/']);
        } else {
          this.error = response.error || 'Código inválido';
          this.verifyLoading = false;
        }
      },
      error: (err: HttpErrorResponse) => {
        this.verifyLoading = false;
        
        // Erro de conexão/rede (não conseguiu conectar na API)
        if (err.status === 0 || err.status === undefined || err.error instanceof ProgressEvent) {
          this.error = 'Erro ao conectar com o servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro de autenticação (código inválido ou expirado)
        if (err.status === 401 || err.status === 400) {
          this.error = err.error?.error || err.error?.message || 'Código inválido ou expirado. Solicite um novo código.';
          return;
        }
        
        // Outros erros (500, etc) - problema interno
        if (err.status >= 500) {
          this.error = 'Erro interno do servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro genérico
        this.error = err.error?.error || err.error?.message || 'Erro ao verificar código. Tente novamente.';
      }
    });
  }

  resendVerificationCode() {
    this.resendLoading = true;
    this.error = '';

    this.authService.resendVerificationCode(this.registeredEmail).subscribe({
      next: (response) => {
        if (response.success) {
          this.error = 'Código reenviado com sucesso! Verifique seu email.';
        } else {
          this.error = response.error || 'Erro ao reenviar código';
        }
        this.resendLoading = false;
      },
      error: (err: HttpErrorResponse) => {
        this.resendLoading = false;
        
        // Erro de conexão/rede (não conseguiu conectar na API)
        if (err.status === 0 || err.status === undefined || err.error instanceof ProgressEvent) {
          this.error = 'Erro ao conectar com o servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Outros erros (500, etc) - problema interno
        if (err.status >= 500) {
          this.error = 'Erro interno do servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro genérico
        this.error = err.error?.error || err.error?.message || 'Erro ao reenviar código. Tente novamente.';
      }
    });
  }

  onVerificationCodeInput(event: Event) {
    // Remove caracteres não numéricos e limita a 6 dígitos
    const target = event.target as HTMLInputElement;
    if (!target) return;
    
    let value = target.value.replace(/\D/g, '');
    if (value.length > 6) {
      value = value.substring(0, 6);
    }
    this.verificationCode = value;
    target.value = value;
  }

  onLoginCodeInput(event: Event, index: number) {
    const target = event.target as HTMLInputElement;
    if (!target) return;
    
    // Pega o valor atual do campo
    let currentValue = target.value;
    
    // Remove caracteres não numéricos
    let value = currentValue.replace(/\D/g, '');
    
    // Se digitou mais de um caractere, pega apenas o último
    if (value.length > 1) {
      value = value.charAt(value.length - 1);
    }
    
    // Limita a apenas 1 dígito
    value = value.slice(0, 1);
    
    // Atualiza apenas o dígito atual no array (sem afetar os outros)
    const oldValue = this.loginCodeDigits[index];
    this.loginCodeDigits[index] = value;
    
    // Força o valor do campo para garantir que está correto
    if (target.value !== value) {
      target.value = value;
    }
    
    // Atualiza o código completo
    this.loginCode = this.loginCodeDigits.join('');
    
    // Se digitou um número e não é o último campo, avança para o próximo
    if (value && value !== oldValue && index < 5) {
      setTimeout(() => {
        const nextInput = document.querySelector(`input[name="codeDigit${index + 1}"]`) as HTMLInputElement;
        if (nextInput) {
          // Limpa o próximo campo antes de focar
          nextInput.value = '';
          this.loginCodeDigits[index + 1] = '';
          nextInput.focus();
        }
      }, 10);
    }
  }

  onLoginCodeKeyDown(event: KeyboardEvent, index: number) {
    const target = event.target as HTMLInputElement;
    
    // Permite navegação com setas
    if (event.key === 'ArrowLeft' && index > 0) {
      event.preventDefault();
      const prevInput = document.querySelector(`input[name="codeDigit${index - 1}"]`) as HTMLInputElement;
      if (prevInput) {
        prevInput.focus();
      }
      return;
    }
    
    if (event.key === 'ArrowRight' && index < 5) {
      event.preventDefault();
      const nextInput = document.querySelector(`input[name="codeDigit${index + 1}"]`) as HTMLInputElement;
      if (nextInput) {
        nextInput.focus();
      }
      return;
    }
    
    // Se pressionou Backspace e o campo está vazio, volta para o anterior
    if (event.key === 'Backspace' && !target.value && index > 0) {
      event.preventDefault();
      this.loginCodeDigits[index] = '';
      this.loginCode = this.loginCodeDigits.join('');
      const prevInput = document.querySelector(`input[name="codeDigit${index - 1}"]`) as HTMLInputElement;
      if (prevInput) {
        prevInput.focus();
        prevInput.value = '';
        this.loginCodeDigits[index - 1] = '';
        this.loginCode = this.loginCodeDigits.join('');
      }
      return;
    }
    
    // Se pressionou Backspace e o campo tem valor, limpa e fica no mesmo campo
    if (event.key === 'Backspace' && target.value) {
      this.loginCodeDigits[index] = '';
      this.loginCode = this.loginCodeDigits.join('');
      return;
    }
    
    // Se pressionou Delete, limpa o campo atual
    if (event.key === 'Delete') {
      this.loginCodeDigits[index] = '';
      this.loginCode = this.loginCodeDigits.join('');
      return;
    }
    
    // Previne que caracteres não numéricos sejam digitados
    if (event.key.length === 1 && !/[0-9]/.test(event.key) && !['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight'].includes(event.key)) {
      event.preventDefault();
      return;
    }
  }

  onLoginCodePaste(event: ClipboardEvent) {
    event.preventDefault();
    const pastedData = event.clipboardData?.getData('text') || '';
    const digits = pastedData.replace(/\D/g, '').slice(0, 6).split('');
    
    // Preenche os campos com os dígitos colados
    for (let i = 0; i < 6; i++) {
      this.loginCodeDigits[i] = digits[i] || '';
      // Atualiza o valor do input diretamente
      const input = document.querySelector(`input[name="codeDigit${i}"]`) as HTMLInputElement;
      if (input) {
        input.value = digits[i] || '';
      }
    }
    
    // Atualiza o código completo
    this.loginCode = this.loginCodeDigits.join('');
    
    // Foca no último campo preenchido ou no primeiro vazio
    const firstEmptyIndex = this.loginCodeDigits.findIndex(d => !d);
    const focusIndex = firstEmptyIndex === -1 ? 5 : firstEmptyIndex;
    const focusInput = document.querySelector(`input[name="codeDigit${focusIndex}"]`) as HTMLInputElement;
    if (focusInput) {
      focusInput.focus();
    }
  }

  trackByIndex(index: number): number {
    return index;
  }

  togglePasswordVisibility() {
    this.showPassword = !this.showPassword;
  }

  requestPasswordReset() {
    if (!this.forgotPasswordEmail) {
      this.error = 'Email é obrigatório';
      return;
    }

    this.forgotPasswordLoading = true;
    this.error = '';
    this.forgotPasswordSuccess = '';

    this.authService.forgotPassword(this.forgotPasswordEmail).subscribe({
      next: (response) => {
        if (response.success) {
          this.forgotPasswordSuccess = 'Link de recuperação enviado! Verifique seu email.';
          this.forgotPasswordLoading = false;
        } else {
          this.error = response.error || 'Erro ao enviar link de recuperação';
          this.forgotPasswordLoading = false;
        }
      },
      error: (err: HttpErrorResponse) => {
        this.forgotPasswordLoading = false;
        
        // Erro de conexão/rede (não conseguiu conectar na API)
        if (err.status === 0 || err.status === undefined || err.error instanceof ProgressEvent) {
          this.error = 'Erro ao conectar com o servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Outros erros (500, etc) - problema interno
        if (err.status >= 500) {
          this.error = 'Erro interno do servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro genérico (mesmo que email não exista, por segurança não revelamos)
        this.error = 'Se o email estiver cadastrado, você receberá um link de recuperação. Verifique sua caixa de entrada.';
      }
    });
  }

  resetPassword() {
    this.error = '';
    this.success = '';

    if (!this.newPassword || !this.confirmPassword) {
      this.error = 'Todos os campos são obrigatórios';
      return;
    }

    if (this.newPassword.length < 6) {
      this.error = 'A senha deve ter no mínimo 6 caracteres';
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.error = 'As senhas não coincidem';
      return;
    }

    // Obtém o token da URL ou do estado
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token') || this.resetToken;

    if (!token) {
      this.error = 'Token de recuperação não encontrado. Por favor, solicite um novo link.';
      return;
    }

    this.resetPasswordLoading = true;

    this.authService.resetPassword(token, this.newPassword).subscribe({
      next: (response) => {
        if (response.success) {
          this.error = '';
          this.resetPasswordLoading = false;
          // Redireciona para login após 2 segundos
          setTimeout(() => {
            this.showResetPassword = false;
            this.showForgotPassword = false;
            this.isLogin = true;
            this.error = '';
            this.newPassword = '';
            this.confirmPassword = '';
            // Mostra mensagem de sucesso no login
            this.success = 'Senha redefinida com sucesso! Faça login com sua nova senha.';
            this.error = '';
          }, 2000);
        } else {
          this.error = response.error || 'Erro ao redefinir senha';
          this.resetPasswordLoading = false;
        }
      },
      error: (err: HttpErrorResponse) => {
        this.resetPasswordLoading = false;
        
        // Erro de conexão/rede (não conseguiu conectar na API)
        if (err.status === 0 || err.status === undefined || err.error instanceof ProgressEvent) {
          this.error = 'Erro ao conectar com o servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro de autenticação (token inválido ou expirado)
        if (err.status === 401 || err.status === 400) {
          this.error = err.error?.error || err.error?.message || 'Token inválido ou expirado. Solicite um novo link de recuperação.';
          return;
        }
        
        // Outros erros (500, etc) - problema interno
        if (err.status >= 500) {
          this.error = 'Erro interno do servidor. O sistema pode estar temporariamente fora do ar. Por favor, tente novamente em alguns instantes ou entre em contato com o suporte.';
          return;
        }
        
        // Erro genérico
        this.error = err.error?.error || err.error?.message || 'Erro ao redefinir senha. Tente novamente.';
      }
    });
  }

  ngAfterViewInit(): void {
    this.updateWideLayout();
    setTimeout(() => this.initLoginVideo(), 150);
  }

  ngOnInit() {
    this.updateWideLayout();
    this.loadPricingPlans();

    // Verifica se há token de reset ou OAuth na URL
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');
    const success = urlParams.get('success');
    const error = urlParams.get('error');

    if (token && success === 'true') {
      // Token do Google OAuth
      this.authService.setToken(token);
      this.authService.verifyToken().subscribe({
        next: (response) => {
          if (response.success && response.user) {
            this.authService.setUser(response.user);
            this.router.navigate(['/']);
          }
        },
        error: () => {
          this.error = 'Erro ao fazer login com Google';
        }
      });
    } else if (token) {
      // Token de reset de senha
      this.resetToken = token;
      this.showResetPassword = true;
      this.showForgotPassword = false;
    } else if (error) {
      if (error === 'google_not_configured') {
        this.error = 'Login com Google não está configurado no momento. Use outro método de login.';
      } else {
        this.error = 'Erro ao fazer login com Google. Tente novamente.';
      }
    }
  }
}

