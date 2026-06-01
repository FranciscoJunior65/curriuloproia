import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { filter, Subject, takeUntil } from 'rxjs';
import { AuthService, User } from '../../services/auth.service';
import { AnalyzerService } from '../../services/analyzer.service';

@Component({
  selector: 'app-site-header',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule],
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss'
})
export class SiteHeaderComponent implements OnInit, OnDestroy {
  currentUser: User | null = null;
  isAuthenticated = false;
  isAdmin = false;
  userCredits = 0;
  englishCredits = 0;
  pendingServicesCount = 0;
  userMenuOpen = false;

  private readonly destroy$ = new Subject<void>();

  constructor(
    public authService: AuthService,
    private router: Router,
    private analyzerService: AnalyzerService
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$.pipe(takeUntil(this.destroy$)).subscribe(user => {
      this.currentUser = user;
      this.isAuthenticated = !!user;
      this.isAdmin = this.authService.isAdmin();
      this.userCredits = user?.credits ?? 0;
      if (!user) {
        this.closeUserMenu();
        this.pendingServicesCount = 0;
        this.englishCredits = 0;
      } else {
        this.loadAccountSummary();
      }
    });

    const user = this.authService.getCurrentUser();
    if (user) {
      this.currentUser = user;
      this.isAuthenticated = true;
      this.isAdmin = this.authService.isAdmin();
      this.userCredits = user.credits ?? 0;
      this.loadAccountSummary();
    }

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.closeUserMenu();
        if (this.isAuthenticated) {
          this.loadAccountSummary();
        }
      });
  }

  loadAccountSummary(): void {
    this.analyzerService.getCredits().subscribe({
      next: (res: any) => {
        if (res?.success) {
          this.userCredits = res.credits ?? this.userCredits;
          this.englishCredits = res.englishCredits ?? 0;
        }
      },
      error: () => {}
    });

    this.analyzerService.getPendingServices().subscribe({
      next: (res: any) => {
        if (res?.success) {
          this.pendingServicesCount = res.totalServicosPendentes ?? 0;
        }
      },
      error: () => {
        this.pendingServicesCount = 0;
      }
    });
  }

  ngOnDestroy(): void {
    this.setBodyScrollLocked(false);
    this.destroy$.next();
    this.destroy$.complete();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.userMenuOpen) {
      this.closeUserMenu();
    }
  }

  toggleUserMenu(): void {
    this.userMenuOpen = !this.userMenuOpen;
    this.setBodyScrollLocked(this.userMenuOpen);
  }

  closeUserMenu(): void {
    if (!this.userMenuOpen) {
      return;
    }
    this.userMenuOpen = false;
    this.setBodyScrollLocked(false);
  }

  getUserDisplayName(): string {
    if (this.currentUser?.name) {
      return this.currentUser.name;
    }
    if (this.currentUser?.email) {
      return this.currentUser.email.split('@')[0];
    }
    return 'Usuário';
  }

  getUserInitials(): string {
    const source = (this.currentUser?.name || this.currentUser?.email || 'U').trim();
    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return source.slice(0, 2).toUpperCase();
  }

  logout(): void {
    this.closeUserMenu();
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  private setBodyScrollLocked(locked: boolean): void {
    document.body.style.overflow = locked ? 'hidden' : '';
    document.body.style.overflowX = locked ? 'hidden' : '';
  }
}
