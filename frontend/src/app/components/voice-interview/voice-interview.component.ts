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

  simliUnavailableHint: string | null = null;

  videoExpanded = false;

  interviewerCaption = "";

  private timerInterval: ReturnType<typeof setInterval> | null = null;

  private autoStartTriggered = false;

  private currentVideoScript = "";

  private phaseTransitionLock = false;

  @ViewChild("simliVideo") simliVideoRef?: ElementRef<HTMLVideoElement>;

  @ViewChild("simliAudio") simliAudioRef?: ElementRef<HTMLAudioElement>;

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

      default:
        return "";
    }
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
    if (!this.resumeText || !this.analysis) {
      this.error = "Análise do currículo necessária antes da entrevista.";

      return;
    }

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

    if (!this.resumeText?.trim()) {
      this.error = "Texto do currículo indisponível.";

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

            this.cdr.markForCheck();

            return;
          }

          if (!res?.success) {
            this.error =
              res?.message || res?.error || "Erro ao iniciar entrevista";

            this.step = "idle";

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

          this.cdr.markForCheck();
        },
      });
  }

  private startPhase1(): void {
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
    this.currentVideoScript = script;

    this.interviewerCaption = script.trim();

    this.videoExpanded = true;

    this.voice.stopListening();

    const videoStep = this.inferVideoStep();

    this.step = videoStep;

    this.cdr.markForCheck();

    const elements = await this.waitForMediaElements();

    if (elements) {
      if (this.simli.isActive()) {
        this.simliActive = true;
      } else {
        this.simliBootstrapping = true;

        this.cdr.markForCheck();

        try {
          const simliConfig = await this.simli.loadConfig();
          if (!simliConfig.enabled) {
            this.simliUnavailableHint =
              "Vídeo animado indisponível: configure SIMLI_API_KEY no backend/.env e reinicie a API.";
            this.simliActive = false;
          } else {
            this.simliUnavailableHint = null;
            this.simliActive = await this.simli.startSession(
              elements.video,
              elements.audio,
              this.persona?.initials,
            );
            if (!this.simliActive) {
              this.simliUnavailableHint =
                "Não foi possível conectar o avatar em vídeo. Verifique SIMLI_API_KEY e a face ID.";
            }
          }
        } catch {
          this.simliActive = false;
          this.simliUnavailableHint =
            "Falha ao conectar avatar Simli. Confira SIMLI_API_KEY no backend/.env.";
        }

        this.simliBootstrapping = false;

        this.cdr.markForCheck();
      }
    }

    const finish = () => {
      this.ngZone.run(() => {
        this.interviewerCaption = "";

        onEnd();
      });
    };

    const gender = this.persona?.voiceGender ?? "female";

    if (this.simliActive && elements) {
      void this.simli.speak(script, finish, { gender }).catch(() => {
        void this.playSpeechFallback(script, elements.audio, finish, gender);
      });
    } else if (elements) {
      void this.playSpeechFallback(script, elements.audio, finish, gender);
    } else {
      this.voice.speak(script, finish, { gender });
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
    try {
      await this.simli.playBackendSpeech(audioEl, script, { gender });

      onEnd();
    } catch {
      this.simliActive = false;

      this.cdr.markForCheck();

      this.voice.speak(script, onEnd, { gender });
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
    const audioEl = elements?.audio ?? this.simliAudioRef?.nativeElement;

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
  ): Promise<{ video: HTMLVideoElement; audio: HTMLAudioElement } | null> {
    return new Promise((resolve) => {
      let attempts = 0;

      const check = () => {
        this.cdr.detectChanges();

        const video = this.simliVideoRef?.nativeElement;

        const audio = this.simliAudioRef?.nativeElement;

        if (video && audio) {
          resolve({ video, audio });

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
