import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export interface SimliRuntimeConfig {
  enabled: boolean;
  transportMode: 'livekit' | 'p2p';
  defaultFaceId: string;
  faceIdsByPersona: Record<string, string>;
}

type SimliClientModule = typeof import('simli-client');

@Injectable({ providedIn: 'root' })
export class SimliAvatarService {
  private config: SimliRuntimeConfig | null = null;
  private client: any = null;
  private clientModule: SimliClientModule | null = null;
  private audioContext: AudioContext | null = null;
  private speaking = false;
  private stopRequested = false;

  constructor(
    private http: HttpClient,
    private auth: AuthService
  ) {}

  async loadConfig(): Promise<SimliRuntimeConfig> {
    if (this.config) {
      return this.config;
    }

    const res = await firstValueFrom(
      this.http.get<{ success: boolean; config: SimliRuntimeConfig }>(`${environment.apiUrl}/simli/config`)
    );

    this.config = {
      enabled: !!res.config?.enabled,
      transportMode: (res.config?.transportMode === 'p2p' ? 'p2p' : 'livekit'),
      defaultFaceId: res.config?.defaultFaceId ?? '',
      faceIdsByPersona: res.config?.faceIdsByPersona ?? {}
    };

    return this.config;
  }

  isActive(): boolean {
    return !!this.client;
  }

  async startSession(
    videoEl: HTMLVideoElement,
    audioEl: HTMLAudioElement,
    personaInitials?: string
  ): Promise<boolean> {
    const config = await this.loadConfig();
    if (!config.enabled) {
      return false;
    }

    await this.stopSession();

    const token = this.auth.getToken();
    if (!token) {
      return false;
    }

    const sessionRes = await firstValueFrom(
      this.http.post<{ success: boolean; sessionToken: string; faceId: string; error?: string }>(
        `${environment.apiUrl}/simli/session`,
        {
          personaInitials,
          faceId: config.defaultFaceId || undefined
        },
        { headers: this.authHeaders(token) }
      )
    );

    if (!sessionRes.success || !sessionRes.sessionToken) {
      throw new Error(sessionRes.error || 'Não foi possível iniciar avatar Simli.');
    }

    const mod = await this.loadClientModule();
    const transports: Array<'livekit' | 'p2p'> =
      config.transportMode === 'p2p' ? ['p2p', 'livekit'] : ['livekit', 'p2p'];

    let lastError: unknown;
    for (const transport of transports) {
      try {
        const iceServers =
          transport === 'p2p'
            ? await this.fetchIceServers(token).catch(() => null)
            : null;

        this.client = new mod.SimliClient(
          sessionRes.sessionToken,
          videoEl,
          audioEl,
          iceServers,
          mod.LogLevel.INFO,
          transport
        );

        await Promise.race([
          this.client.start(),
          new Promise<void>((_, reject) =>
            setTimeout(() => reject(new Error('Tempo esgotado ao conectar avatar Simli.')), 25_000)
          )
        ]);

        lastError = null;
        break;
      } catch (err) {
        lastError = err;
        await this.stopSession();
      }
    }

    if (lastError) {
      throw lastError;
    }

    videoEl.muted = true;
    try {
      await videoEl.play();
    } catch {
      // autoplay pode falhar até haver stream; ignora
    }

    return true;
  }

  async speak(
    text: string,
    onEnd?: () => void,
    options?: { gender?: 'female' | 'male' }
  ): Promise<void> {
    if (!this.client || !text?.trim()) {
      onEnd?.();
      return;
    }

    this.stopRequested = false;
    this.speaking = true;

    try {
      const pcm = await this.fetchSpeechPcm(text, options?.gender);
      await this.streamPcmToSimli(pcm);
    } finally {
      this.speaking = false;
      if (!this.stopRequested) {
        onEnd?.();
      }
    }
  }

