import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  NgZone,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  ViewChild,
} from "@angular/core";

import { CommonModule } from "@angular/common";

import { FormsModule } from "@angular/forms";

import { MatButtonModule } from "@angular/material/button";

import { MatIconModule } from "@angular/material/icon";

import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";

import { AnalyzerService } from "../../services/analyzer.service";

// Simli desativado — intro via MP4. Serviço mantido comentado para reativação futura.
// import { SimliAvatarService } from "../../services/simli-avatar.service";
import { BackendSpeechService } from "../../services/backend-speech.service";

import {
  VoiceSpeechService,
  VoiceGender,
} from "../../services/voice-speech.service";

type InterviewStep =
  | "idle"
  | "loading"
  | "already_done"
  | "intro_video"
  | "written_questions"
  | "phase1"
  | "loading_feedback"
  | "feedback_audio"
  | "complete";

interface Persona {
  name: string;

  role: string;

  company: string;

  initials: string;

  avatarColor: string;

  avatarUrl?: string;

  voiceGender: VoiceGender;
}

interface WrittenQuestion {
  text: string;
  type: "open" | "choice";
  options: string[];
}

const INTERVIEW_AVATAR = "assets/imagens/persona.png";

const INTRO_VIDEO_URL = 'assets/videos/simulador-entrevista.mp4';

/** Simli/WebRTC desativado — substituído por vídeo MP4 + foto estática. */
const SIMLI_ENABLED = false;

const PERSONA_GENDER: Record<string, VoiceGender> = {
  AR: "female",

  MC: "female",

  CM: "male",
};

