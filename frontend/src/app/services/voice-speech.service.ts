import { Injectable } from '@angular/core';

export type VoiceUiState = 'idle' | 'speaking' | 'listening' | 'processing' | 'reviewing';
export type VoiceGender = 'female' | 'male';

export interface SpeakOptions {
  gender?: VoiceGender;
}

@Injectable({ providedIn: 'root' })
export class VoiceSpeechService {
  private synth = typeof window !== 'undefined' ? window.speechSynthesis : null;
  private recognition: any = null;
  private voicesReady = false;
  private speakSafetyTimer: ReturnType<typeof setTimeout> | null = null;
  private speakPollTimer: ReturnType<typeof setInterval> | null = null;
  private speakSession = 0;

  constructor() {
    if (this.synth && typeof window !== 'undefined') {
      this.loadVoices();
      this.synth.onvoiceschanged = () => this.loadVoices();
    }
  }

  isSupported(): boolean {
    return !!(this.synth && this.getRecognitionCtor());
  }

  getRecognitionCtor(): any {
    const w = window as any;
    return w.SpeechRecognition || w.webkitSpeechRecognition || null;
  }

  speak(text: string, onEnd?: () => void, options?: SpeakOptions): void {
    if (!this.synth || !text?.trim()) {
      onEnd?.();
      return;
    }

    const chunks = this.splitIntoChunks(text.trim());
    this.stopSpeaking();
    const session = ++this.speakSession;
    const finishOnce = this.once(onEnd);
    const maxMs = Math.min(90_000, Math.max(8_000, text.trim().length * 55));
    this.speakSafetyTimer = setTimeout(() => {
      if (session !== this.speakSession) {
        return;
      }
      this.stopSpeaking();
      finishOnce();
    }, maxMs);
    this.speakChunksSequential(chunks, 0, finishOnce, options, session);
  }

  stopSpeaking(): void {
    this.speakSession++;
    if (this.speakSafetyTimer) {
      clearTimeout(this.speakSafetyTimer);
      this.speakSafetyTimer = null;
    }
    if (this.speakPollTimer) {
      clearInterval(this.speakPollTimer);
      this.speakPollTimer = null;
    }
    this.synth?.cancel();
  }

  listen(
    onResult: (transcript: string) => void,
    onError?: (message: string) => void,
    onEnd?: (finalTranscript: string) => void
  ): void {
    const Ctor = this.getRecognitionCtor();
    if (!Ctor) {
      onError?.('Reconhecimento de voz não suportado neste navegador. Use Chrome ou Edge.');
      return;
    }

    this.stopListening();
    const recognition = new Ctor();
    recognition.lang = 'pt-BR';
    recognition.interimResults = true;
    recognition.continuous = true;
    recognition.maxAlternatives = 1;

    let finalText = '';

    recognition.onresult = (event: any) => {
      let interim = '';
      for (let i = 0; i < event.results.length; i++) {
        const part = event.results[i][0]?.transcript ?? '';
        if (event.results[i].isFinal) {
          finalText += part;
        } else {
          interim += part;
        }
      }
      const preview = (finalText + interim).trim();
      if (preview) {
        onResult(preview);
      }
    };

    recognition.onerror = (event: any) => {
      if (event?.error !== 'aborted') {
        onError?.(
          event?.error === 'no-speech'
            ? 'Não ouvi nada. Tente falar novamente.'
            : 'Erro ao captar voz.'
        );
      }
    };

    recognition.onend = () => {
      const text = finalText.trim();
      onEnd?.(text);
      this.recognition = null;
    };

    this.recognition = recognition;
    try {
      recognition.start();
    } catch {
      onError?.('Não foi possível iniciar o microfone. Tente novamente.');
    }
  }

  stopListening(): void {
    try {
      this.recognition?.stop();
    } catch {
      // ignore
    }
    this.recognition = null;
  }