  /** Voz do backend (Edge TTS) sem depender do buffer do Simli — evita fallback “GPS” do navegador. */
  async playBackendSpeech(
    audioEl: HTMLAudioElement,
    text: string,
    options?: { gender?: 'female' | 'male' }
  ): Promise<void> {
    const trimmed = text?.trim();
    if (!trimmed) {
      return;
    }

    const token = this.auth.getToken();
    if (!token) {
      throw new Error('Não autenticado');
    }

    const voice = options?.gender === 'male' ? 'onyx' : 'nova';
    const mp3 = await firstValueFrom(
      this.http.post(`${environment.apiUrl}/simli/speech`, { text: trimmed, voice }, {
        headers: this.authHeaders(token),
        responseType: 'arraybuffer'
      })
    );

    const blob = new Blob([mp3], { type: 'audio/mpeg' });
    const url = URL.createObjectURL(blob);

    return new Promise<void>((resolve, reject) => {
      const cleanup = () => URL.revokeObjectURL(url);
      audioEl.onended = () => {
        cleanup();
        resolve();
      };
      audioEl.onerror = () => {
        cleanup();
        reject(new Error('Falha ao reproduzir áudio'));
      };
      audioEl.src = url;
      audioEl.play().catch(err => {
        cleanup();
        reject(err);
      });
    });
  }

  private async fetchSpeechPcm(
    text: string,
    gender?: 'female' | 'male'
  ): Promise<Uint8Array> {
    const token = this.auth.getToken();
    if (!token) {
      throw new Error('Não autenticado');
    }

    const voice = gender === 'male' ? 'onyx' : 'nova';
    const mp3 = await firstValueFrom(
      this.http.post(`${environment.apiUrl}/simli/speech`, { text, voice }, {
        headers: this.authHeaders(token),
        responseType: 'arraybuffer'
      })
    );

    return this.mp3ToPcm16Mono16k(mp3);
  }

  stopSpeaking(): void {
    this.stopRequested = true;
    this.speaking = false;
    try {
      this.client?.ClearBuffer?.();
    } catch {
      // ignore
    }
  }

  async stopSession(): Promise<void> {
    this.stopSpeaking();
    if (this.client) {
      try {
        await this.client.stop();
      } catch {
        // ignore
      }
      this.client = null;
    }

    if (this.audioContext) {
      try {
        await this.audioContext.close();
      } catch {
        // ignore
      }
      this.audioContext = null;
    }
  }

  private async loadClientModule(): Promise<SimliClientModule> {
    if (!this.clientModule) {
      this.clientModule = await import('simli-client');
    }
    return this.clientModule;
  }

  private authHeaders(token: string): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${token}` });
  }

  private async fetchIceServers(token: string): Promise<RTCIceServer[] | null> {
    const res = await firstValueFrom(
      this.http.get<{ success: boolean; iceServers: RTCIceServer[] }>(
        `${environment.apiUrl}/simli/ice`,
        { headers: this.authHeaders(token) }
      )
    );
    return res.iceServers?.length ? res.iceServers : null;
  }

  private async mp3ToPcm16Mono16k(mp3: ArrayBuffer): Promise<Uint8Array> {
    if (!this.audioContext) {
      this.audioContext = new AudioContext();
    }

    const decoded = await this.audioContext.decodeAudioData(mp3.slice(0));
    const offline = new OfflineAudioContext(1, Math.ceil(decoded.duration * 16000), 16000);
    const source = offline.createBufferSource();
    source.buffer = decoded;
    source.connect(offline.destination);
    source.start(0);
    const rendered = await offline.startRendering();
    const channel = rendered.getChannelData(0);
    const pcm = new Int16Array(channel.length);

    for (let i = 0; i < channel.length; i++) {
      const sample = Math.max(-1, Math.min(1, channel[i]));
      pcm[i] = sample < 0 ? sample * 0x8000 : sample * 0x7fff;
    }

    return new Uint8Array(pcm.buffer);
  }

  private async streamPcmToSimli(pcm: Uint8Array): Promise<void> {
    const chunkSize = 6000;
    const chunkDelayMs = 165;
    const startedAt = performance.now();

    for (let offset = 0; offset < pcm.length; offset += chunkSize) {
      if (this.stopRequested) {
        break;
      }

      const chunk = pcm.subarray(offset, Math.min(offset + chunkSize, pcm.length));
      this.client.sendAudioData(chunk);
      await this.delay(chunkDelayMs);
    }

    if (this.stopRequested) {
      return;
    }

    // Aguarda duração real do áudio (16 kHz mono PCM16) + margem para lipsync
    const totalDurationMs = (pcm.length / 2 / 16000) * 1000;
    const elapsedMs = performance.now() - startedAt;
    const remainingMs = Math.max(600, totalDurationMs - elapsedMs + 900);
    await this.delay(remainingMs);
  }

  private delay(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}
