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

export type SimliStartFailureReason =
  | 'disabled'
  | 'unauthenticated'
  | 'api_error'
  | 'webrtc_timeout'
  | 'client_load_failed';

export interface SimliStartResult {
  active: boolean;
  reason?: SimliStartFailureReason;
  detail?: string;
}

type SimliClientModule = typeof import('simli-client');

/** LogLevel.INFO — evita depender do enum quando o bundler não reexporta named exports. */
const SIMLI_LOG_LEVEL_INFO = 1;

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

  private extractHttpError(err: unknown): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const payload = (err as { error?: { error?: string; message?: string } | string }).error;
      if (typeof payload === 'string' && payload.trim()) {
        return payload.trim();
      }

      if (payload && typeof payload === 'object') {
        return payload.error?.trim() || payload.message?.trim() || '';
      }
    }

    if (err instanceof Error && err.message.trim()) {
      return err.message.trim();
    }

    return '';
  }

  async startSession(
    videoEl: HTMLVideoElement,
    audioEl: HTMLAudioElement,
    personaInitials?: string
  ): Promise<SimliStartResult> {
    const config = await this.loadConfig();
    if (!config.enabled) {
      return { active: false, reason: 'disabled' };
    }

    const authToken = this.auth.getToken();
    if (!authToken) {
      return { active: false, reason: 'unauthenticated' };
    }

    await this.stopSession();

    videoEl.muted = true;
    videoEl.autoplay = true;
    videoEl.playsInline = true;
    audioEl.autoplay = true;
    audioEl.muted = false;

    let mod: SimliClientModule;
    try {
      mod = await this.loadClientModule();
    } catch (err) {
      console.warn('[Simli] Falha ao carregar simli-client:', err);
      return {
        active: false,
        reason: 'client_load_failed',
        detail: this.extractHttpError(err) || 'módulo de vídeo indisponível'
      };
    }

    // LiveKit é o modo recomendado pela Simli em redes com firewall; P2P é fallback.
    const preferred = config.transportMode === 'p2p' ? 'p2p' : 'livekit';
    const transports: Array<'livekit' | 'p2p'> =
      preferred === 'livekit' ? ['livekit', 'p2p'] : ['p2p', 'livekit'];

    let lastDetail = '';

    for (const transport of transports) {
      try {
        const session = await this.createBrowserSession(authToken, personaInitials, config);
        const iceServers =
          transport === 'p2p'
            ? await this.fetchIceServers(authToken).catch(() => null)
            : null;

        const logLevel = mod.LogLevel?.INFO ?? SIMLI_LOG_LEVEL_INFO;

        this.client = new mod.SimliClient(
          session.sessionToken,
          videoEl,
          audioEl,
          iceServers,
          logLevel,
          transport
        );

        await Promise.race([
          this.client.start(),
          new Promise<void>((_, reject) =>
            setTimeout(
              () => reject(new Error('Tempo esgotado ao conectar avatar Simli (WebRTC).')),
              transport === 'p2p' ? 35_000 : 25_000
            )
          )
        ]);

        try {
          await videoEl.play();
        } catch {
          // autoplay pode falhar até haver stream; ignora
        }

        try {
          await audioEl.play();
        } catch {
          // idem
        }

        return { active: true };
      } catch (err) {
        lastDetail = this.extractHttpError(err) || (err instanceof Error ? err.message : String(err));
        console.warn(`[Simli] Transporte ${transport} falhou:`, lastDetail);
        await this.stopSession();
      }
    }

    const timedOut =
      lastDetail.includes('Tempo esgotado') ||
      lastDetail.toLowerCase().includes('webrtc') ||
      lastDetail.toLowerCase().includes('timeout');

    return {
      active: false,
      reason: timedOut ? 'webrtc_timeout' : 'api_error',
      detail: lastDetail || undefined
    };
  }

  private async createBrowserSession(
    authToken: string,
    personaInitials: string | undefined,
    config: SimliRuntimeConfig
  ): Promise<{ sessionToken: string; faceId: string }> {
    const sessionRes = await firstValueFrom(
      this.http.post<{ success: boolean; sessionToken: string; faceId: string; error?: string }>(
        `${environment.apiUrl}/simli/session`,
        {
          personaInitials,
          faceId: config.defaultFaceId || undefined
        },
        { headers: this.authHeaders(authToken) }
      )
    ).catch((err: unknown) => {
      const detail = this.extractHttpError(err);
      throw new Error(detail || 'Não foi possível criar sessão Simli na API.');
    });

    if (!sessionRes.success || !sessionRes.sessionToken) {
      throw new Error(sessionRes.error || 'Não foi possível iniciar avatar Simli.');
    }

    return {
      sessionToken: sessionRes.sessionToken,
      faceId: sessionRes.faceId
    };
  }

  /** Envia áudio ao Simli para lipsync. Retorna false se falhar (não chama onEnd em erro). */
  async speak(
    text: string,
    onEnd?: () => void,
    options?: { gender?: 'female' | 'male' }
  ): Promise<boolean> {
    if (!this.client || !text?.trim()) {
      return false;
    }

    this.stopRequested = false;
    this.speaking = true;

    try {
      const pcm = await this.fetchSpeechPcm(text, options?.gender);
      await this.streamPcmToSimli(pcm);
      onEnd?.();
      return true;
    } catch (err) {
      console.warn('[Simli] speak falhou:', err);
      return false;
    } finally {
      this.speaking = false;
    }
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

  /** Interrompe envio de áudio ao Simli (não afeta MP3 do feedback). */
  stopActivePlayback(): void {
    this.stopSpeaking();
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

  async warmup(): Promise<void> {
    try {
      await this.loadClientModule();
    } catch {
      // ignora — tentativa de pré-carregar o chunk do simli-client
    }
  }

  private async loadClientModule(): Promise<SimliClientModule> {
    if (!this.clientModule) {
      const loaded = await import('simli-client');
      // Angular/esbuild empacota simli-client (CJS) como `export default { SimliClient, LogLevel, ... }`.
      this.clientModule = this.normalizeSimliModule(loaded);
    }
    return this.clientModule;
  }

  private normalizeSimliModule(
    loaded: SimliClientModule & { default?: SimliClientModule }
  ): SimliClientModule {
    const mod = loaded.default ?? loaded;
    if (!mod?.SimliClient) {
      throw new Error('simli-client carregado sem SimliClient (export inválido).');
    }
    return mod;
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
