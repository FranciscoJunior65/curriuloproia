import { ChangeDetectorRef, Component, EventEmitter, Input, NgZone, OnDestroy, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AnalyzerService } from '../../services/analyzer.service';
import {
  VoiceSpeechService,
  VoiceUiState,
  VoiceGender
} from '../../services/voice-speech.service';

interface ConversationLine {
  role: 'interviewer' | 'candidate';
  content: string;
}

interface Persona {
  name: string;
  role: string;
  company: string;
  initials: string;
  avatarColor: string;
  avatarUrl?: string;
  voiceGender: VoiceGender;
}

const INTERVIEWER_AVATARS: Record<string, string> = {
  AR: 'assets/interviewers/ana-ribeiro.png',
  CM: 'assets/interviewers/carlos-mendes.png',
  MC: 'assets/interviewers/marina-costa.png'
};

const PERSONA_GENDER: Record<string, VoiceGender> = {
  AR: 'female',
  MC: 'female',
  CM: 'male'
};

@Component({
  selector: 'app-voice-interview',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './voice-interview.component.html',
  styleUrl: './voice-interview.component.scss'
})
export class VoiceInterviewComponent implements OnDestroy {
  @Input() resumeText = '';
  @Input() analysis: any;
  @Input() siteId?: string;
  @Input() resumeId?: string;
  @Input() analysisId?: string;
  @Output() closed = new EventEmitter<void>();