  private speakChunksSequential(
    chunks: string[],
    index: number,
    onEnd: () => void,
    options: SpeakOptions | undefined,
    session: number
  ): void {
    if (session !== this.speakSession) {
      return;
    }

    if (index >= chunks.length) {
      if (this.speakSafetyTimer) {
        clearTimeout(this.speakSafetyTimer);
        this.speakSafetyTimer = null;
      }
      onEnd();
      return;
    }

    const chunk = chunks[index];
    let finished = false;
    const complete = () => {
      if (finished || session !== this.speakSession) {
        return;
      }
      finished = true;
      if (this.speakPollTimer) {
        clearInterval(this.speakPollTimer);
        this.speakPollTimer = null;
      }
      this.speakChunksSequential(chunks, index + 1, onEnd, options, session);
    };

    const synth = this.synth!;
    const utterance = new SpeechSynthesisUtterance(chunk);
    utterance.lang = 'pt-BR';
    utterance.rate = 0.9;

    const voice = this.pickVoice(options?.gender ?? 'female');
    if (voice) {
      utterance.voice = voice;
    }
    if (options?.gender === 'female') {
      utterance.pitch = 1.12;
    } else if (options?.gender === 'male') {
      utterance.pitch = 0.88;
    }

    const timeoutMs = Math.min(45_000, Math.max(2_500, chunk.length * 70));
    const safety = setTimeout(() => complete(), timeoutMs);
    utterance.onend = () => {
      clearTimeout(safety);
      complete();
    };
    utterance.onerror = () => {
      clearTimeout(safety);
      complete();
    };

    let silentTicks = 0;
    if (this.speakPollTimer) {
      clearInterval(this.speakPollTimer);
    }
    this.speakPollTimer = setInterval(() => {
      if (session !== this.speakSession || finished) {
        return;
      }
      if (!synth.speaking && !synth.pending) {
        silentTicks += 1;
        if (silentTicks >= 2) {
          clearTimeout(safety);
          complete();
        }
      } else {
        silentTicks = 0;
      }
    }, 350);

    try {
      synth.resume();
    } catch {
      // ignore
    }
    synth.speak(utterance);

    // Chrome/Safari: às vezes a fila não inicia sem um resume após speak
    setTimeout(() => {
      if (session !== this.speakSession) {
        return;
      }
      try {
        if (!synth.speaking && !synth.pending) {
          synth.resume();
          synth.speak(utterance);
        }
      } catch {
        // ignore
      }
    }, 120);
  }

  private once(fn?: () => void): () => void {
    let called = false;
    return () => {
      if (called) {
        return;
      }
      called = true;
      if (this.speakSafetyTimer) {
        clearTimeout(this.speakSafetyTimer);
        this.speakSafetyTimer = null;
      }
      fn?.();
    };
  }

  private splitIntoChunks(text: string): string[] {
    const parts = text
      .split(/(?<=[.!?…])\s+/)
      .map(s => s.trim())
      .filter(s => s.length > 0);
    if (parts.length === 0) {
      return [text];
    }
    // Frases muito longas: quebra adicional
    const out: string[] = [];
    for (const p of parts) {
      if (p.length > 180) {
        const sub = p.match(/[^,;]{1,140}[,.;]?/g) ?? [p];
        out.push(...sub.map(s => s.trim()).filter(Boolean));
      } else {
        out.push(p);
      }
    }
    return out.length ? out : [text];
  }

  private pickVoice(gender: VoiceGender): SpeechSynthesisVoice | null {
    const voices = (this.synth?.getVoices() ?? []).filter(v =>
      v.lang.toLowerCase().startsWith('pt')
    );
    if (voices.length === 0) {
      return null;
    }

    const femaleHints = [
      'female',
      'luciana',
      'maria',
      'raquel',
      'francisca',
      'heloisa',
      'heloísa',
      'vitória',
      'vitoria',
      'camila',
      'lucia',
      'lúcia',
      'fernanda',
      'letícia',
      'leticia',
      'gabriela'
    ];
    const maleHints = [
      'male',
      'felipe',
      'daniel',
      'thiago',
      'jorge',
      'mateus',
      'ricardo',
      'paulo',
      'eduardo',
      'luciano'
    ];

    const score = (v: SpeechSynthesisVoice): number => {
      const n = v.name.toLowerCase();
      let s = 0;
      if (v.lang.toLowerCase().startsWith('pt-br')) {
        s += 2;
      }
      if (gender === 'female') {
        if (femaleHints.some(h => n.includes(h))) {
          s += 10;
        }
        if (maleHints.some(h => n.includes(h)) && !n.includes('female')) {
          s -= 8;
        }
      } else {
        if (maleHints.some(h => n.includes(h))) {
          s += 10;
        }
        if (femaleHints.some(h => n.includes(h))) {
          s -= 8;
        }
      }
      return s;
    };

    return [...voices].sort((a, b) => score(b) - score(a))[0] ?? voices[0];
  }

  private loadVoices(): void {
    const voices = this.synth?.getVoices() ?? [];
    if (voices.length > 0) {
      this.voicesReady = true;
    }
  }
}
