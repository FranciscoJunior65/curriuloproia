import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

/** Edge TTS via API — independente do Simli (intro fallback e feedback em voz). */
@Injectable({ providedIn: 'root' })
export class BackendSpeechService {
  private activePlaybackReject: ((err: Error) => void) | null = null;

  constructor(
    private http: HttpClient,
    private auth: AuthService,
  ) {}

  async play(
    audioEl: HTMLAudioElement,
    text: string,
    options?: { gender?: 'female' | 'male' },
    loadTimeoutMs = 20_000,
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
      this.http.post(
        `${environment.apiUrl}/simli/speech`,
        { text: trimmed, voice },
        {
          headers: new HttpHeaders({ Authorization: `Bearer ${token}` }),
          responseType: 'arraybuffer',
        },
      ),
    );

    if (!mp3 || mp3.byteLength < 128) {
      throw new Error('Áudio vazio ou inválido.');
    }

    const blob = new Blob([mp3], { type: 'audio/mpeg' });
    const url = URL.createObjectURL(blob);

    return new Promise<void>((resolve, reject) => {
      let settled = false;
      const cleanup = () => {
        audioEl.onended = null;
        audioEl.onerror = null;
        audioEl.onloadedmetadata = null;
        if (this.activePlaybackReject === reject) {
          this.activePlaybackReject = null;
        }
        URL.revokeObjectURL(url);
      };
      const done = () => {
        if (settled) {
          return;
        }
        settled = true;
        cleanup();
        resolve();
      };
      const fail = (message: string) => {
        if (settled) {
          return;
        }
        settled = true;
        cleanup();
        reject(new Error(message));
      };

      this.activePlaybackReject = reject;

      const loadTimeout = setTimeout(() => {
        fail('Tempo esgotado ao carregar áudio.');
      }, loadTimeoutMs);

      audioEl.onended = () => {
        clearTimeout(loadTimeout);
        done();
      };
      audioEl.onerror = () => {
        clearTimeout(loadTimeout);
        fail('Falha ao reproduzir áudio');
      };

      try {
        audioEl.pause();
        audioEl.currentTime = 0;
      } catch {
        // ignore
      }

      audioEl.src = url;
      audioEl.load();
      void audioEl.play().catch(() => {
        // Autoplay pode rejeitar a promise mesmo com áudio tocando — aguarda antes de falhar.
        window.setTimeout(() => {
          if (settled) {
            return;
          }
          const playing =
            !audioEl.paused &&
            !audioEl.ended &&
            (audioEl.currentTime > 0 || audioEl.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA);
          if (playing) {
            return;
          }
          clearTimeout(loadTimeout);
          fail('Autoplay bloqueado');
        }, 300);
      });
    });
  }

  stop(): void {
    if (this.activePlaybackReject) {
      const reject = this.activePlaybackReject;
      this.activePlaybackReject = null;
      reject(new Error('playback_stopped'));
    }
  }
}
