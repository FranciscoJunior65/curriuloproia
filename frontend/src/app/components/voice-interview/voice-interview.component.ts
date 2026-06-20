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

import { SimliAvatarService } from "../../services/simli-avatar.service";

import {
  VoiceSpeechService,
  VoiceGender,
} from "../../services/voice-speech.service";

type InterviewStep =
  | "idle"
  | "loading"
  | "already_done"
  | "written_questions"
  | "loading_intro"
  | "intro_video"
  | "phase1"
  | "loading_feedback"
  | "feedback_audio"
  | "feedback_video"
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

const INTERVIEW_AVATAR = "assets/imagens/avatar.png";

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

  videoExpanded = false;

  interviewerCaption = "";

  private timerInterval: ReturnType<typeof setInterval> | null = null;

  private autoStartTriggered = false;

  private currentVideoScript = "";

  private phaseTransitionLock = false;

  @ViewChild("simliVideo") simliVideoRef?: ElementRef<HTMLVideoElement>;

  @ViewChild("simliAudio") simliAudioRef?: ElementRef<HTMLAudioElement>;

  @ViewChild("fallbackAudio") fallbackAudioRef?: ElementRef<HTMLAudioElement>;

  /** Conexão Simli iniciada em loading_intro — não compete com o áudio de fallback. */
  private simliPreconnectTask: Promise<boolean> | null = null;

  private introFinishLock = false;

  constructor(
    private analyzer: AnalyzerService,

    private voice: VoiceSpeechService,

    private simli: SimliAvatarService,

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
    this.clearTimer();

    this.voice.stopSpeaking();

    this.voice.stopListening();

    void this.simli.stopSession();
  }

  get simliStageVisible(): boolean {
    return this.simliActive || this.simliBootstrapping;
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

      case "loading_intro":
        return "Preparando apresentação em vídeo...";

      case "intro_video":
        return "Entrevistador se apresentando...";

      case "phase1":
        return `Fale sobre você (${this.timerLabel} restantes)`;

      case "loading_feedback":
        return "Analisando respostas e gerando feedback...";

      case "feedback_audio":
        return "Ouvindo feedback da entrevista...";

      case "feedback_video":
        return "Feedback da entrevista...";

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

  get isVideoPhase(): boolean {
    return this.step === "intro_video" || this.simliBootstrapping;
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
    if (!this.isRecordingPhase || this.phaseTransitionLock) {
      return;
    }

    this.endCurrentRecordingPhase();
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

          this.startInterview();
        },

        error: () => this.startInterview(),
      });
    } else {
      this.startInterview();
    }
  }

  continueToVoicePhase(): void {
    if (!this.writtenQuestionsComplete) {
      this.error = "Responda pelo menos 3 das 5 perguntas para continuar.";

      this.cdr.markForCheck();

      return;
    }

    this.error = null;

    this.step = "loading_intro";

    this.cdr.markForCheck();

    void this.simli.warmup();
    void this.unlockIntroAudioAfterRender();
    this.simliPreconnectTask = this.preconnectSimli();

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
          if (!res?.success) {
            this.error = res?.message || "Erro ao iniciar fase em vídeo";

            this.step = "written_questions";

            this.cdr.markForCheck();

            return;
          }

          this.introScript = res.introScript ?? "";

          void this.playVideoScript(this.introScript, () => this.startPhase1());
        },

        error: (err) => {
          this.error = err?.error?.message || "Erro ao preparar vídeo";

          this.step = "written_questions";

          this.cdr.markForCheck();
        },
      });
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
    const gender = this.persona?.voiceGender ?? "female";
    this.voice.speak(script, undefined, { gender });
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
    this.clearTimer();

    this.videoExpanded = false;

    this.interviewerCaption = "";

    this.voice.stopSpeaking();

    this.voice.stopListening();

    void this.simli.stopSession();

    this.closed.emit();
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

  private startInterview(): void {
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
          this.loading = false;

          if (res?.alreadyCompleted) {
            this.simulationId = res.simulationId ?? null;

            this.step = "already_done";

            this.notifyParentReady();
            this.cdr.markForCheck();

            return;
          }

          if (!res?.success) {
            this.error =
              res?.message || res?.error || "Erro ao iniciar entrevista";

            this.step = "idle";

            this.notifyParentReady();
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

          this.step = "written_questions";

          this.notifyParentReady();
          this.cdr.markForCheck();
        },

        error: (err) => {
          this.loading = false;

          this.step = "idle";

          if (err?.error?.alreadyCompleted) {
            this.simulationId = err.error.simulationId ?? null;

            this.step = "already_done";
          } else {
            this.error =
              err?.error?.message ||
              err?.error?.error ||
              "Erro ao iniciar entrevista";
          }

          this.notifyParentReady();
          this.cdr.markForCheck();
        },
      });
  }

  private startPhase1(): void {
    if (this.step !== "intro_video") {
      console.warn(
        "[Entrevista] startPhase1 ignorado — etapa atual:",
        this.step,
      );
      return;
    }

    this.stopSimliForRecording();

    this.interviewerCaption = "";

    this.phaseTransitionLock = false;

    this.step = "phase1";

    this.candidateDraft = "";

    this.candidateSegments = [];

    this.candidateInterim = "";

    this.startPhaseTimer(this.phase1Minutes, () => this.onPhase1End());
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
            void this.playFeedbackAudio(this.feedbackScript, () => this.onComplete());
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
    this.step = "complete";

    if (this.simulationId) {
      this.completed.emit({ simulationId: this.simulationId });
    }

    void this.simli.stopSession();

    this.simliActive = false;

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

  private async playVideoScript(
    script: string,
    onEnd: () => void,
  ): Promise<void> {
    const trimmed = script?.trim();
    if (!trimmed) {
      console.warn("[Entrevista] Texto da apresentação vazio — pulando para gravação.");
      this.ngZone.run(() => onEnd());
      return;
    }

    this.introFinishLock = false;
    this.currentVideoScript = trimmed;
    this.interviewerCaption = trimmed;
    this.videoExpanded = true;
    this.voice.stopListening();
    this.step = this.inferVideoStep();
    this.cdr.markForCheck();

    if (!this.simliActive && this.simliPreconnectTask) {
      this.simliBootstrapping = true;
      this.cdr.markForCheck();
      void this.runSimliBootstrapTimeout(10_000);
    }

    const elements = await this.waitForMediaElements();
    const gender = this.persona?.voiceGender ?? "female";
    const finish = () => {
      if (this.introFinishLock) {
        return;
      }
      this.introFinishLock = true;
      this.ngZone.run(() => {
        this.interviewerCaption = "";
        onEnd();
      });
    };

    // Lipsync só se o vídeo ficar pronto enquanto o áudio ainda toca.
    const lipsync = this.startIntroLipsync(trimmed, elements, gender);

    try {
      if (elements) {
        elements.fallbackAudio.muted = false;
        await this.playIntroAudio(trimmed, elements.fallbackAudio, gender);
      } else {
        await this.voiceSpeakAsync(trimmed, () => {}, gender);
      }
    } catch (err) {
      console.warn("[Entrevista] Áudio da apresentação falhou:", err);
      this.simli.stopSpeaking();
      await this.voiceSpeakAsync(trimmed, () => {}, gender);
    }

    await Promise.race([lipsync, this.delay(4_000)]);

    if (this.simliActive) {
      await this.delay(1_200);
    }

    finish();
  }

  private runSimliBootstrapTimeout(ms: number): void {
    setTimeout(() => {
      if (this.step !== "intro_video" || this.simliActive) {
        return;
      }
      this.simliBootstrapping = false;
      this.cdr.markForCheck();
    }, ms);
  }

  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  private waitForSimliVideoFrame(
    video: HTMLVideoElement,
    maxMs = 12_000,
  ): Promise<boolean> {
    if (video.videoWidth > 0 && video.videoHeight > 0) {
      return Promise.resolve(true);
    }

    return new Promise((resolve) => {
      const done = (ok: boolean) => {
        cleanup();
        resolve(ok);
      };
      const check = () => {
        if (video.videoWidth > 0 && video.videoHeight > 0) {
          done(true);
        }
      };
      const cleanup = () => {
        clearTimeout(timeout);
        video.removeEventListener("loadeddata", check);
        video.removeEventListener("playing", check);
        video.removeEventListener("resize", check);
      };

      const timeout = setTimeout(() => done(false), maxMs);
      video.addEventListener("loadeddata", check);
      video.addEventListener("playing", check);
      video.addEventListener("resize", check);
      void video.play().catch(() => {});
      check();
    });
  }

  /** Desbloqueia autoplay no clique do usuário (antes da API/Simli demorarem). */
  private async unlockIntroAudioAfterRender(): Promise<void> {
    const elements = await this.waitForMediaElements(60);
    if (!elements) {
      return;
    }

    const audio = elements.fallbackAudio;
    try {
      audio.muted = true;
      await audio.play();
      audio.pause();
      audio.currentTime = 0;
      audio.removeAttribute("src");
      audio.load();
    } catch {
      // ignora — tentativa de desbloquear autoplay
    } finally {
      audio.muted = false;
    }
  }

  private startIntroLipsync(
    script: string,
    elements: {
      video: HTMLVideoElement;
      simliAudio: HTMLAudioElement;
      fallbackAudio: HTMLAudioElement;
    } | null,
    gender: VoiceGender,
  ): Promise<void> {
    if (!elements) {
      return Promise.resolve();
    }

    const trySpeak = async (): Promise<void> => {
      if (!this.simli.isActive() || this.step !== "intro_video") {
        return;
      }

      const audio = elements.fallbackAudio;
      const audioStillPlaying =
        !audio.ended && audio.currentTime > 0.05 && !audio.paused;

      if (!audioStillPlaying) {
        console.warn(
          "[Simli] Vídeo conectou após o áudio — mantendo foto estática nesta fala.",
        );
        return;
      }

      this.simliActive = true;
      this.simliBootstrapping = false;
      elements.simliAudio.muted = true;
      this.cdr.markForCheck();

      const spoke = await this.simli.speak(script, undefined, { gender });
      if (!spoke) {
        console.warn("[Simli] Lipsync não iniciou durante a apresentação.");
      }
    };

    const whenVideoReady = async (): Promise<void> => {
      const ready = this.simliPreconnectTask
        ? await this.simliPreconnectTask.catch(() => false)
        : this.simli.isActive();

      if (!ready) {
        return;
      }

      const hasFrame = await this.waitForSimliVideoFrame(elements.video, 4_000);
      if (hasFrame) {
        this.simliActive = true;
        this.simliBootstrapping = false;
        this.cdr.markForCheck();
      }

      await trySpeak();
    };

    if (this.simli.isActive()) {
      return whenVideoReady();
    }

    if (!this.simliPreconnectTask) {
      return Promise.resolve();
    }

    return whenVideoReady();
  }

  private async playIntroAudio(
    script: string,
    audioEl: HTMLAudioElement,
    gender: VoiceGender,
  ): Promise<void> {
    try {
      await this.simli.playBackendSpeech(audioEl, script, { gender });
    } catch (err) {
      console.warn("[Entrevista] MP3 indisponível, voz do navegador:", err);
      await this.voiceSpeakAsync(script, () => {}, gender);
    }
  }

  /** Inicia conexão Simli cedo (durante loading_intro) para ganhar tempo no WebRTC. */
  private async preconnectSimli(): Promise<boolean> {
    const elements = await this.waitForMediaElements(80);
    if (!elements) {
      return false;
    }
    return this.tryConnectSimliVideo(elements);
  }

  /** Tenta vídeo animado — usa áudio dedicado ao WebRTC (não o de fallback MP3). */
  private async tryConnectSimliVideo(elements: {
    video: HTMLVideoElement;
    simliAudio: HTMLAudioElement;
  }): Promise<boolean> {
    if (this.simli.isActive()) {
      const hasFrame = await this.waitForSimliVideoFrame(elements.video, 4_000);
      this.simliActive = hasFrame;
      this.simliBootstrapping = false;
      this.cdr.markForCheck();
      return hasFrame;
    }

    try {
      const simliConfig = await this.simli.loadConfig();
      if (!simliConfig.enabled) {
        return false;
      }

      this.simliBootstrapping = true;
      this.cdr.markForCheck();

      const result = await this.simli.startSession(
        elements.video,
        elements.simliAudio,
        this.persona?.initials,
      );

      if (!result.active) {
        console.warn(
          "[Simli] Vídeo indisponível:",
          result.reason ?? "desconhecido",
          result.detail ?? "",
        );
        return false;
      }

      const hasFrame = await this.waitForSimliVideoFrame(elements.video, 12_000);
      this.simliActive = hasFrame;

      if (!hasFrame) {
        console.warn(
          "[Simli] LiveKit conectou, mas o vídeo não exibiu frames a tempo.",
        );
      }

      return hasFrame;
    } catch (err) {
      this.simliActive = false;
      console.warn("[Simli] Falha ao conectar vídeo:", err);
      return false;
    } finally {
      this.simliBootstrapping = false;
      this.cdr.markForCheck();
    }
  }

  private stopSimliForRecording(): void {
    void this.simli.stopSession();

    this.simliActive = false;

    this.simliBootstrapping = false;
  }

  private async playSpeechFallback(
    script: string,

    audioEl: HTMLAudioElement,

    onEnd: () => void,

    gender: VoiceGender,
  ): Promise<void> {
    const trimmed = script?.trim();
    if (!trimmed) {
      onEnd();
      return;
    }

    try {
      await this.simli.playBackendSpeech(audioEl, trimmed, { gender });

      onEnd();
    } catch {
      this.voice.speak(trimmed, onEnd, { gender });
    }
  }

  private async playFeedbackAudio(
    script: string,
    onEnd: () => void,
  ): Promise<void> {
    this.step = "feedback_audio";
    this.videoExpanded = false;
    this.interviewerCaption = "";
    this.cdr.markForCheck();

    void this.simli.stopSession();
    this.simliActive = false;
    this.simliBootstrapping = false;

    const finish = () => {
      this.ngZone.run(() => {
        this.interviewerCaption = "";
        onEnd();
      });
    };

    const gender = this.persona?.voiceGender ?? "female";
    const elements = await this.waitForMediaElements(10);
    const audioEl = elements?.fallbackAudio ?? this.fallbackAudioRef?.nativeElement;

    if (audioEl) {
      try {
        await this.simli.playBackendSpeech(audioEl, script, { gender });
        finish();
        return;
      } catch {
        // fallback abaixo
      }
    }

    this.voice.speak(script, finish, { gender });
  }

  private inferVideoStep(): InterviewStep {
    return "intro_video";
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

  private waitForMediaElements(
    maxAttempts = 30,
  ): Promise<{
    video: HTMLVideoElement;
    simliAudio: HTMLAudioElement;
    fallbackAudio: HTMLAudioElement;
  } | null> {
    return new Promise((resolve) => {
      let attempts = 0;

      const check = () => {
        this.cdr.detectChanges();

        const video = this.simliVideoRef?.nativeElement;
        const simliAudio = this.simliAudioRef?.nativeElement;
        const fallbackAudio = this.fallbackAudioRef?.nativeElement;

        if (video && simliAudio && fallbackAudio) {
          resolve({ video, simliAudio, fallbackAudio });
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

  private voiceSpeakAsync(
    script: string,
    onEnd: () => void,
    gender: VoiceGender,
  ): Promise<void> {
    return new Promise((resolve) => {
      this.voice.speak(script, () => {
        onEnd();
        resolve();
      }, { gender });
    });
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