@Component({
  selector: "app-voice-interview",

  standalone: true,

  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],

  templateUrl: "./voice-interview.component.html",

  styleUrl: "./voice-interview.component.scss",
})
export class VoiceInterviewComponent
  implements OnDestroy, AfterViewInit, OnChanges
{
  @Input() resumeText = "";

  @Input() analysis: any;

  @Input() siteId?: string;

  @Input() resumeId?: string;

  @Input() analysisId?: string;

  @Input() autoStart = false;

  @Output() closed = new EventEmitter<void>();

  @Output() completed = new EventEmitter<{ simulationId: string }>();

  @Output() ready = new EventEmitter<void>();

  step: InterviewStep = "idle";

  persona: Persona | null = null;

  simulationId: string | null = null;

  candidateName = "";

  introScript = "";

  writtenQuestions: WrittenQuestion[] = [];

  writtenAnswers: string[] = ["", "", "", "", ""];

  phase1Answer = "";

  feedbackScript = "";

  summary: any = null;

  phase1Minutes = 15;

  timerSeconds = 0;

  candidateDraft = "";

  candidateSegments: string[] = [];

  candidateInterim = "";

  loading = false;

  error: string | null = null;

  speechSupported = true;

  simliActive = false;

  simliBootstrapping = false;

  readonly introVideoUrl = INTRO_VIDEO_URL;

  introVideoEnded = false;

  introVideoNeedsPlay = false;

  questionsLoading = false;

  questionsReady = false;

  videoExpanded = false;

  interviewerCaption = "";

  /** Após a intro — aguardando botão para abrir o microfone. */
  recordingAwaitingStart = false;

  get isRecordingAwaitingStart(): boolean {
    return this.step === "phase1" && this.recordingAwaitingStart;
  }

  /** Feedback gerado — aguardando botão ouvir. */
  feedbackAwaitingPlay = false;

  feedbackPlaying = false;

  private feedbackFinishLock = false;

  private timerInterval: ReturnType<typeof setInterval> | null = null;

  private autoStartTriggered = false;

  private currentVideoScript = "";

  private phaseTransitionLock = false;

  @ViewChild("introVideo") introVideoRef?: ElementRef<HTMLVideoElement>;

  @ViewChild("fallbackAudio") fallbackAudioRef?: ElementRef<HTMLAudioElement>;

  /** @deprecated Simli desativado */
  private simliPreconnectTask: Promise<boolean> | null = null;

  /** @deprecated Simli desativado */
  private simliSessionEnded = true;

  private introFinishLock = false;

  /** Incrementado ao parar áudio — invalida reproduções em andamento. */
  private playbackSession = 0;

  constructor(
    private analyzer: AnalyzerService,

    private voice: VoiceSpeechService,

    private backendSpeech: BackendSpeechService,

    private ngZone: NgZone,

    private cdr: ChangeDetectorRef,
  ) {
    this.speechSupported = this.voice.isSupported();
  }

  ngAfterViewInit(): void {
    this.tryAutoStart();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes["autoStart"] || changes["analysisId"]) {
      this.tryAutoStart();
    }
  }

  ngOnDestroy(): void {
    this.stopAllPlayback();
    this.clearTimer();
    this.stopIntroVideo();
  }

  get simliStageVisible(): boolean {
    return false;
  }

  get timerLabel(): string {
    const m = Math.floor(this.timerSeconds / 60);

    const s = this.timerSeconds % 60;

    return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  }

  get statusLabel(): string {
    switch (this.step) {
      case "written_questions":
        return "Responda às 5 perguntas sobre seu currículo";

      case "intro_video":
        if (!this.introVideoEnded) {
          return "Assista à apresentação da entrevistadora";
        }
        if (!this.questionsReady) {
          return "Preparando suas perguntas personalizadas…";
        }
        return "Pronto — continue para as perguntas";

      case "phase1":
        if (this.recordingAwaitingStart) {
          return "Toque em iniciar gravação quando estiver pronto";
        }
        return `Fale sobre você (${this.timerLabel} restantes)`;

      case "loading_feedback":
        return "Analisando respostas e gerando feedback...";

      case "feedback_audio":
        if (this.feedbackPlaying) {
          return "Ouvindo feedback da entrevista...";
        }
        if (this.feedbackAwaitingPlay) {
          return "Feedback pronto — toque para ouvir";
        }
        return "Gerando áudio do feedback...";

      case "complete":
        return "Entrevista concluída";

      case "already_done":
        return "Entrevista já realizada";

      case "loading":
        return "Preparando sua simulação de entrevista...";

      default:
        return "";
    }
  }

  get isBootstrapping(): boolean {
    return this.loading || this.step === "loading";
  }

  get bootstrapLoadingMessage(): string {
    return this.statusLabel || "Preparando sua simulação de entrevista...";
  }

  private notifyParentReady(): void {
    this.ready.emit();
  }

  get isRecordingPhase(): boolean {
    return this.step === "phase1";
  }

  /** Timer e microfone ativos — candidato já clicou em iniciar gravação. */
  get isRecordingActive(): boolean {
    return this.step === "phase1" && !this.recordingAwaitingStart;
  }

  get isVideoPhase(): boolean {
    return this.step === "intro_video";
  }

  get isFeedbackAudioPhase(): boolean {
    return this.step === "feedback_audio";
  }

  get writtenQuestionsComplete(): boolean {
    return this.writtenAnswers.filter((a) => a?.trim().length > 0).length >= 3;
  }

  toggleVideoExpanded(): void {
    this.videoExpanded = !this.videoExpanded;

    this.cdr.markForCheck();
  }

  finishRecordingEarly(): void {
    if (!this.isRecordingActive || this.phaseTransitionLock) {
      return;
    }

    this.endCurrentRecordingPhase();
  }

  startCandidateRecording(): void {
    if (!this.recordingAwaitingStart || this.step !== "phase1") {
      return;
    }

    this.recordingAwaitingStart = false;
    this.cdr.markForCheck();
    this.startPhaseTimer(this.phase1Minutes, () => this.onPhase1End());
  }

  begin(): void {
    if (!this.analysis) {
      this.error = "Análise do currículo necessária antes da entrevista.";
      this.notifyParentReady();
      return;
    }

    if (!this.resumeText?.trim() && this.analysisId) {
      this.loading = true;
      this.error = null;
      this.step = "loading";
      this.analyzer.getAnalysisById(this.analysisId).subscribe({
        next: (res: any) => {
          if (res?.originalText?.trim()) {
            this.resumeText = res.originalText;
          }
          if (!this.siteId) {
            this.siteId = res?.siteId ?? res?.analysis?.id_site_vagas ?? this.siteId;
          }
          if (!this.resumeText?.trim()) {
            this.loading = false;
            this.step = "idle";
            this.error = "Texto do currículo indisponível. Abra pelo histórico ou refaça a análise.";
            this.notifyParentReady();
            this.cdr.markForCheck();
            return;
          }
          this.runBeginAfterResumeReady();
        },
        error: () => {
          this.loading = false;
          this.step = "idle";
          this.error = "Não foi possível carregar o currículo para a entrevista.";
          this.notifyParentReady();
          this.cdr.markForCheck();
        },
      });
      return;
    }

    if (!this.resumeText?.trim()) {
      this.error = "Texto do currículo indisponível.";
      this.notifyParentReady();
      return;
    }

    this.runBeginAfterResumeReady();
  }

  private runBeginAfterResumeReady(): void {
    this.loading = true;
    this.error = null;
    this.step = "loading";

    if (this.analysisId) {
      this.analyzer.getStructuredInterviewStatus(this.analysisId).subscribe({
        next: (statusRes: any) => {
          if (statusRes?.status?.alreadyCompleted) {
            this.loading = false;

            this.simulationId = statusRes.status.simulationId ?? null;

            this.summary = statusRes.status.savedFeedback ?? null;
            this.feedbackScript = this.summary?.feedbackScript ?? "";

            this.step = "already_done";

            this.notifyParentReady();
            this.cdr.markForCheck();

            return;
          }

          this.startIntroVideoPhase();
        },

        error: () => this.startIntroVideoPhase(),
      });
    } else {
      this.startIntroVideoPhase();
    }
  }

  /** Vídeo MP4 + prefetch das perguntas em paralelo. */
  private startIntroVideoPhase(): void {
    this.loading = false;
    this.error = null;
    this.introVideoEnded = false;
    this.introVideoNeedsPlay = false;
    this.questionsReady = false;
    this.questionsLoading = true;
    this.step = "intro_video";
    this.videoExpanded = true;
    this.notifyParentReady();
    this.cdr.markForCheck();
    this.prefetchQuestions();
    setTimeout(() => void this.playIntroVideo(), 200);
  }

  playIntroVideo(attempt = 0): void {
    const el = this.introVideoRef?.nativeElement;
    if (!el) {
      if (attempt < 25) {
        setTimeout(() => this.playIntroVideo(attempt + 1), 120);
      }
      return;
    }
    this.introVideoNeedsPlay = false;
    el.muted = false;
    void el.play().catch(() => {
      this.introVideoNeedsPlay = true;
      this.cdr.markForCheck();
    });
  }

  onIntroVideoEnded(): void {
    this.introVideoEnded = true;
    this.stopIntroVideo(false);
    this.cdr.markForCheck();
  }

  continueToQuestions(): void {
    if (!this.introVideoEnded || !this.questionsReady || !this.persona) {
      return;
    }
    this.stopIntroVideo();
    this.step = "written_questions";
    this.cdr.markForCheck();
  }

  continueToRecording(): void {
    if (!this.writtenQuestionsComplete) {
      this.error = "Responda pelo menos 3 das 5 perguntas para continuar.";
      this.cdr.markForCheck();
      return;
    }

    this.error = null;
    this.loading = true;
    this.cdr.markForCheck();

    this.analyzer
      .beginStructuredVoicePhase(this.resumeText, this.analysis, {
        simulationId: this.simulationId ?? undefined,
        analysisId: this.analysisId,
        siteId: this.siteId,
        candidateName: this.candidateName,
        writtenQuestions: this.writtenQuestions.map((q) => q.text),
        writtenAnswers: this.writtenAnswers,
      })
      .subscribe({
        next: (res: any) => {
          this.loading = false;
          if (!res?.success) {
            this.error = res?.message || "Erro ao preparar gravação";
            this.cdr.markForCheck();
            return;
          }
          this.introScript = res.introScript ?? "";
          this.prepareRecordingPhase();
        },
        error: (err) => {
          this.loading = false;
          this.error = err?.error?.message || "Erro ao preparar gravação";
          this.cdr.markForCheck();
        },
      });
  }

  private prefetchQuestions(): void {
    this.analyzer
      .startStructuredInterview(
        this.resumeText,
        this.analysis,
        this.siteId,
        this.resumeId,
        this.analysisId,
      )
      .subscribe({
        next: (res: any) => {
          this.questionsLoading = false;

          if (res?.alreadyCompleted) {
            this.simulationId = res.simulationId ?? null;
            this.step = "already_done";
            this.stopIntroVideo();
            this.notifyParentReady();
            this.cdr.markForCheck();
            return;
          }

          if (!res?.success) {
            this.error =
              res?.message || res?.error || "Erro ao gerar perguntas";
            this.cdr.markForCheck();
            return;
          }

          this.persona = this.mapPersona(res.persona);
          this.simulationId = res.simulationId ?? null;
          this.candidateName = res.candidateName ?? "Candidato";
          this.writtenQuestions = this.normalizeWrittenQuestions(
            res.writtenQuestions,
          );

          while (this.writtenQuestions.length < 5) {
            this.writtenQuestions.push({
              text: "Conte mais sobre sua experiência.",
              type: "open",
              options: [],
            });
          }

          this.writtenQuestions = this.writtenQuestions.slice(0, 5);
          this.writtenAnswers = ["", "", "", "", ""];
          this.phase1Minutes = res.phase1Minutes ?? 15;
          this.questionsReady = true;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.questionsLoading = false;
          if (err?.error?.alreadyCompleted) {
            this.simulationId = err.error.simulationId ?? null;
            this.step = "already_done";
            this.stopIntroVideo();
          } else {
            this.error =
              err?.error?.message ||
              err?.error?.error ||
              "Erro ao gerar perguntas";
          }
          this.cdr.markForCheck();
        },
      });
  }

  private stopIntroVideo(resetTime = true): void {
    const el = this.introVideoRef?.nativeElement;
    if (!el) {
      return;
    }
    el.pause();
    if (resetTime) {
      el.currentTime = 0;
    }
  }

  startFeedbackPlayback(): void {
    const script = this.feedbackScript?.trim();
    if (!script || this.step !== "feedback_audio" || this.feedbackPlaying) {
      return;
    }

    this.feedbackAwaitingPlay = false;
    this.feedbackPlaying = true;
    this.feedbackFinishLock = false;
    this.cdr.markForCheck();

    void this.playFeedbackAudio(script, () => {
      this.feedbackPlaying = false;
      this.onComplete();
    });
  }

  stopFeedbackPlayback(): void {
    this.stopAllPlayback();
    this.feedbackAwaitingPlay = false;
    this.feedbackPlaying = false;
    this.onComplete();
  }

  skipFeedbackAudio(): void {
    this.stopAllPlayback();
    this.feedbackAwaitingPlay = false;
    this.feedbackPlaying = false;
    this.onComplete();
  }

  replayFeedbackAudio(): void {
    const script = (
      this.feedbackScript ||
      this.summary?.feedbackScript ||
      ""
    ).trim();
    if (!script) {
      return;
    }
    this.stopAllPlayback();
    const gender = this.persona?.voiceGender ?? "female";
    void (async () => {
      const fallbackAudio = await this.waitForFallbackAudio();
      if (fallbackAudio) {
        fallbackAudio.muted = false;
      }
      await this.playBackendSpeechScript(
        script,
        gender,
        fallbackAudio ?? undefined,
      );
    })();
  }

  downloadReport(format: "txt" | "pdf" | "docx" = "pdf"): void {
    if (!this.simulationId) {
      return;
    }

    this.analyzer.downloadInterview(this.simulationId, format).subscribe({
      next: (blob) => {
        const ext = format === "docx" ? "docx" : format;
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `entrevista_${new Date().toISOString().split("T")[0]}.${ext}`;
        link.click();
        window.URL.revokeObjectURL(url);
      },

      error: () => {
        this.error = "Erro ao baixar relatório.";
        this.cdr.markForCheck();
      },
    });
  }

  close(): void {
    this.stopAllPlayback();
    this.stopIntroVideo();
    this.clearTimer();
    this.videoExpanded = false;
    this.recordingAwaitingStart = false;
    this.feedbackAwaitingPlay = false;
    this.feedbackPlaying = false;
    this.closed.emit();
  }

  /** Para vídeo intro, feedback (Edge TTS) e microfone. */
  private stopAllPlayback(): void {
    this.playbackSession++;
    this.voice.stopSpeaking();
    this.voice.stopListening();
    this.backendSpeech.stop();
    this.resetAudioElement(this.fallbackAudioRef?.nativeElement);
    this.feedbackPlaying = false;
  }

  private isPlaybackCancelled(session: number): boolean {
    return session !== this.playbackSession;
  }

  private isPlaybackStopError(err: unknown): boolean {
    return err instanceof Error && err.message === "playback_stopped";
  }

  private tryAutoStart(): void {
    if (!this.autoStart || this.autoStartTriggered || this.step !== "idle") {
      return;
    }

    if (!this.analysis) {
      return;
    }

    if (!this.resumeText?.trim() && !this.analysisId) {
      this.error = "Texto do currículo indisponível.";
      this.notifyParentReady();
      this.cdr.markForCheck();

      return;
    }

    this.autoStartTriggered = true;

    setTimeout(() => this.begin(), 0);
  }

  private prepareRecordingPhase(): void {
    this.stopAllPlayback();
    this.stopIntroVideo();
    this.phaseTransitionLock = false;
    this.step = "phase1";
    this.recordingAwaitingStart = true;
    this.candidateDraft = "";
    this.candidateSegments = [];
    this.candidateInterim = "";
    this.cdr.markForCheck();
  }

  private onPhase1End(): void {
    this.phase1Answer = this.getRecordingAnswer();

    this.persistPhase(this.introScript, this.phase1Answer);

    this.step = "loading_feedback";

    this.cdr.markForCheck();

    this.analyzer

      .finishStructuredInterview(this.resumeText, this.analysis, {
        simulationId: this.simulationId ?? undefined,

        analysisId: this.analysisId,

        siteId: this.siteId,

        candidateName: this.candidateName,

        introScript: this.introScript,

        phase1Answer: this.phase1Answer,

        writtenQuestions: this.writtenQuestions.map((q) => q.text),

        writtenAnswers: this.writtenAnswers,

        writtenQuestionTypes: this.writtenQuestions.map((q) => q.type),
      })

      .subscribe({
        next: (res: any) => {
          if (!res?.success) {
            this.error = res?.message || "Erro ao finalizar";

            this.step = "complete";

            this.cdr.markForCheck();

            return;
          }

          this.summary = res.summary ?? res;

          this.feedbackScript = res.feedbackScript ?? "";

          this.simulationId = res.simulationId ?? this.simulationId;

          if (this.feedbackScript?.trim()) {
            this.step = "feedback_audio";
            this.feedbackAwaitingPlay = false;
            this.feedbackPlaying = true;
            this.cdr.markForCheck();
            void this.playFeedbackAudio(this.feedbackScript.trim(), () => {
              this.feedbackPlaying = false;
              this.onComplete();
            });
          } else {
            this.onComplete();
          }
        },

        error: (err) => {
          this.error = err?.error?.message || "Erro ao gerar feedback";

          this.step = "complete";

          this.cdr.markForCheck();
        },
      });
  }

  private onComplete(): void {
    this.stopAllPlayback();
    this.feedbackFinishLock = true;
    this.step = "complete";

    if (this.simulationId) {
      this.completed.emit({ simulationId: this.simulationId });
    }

    this.cdr.markForCheck();
  }

  private getRecordingAnswer(): string {
    const parts = [...this.candidateSegments];

    const interim = this.candidateInterim.trim();

    if (interim) {
      parts.push(interim);
    }

    return parts.join(" ").trim() || this.candidateDraft.trim();
  }

  private persistPhase(script: string, answer: string): void {
    if (!this.simulationId) {
      return;
    }

    this.analyzer

      .submitStructuredPhase(
        this.simulationId,
        5,
        script,
        answer,
        this.analysisId,
      )

      .subscribe({ error: () => {} });
  }

  private startPhaseTimer(minutes: number, onEnd: () => void): void {
    this.clearTimer(false);

    this.timerSeconds = minutes * 60;

    this.cdr.markForCheck();

    this.startListening();

    this.timerInterval = setInterval(() => {
      this.ngZone.run(() => {
        this.timerSeconds--;

        this.cdr.markForCheck();

        if (this.timerSeconds <= 0) {
          this.endCurrentRecordingPhase(onEnd);
        }
      });
    }, 1000);
  }

  private endCurrentRecordingPhase(onEnd?: () => void): void {
    if (this.phaseTransitionLock) {
      return;
    }

    this.phaseTransitionLock = true;

    this.clearTimer();

    if (onEnd) {
      onEnd();
    } else if (this.step === "phase1") {
      this.onPhase1End();
    }
  }

  private clearTimer(stopMic = true): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);

      this.timerInterval = null;
    }

    if (stopMic) {
      this.voice.stopListening();
    }
  }

  private startListening(): void {
    if (!this.speechSupported) {
      return;
    }

    this.voice.listen(
      (update) => {
        this.ngZone.run(() => {
          this.candidateDraft = update.transcript;

          this.candidateSegments = update.segments;

          this.candidateInterim = update.interim;

          this.cdr.markForCheck();
        });
      },

      () => {},

      () => {},
    );
  }

  /* Simli/WebRTC desativado (SIMLI_ENABLED=false) — ver simli-avatar.service.ts no git. */

  private resetAudioElement(audioEl?: HTMLAudioElement | null): void {
    if (!audioEl) {
      return;
    }
    try {
      audioEl.pause();
      audioEl.currentTime = 0;
      audioEl.removeAttribute("src");
      audioEl.load();
    } catch {
      // ignore
    }
  }

  /** Edge TTS via API — só feedback em voz (um áudio). */
  private async playBackendSpeechScript(
    script: string,
    gender: VoiceGender,
    audioEl?: HTMLAudioElement,
    session = this.playbackSession,
  ): Promise<void> {
    this.voice.stopSpeaking();

    const trimmed = script?.trim();
    if (!trimmed) {
      return;
    }

    const el = audioEl ?? this.fallbackAudioRef?.nativeElement;

    if (this.isPlaybackCancelled(session)) {
      return;
    }

    if (!el) {
      console.warn("[Entrevista] Elemento de áudio indisponível.");
      return;
    }

    this.resetAudioElement(el);

    const loadTimeoutMs = Math.min(
      120_000,
      Math.max(30_000, trimmed.length * 90),
    );

    try {
      await this.backendSpeech.play(el, trimmed, { gender }, loadTimeoutMs);
    } catch (err) {
      if (this.isPlaybackCancelled(session) || this.isPlaybackStopError(err)) {
        return;
      }
      console.warn("[Entrevista] Áudio indisponível:", err);
      throw err;
    }
  }

  private waitForFallbackAudio(maxAttempts = 60): Promise<HTMLAudioElement | null> {
    return new Promise((resolve) => {
      let attempts = 0;

      const check = () => {
        this.cdr.detectChanges();

        const fallbackAudio = this.fallbackAudioRef?.nativeElement;
        if (fallbackAudio) {
          resolve(fallbackAudio);
          return;
        }

        if (++attempts >= maxAttempts) {
          resolve(null);
          return;
        }

        setTimeout(check, 50);
      };

      check();
    });
  }

  private async playFeedbackAudio(
    script: string,
    onEnd: () => void,
  ): Promise<void> {
    const session = this.playbackSession;

    this.stopIntroVideo();
    this.step = "feedback_audio";
    this.cdr.markForCheck();

    const finish = () => {
      if (
        this.feedbackFinishLock ||
        this.isPlaybackCancelled(session)
      ) {
        return;
      }
      this.feedbackFinishLock = true;
      this.voice.stopSpeaking();
      this.ngZone.run(() => onEnd());
    };

    const gender = this.persona?.voiceGender ?? "female";

    this.feedbackFinishLock = false;

    try {
      const fallbackAudio = await this.waitForFallbackAudio();
      if (fallbackAudio) {
        fallbackAudio.muted = false;
      }
      await this.playBackendSpeechScript(
        script,
        gender,
        fallbackAudio ?? undefined,
        session,
      );
      if (this.isPlaybackCancelled(session)) {
        return;
      }
      finish();
    } catch (err) {
      if (this.isPlaybackCancelled(session) || this.isPlaybackStopError(err)) {
        return;
      }
      console.warn("[Entrevista] Feedback em voz falhou:", err);
      this.ngZone.run(() => {
        this.feedbackPlaying = false;
        this.feedbackAwaitingPlay = true;
        this.cdr.markForCheck();
      });
    }
  }

  private normalizeWrittenQuestions(raw: unknown): WrittenQuestion[] {
    if (!Array.isArray(raw)) {
      return [];
    }

    return raw
      .map((item: any) => {
        if (typeof item === "string") {
          return { text: item.trim(), type: "open" as const, options: [] };
        }

        const text = (item?.text ?? item?.question ?? "").trim();
        const type = item?.type === "choice" ? "choice" : "open";
        const options = Array.isArray(item?.options)
          ? item.options.filter((o: unknown) => typeof o === "string")
          : [];

        return {
          text,
          type: type as "open" | "choice",
          options: type === "choice" ? options : [],
        };
      })
      .filter((q) => q.text.length > 0);
  }

  private mapPersona(raw: any): Persona {
    const initials = "AR";

    return {
      name: raw?.name ?? "Entrevistadora",

      role: raw?.role ?? "Recrutadora",

      company: raw?.company ?? "",

      initials,

      avatarColor: raw?.avatarColor ?? "#6366f1",

      avatarUrl: INTERVIEW_AVATAR,

      voiceGender: "female",
    };
  }
}
