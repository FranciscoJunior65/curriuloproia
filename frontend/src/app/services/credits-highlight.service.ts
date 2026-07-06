import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface CreditsHighlightEvent {
  delta: number;
  total: number;
}

@Injectable({
  providedIn: 'root'
})
export class CreditsHighlightService {
  private readonly highlight$ = new Subject<CreditsHighlightEvent>();

  notify(delta: number, total: number): void {
    if (delta <= 0) {
      return;
    }

    this.highlight$.next({ delta, total });
  }

  watch() {
    return this.highlight$.asObservable();
  }
}