  persona: Persona | null = null;
  simulationId: string | null = null;
  history: ConversationLine[] = [];
  uiState: VoiceUiState = 'idle';
  phase = '';
  turnNumber = 0;
  loading = false;
  error: string | null = null;
  started = false;
  finished = false;
  summary: any = null;
  /** Texto capturado da voz — editável antes de enviar */
  candidateDraft = '';
  speechSupported = true;
  private sendingAnswer = false;
  private speakIdleTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private analyzer: AnalyzerService,
    private voice: VoiceSpeechService,
    private ngZone: NgZone,
    private cdr: ChangeDetectorRef
  ) {
    this.speechSupported = this.voice.isSupported();
  }

  ngOnDestroy(): void {
    this.clearSpeakIdleTimer();
    this.voice.stopSpeaking();
    this.voice.stopListening();
  }

  skipNarration(): void {
    this.clearSpeakIdleTimer();
    this.voice.stopSpeaking();
    this.setUiState('idle');
  }

  get canUseMic(): boolean {
    return (
      this.started &&
      !this.finished &&
      !this.loading &&
      this.uiState !== 'speaking' &&
      this.uiState !== 'processing'
    );
  }

  get canSendAnswer(): boolean {
    return (
      !this.finished &&
      !this.loading &&
      this.uiState !== 'speaking' &&
      this.uiState !== 'processing' &&
      !!this.candidateDraft.trim()
    );
  }

  get statusLabel(): string {
    switch (this.uiState) {
      case 'speaking':
        return `${this.persona?.name ?? 'Entrevistador'} está falando...`;
      case 'listening':
        return 'Ouvindo você... o texto aparece abaixo em tempo real';
      case 'reviewing':
        return 'Revise sua resposta e clique em Enviar';
      case 'processing':
        return 'Processando resposta...';
      default:
        return this.finished
          ? 'Entrevista encerrada'
          : 'Toque no microfone, fale, revise o texto e envie';
    }
  }

  begin(): void {
    if (!this.resumeText || !this.analysis) {
      this.error = 'Análise do currículo necessária antes da entrevista.';
      return;
    }

    this.loading = true;
    this.error = null;

    this.analyzer
      .startVoiceInterview(this.resumeText, this.analysis, this.siteId, this.resumeId, this.analysisId)
      .subscribe({
        next: (res: any) => {
          this.loading = false;
          if (!res?.success) {
            this.error = res?.message || 'Erro ao iniciar entrevista';
            return;
          }
          this.persona = this.mapPersona(res.persona);
          this.simulationId = res.simulationId ?? null;
          this.started = true;
          this.pushInterviewer(res.openingMessage);
          this.speakInterviewer(res.openingMessage);
        },
        error: (err) => {
          this.loading = false;
          const msg = err?.error?.message || 'Erro ao iniciar entrevista por voz';
          this.error =
            msg.includes('high demand') || err?.status === 503
              ? 'A IA está sobrecarregada no momento. Aguarde alguns minutos e tente de novo.'
              : msg;
        }
      });
  }

  startListening(): void {
    if (!this.canUseMic) {
      return;
    }
    this.error = null;
    this.candidateDraft = '';
    this.setUiState('listening');
    this.voice.listen(
      (text) => {
        this.ngZone.run(() => {
          this.candidateDraft = text;
          this.cdr.markForCheck();
        });
      },
      (msg) => {
        this.ngZone.run(() => {
          this.setUiState('reviewing');
          this.error = msg;
        });
      },
      () => {
        this.ngZone.run(() => this.setUiState('reviewing'));
      }
    );
  }

  stopListeningForReview(): void {
    this.voice.stopListening();
    if (this.uiState === 'listening') {
      this.setUiState('reviewing');
    }
  }

  confirmAndSend(): void {
    const message = this.candidateDraft.trim();
    if (!message || this.sendingAnswer) {
      return;
    }
    this.voice.stopListening();
    this.sendCandidateMessage(message);
  }

  endEarly(): void {
    this.voice.stopSpeaking();
    this.voice.stopListening();
    this.finalizeInterview();
  }

  close(): void {
    this.voice.stopSpeaking();
    this.voice.stopListening();
    this.closed.emit();
  }

  private sendCandidateMessage(message: string): void {
    if (this.sendingAnswer) {
      return;
    }
    this.sendingAnswer = true;
    this.candidateDraft = '';
    this.setUiState('processing');
    this.loading = true;
    this.error = null;
    this.turnNumber += 1;
    this.pushCandidate(message);

    const apiHistory = this.history.map(h => ({ role: h.role, content: h.content }));

    this.analyzer
      .voiceInterviewTurn(
        this.resumeText,
        this.analysis,
        message,
        apiHistory,
        this.turnNumber,
        this.siteId,
        this.simulationId ?? undefined,
        this.analysisId
      )
      .subscribe({
        next: (res: any) => {
          this.loading = false;
          this.sendingAnswer = false;
          if (!res?.success) {
            this.error = res?.message || 'Erro no turno da entrevista';
            this.setUiState('reviewing');
            this.candidateDraft = message;
            return;
          }
          this.phase = res.phase || '';
          this.pushInterviewer(res.interviewerMessage);
          if (res.shouldEnd) {
            this.speakInterviewer(res.interviewerMessage, () => this.finalizeInterview());
          } else {
            this.speakInterviewer(res.interviewerMessage);
          }
        },
        error: (err) => {
          this.loading = false;
          this.sendingAnswer = false;
          this.setUiState('reviewing');
          this.candidateDraft = message;
          this.error = err?.error?.message || 'Erro ao processar sua resposta';
        }
      });
  }

  private finalizeInterview(): void {
    this.setUiState('processing');
    this.loading = true;
    this.voice.stopListening();
    const apiHistory = this.history.map(h => ({ role: h.role, content: h.content }));

    this.analyzer
      .finishVoiceInterview(
        this.resumeText,
        this.analysis,
        apiHistory,
        this.simulationId ?? undefined,
        this.analysisId
      )
      .subscribe({
        next: (res: any) => {
          this.loading = false;
          this.finished = true;
          this.setUiState('idle');
          this.summary = res?.summary ?? res;
        },
        error: (err) => {
          this.loading = false;
          this.finished = true;
          this.error = err?.error?.message || 'Erro ao gerar resumo final';
        }
      });
  }

  private mapPersona(raw: any): Persona {
    const initials = (raw?.initials ?? 'AR').toUpperCase();
    return {
      name: raw?.name ?? 'Entrevistador',
      role: raw?.role ?? 'Recrutador(a)',
      company: raw?.company ?? '',
      initials,
      avatarColor: raw?.avatarColor ?? '#6366f1',
      avatarUrl: INTERVIEWER_AVATARS[initials] ?? 'assets/imagens/persona.jpeg',
      voiceGender: PERSONA_GENDER[initials] ?? 'female'
    };
  }

  private pushInterviewer(content: string): void {
    this.history.push({ role: 'interviewer', content });
  }

  private pushCandidate(content: string): void {
    this.history.push({ role: 'candidate', content });
  }

  private speakInterviewer(text: string, onEnd?: () => void): void {
    this.voice.stopListening();
    this.setUiState('speaking');
    this.scheduleSpeakIdleFallback(text);

    const gender = this.persona?.voiceGender ?? 'female';
    this.voice.speak(
      text,
      () => {
        this.ngZone.run(() => {
          this.clearSpeakIdleTimer();
          this.setUiState('idle');
          onEnd?.();
        });
      },
      { gender }
    );
  }

  private setUiState(state: VoiceUiState): void {
    this.uiState = state;
    this.cdr.markForCheck();
  }

  private scheduleSpeakIdleFallback(text: string): void {
    this.clearSpeakIdleTimer();
    const ms = Math.min(75_000, Math.max(6_000, text.length * 50));
    this.speakIdleTimer = setTimeout(() => {
      if (this.uiState === 'speaking') {
        this.setUiState('idle');
      }
    }, ms);
  }

  private clearSpeakIdleTimer(): void {
    if (this.speakIdleTimer) {
      clearTimeout(this.speakIdleTimer);
      this.speakIdleTimer = null;
    }
  }
}
