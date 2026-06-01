import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { AnalyzerService } from '../../services/analyzer.service';

/** Exibe créditos de análise e add-ons de inglês (mesmo padrão do site-header). */
@Component({
  selector: 'app-account-credits-inline',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './account-credits-inline.component.html',
  styleUrl: './account-credits-inline.component.scss'
})
export class AccountCreditsInlineComponent implements OnInit, OnDestroy {
  /** inline = uma linha; stacked = duas linhas (menu). */
  @Input() layout: 'inline' | 'stacked' = 'inline';

  /** Texto após contagem de análises: crédito(s) ou análise(s). */
  @Input() analysisLabel = 'análise(s)';

  userCredits = 0;
  englishCredits = 0;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private authService: AuthService,
    private analyzerService: AnalyzerService
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe(user => {
        this.userCredits = user?.credits ?? 0;
        if (!user) {
          this.englishCredits = 0;
        }
      });

    const user = this.authService.getCurrentUser();
    if (user) {
      this.userCredits = user.credits ?? 0;
    }

    this.loadCredits();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadCredits(): void {
    if (!this.authService.isAuthenticated()) {
      return;
    }

    this.analyzerService.getCredits().subscribe({
      next: (res: { success?: boolean; credits?: number; englishCredits?: number }) => {
        if (res?.success) {
          if (res.credits != null) {
            this.userCredits = res.credits;
          }
          this.englishCredits = res.englishCredits ?? 0;
        }
      },
      error: () => {}
    });
  }
}
