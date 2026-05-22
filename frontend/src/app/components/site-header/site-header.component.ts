import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { Subject, takeUntil } from 'rxjs';
import { AuthService, User } from '../../services/auth.service';
import { AnalyzerService } from '../../services/analyzer.service';

@Component({
  selector: 'app-site-header',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule, MatMenuModule, MatButtonModule],
  templateUrl: './site-header.component.html'
})
export class SiteHeaderComponent implements OnInit, OnDestroy {
  currentUser: User | null = null;
  isAuthenticated = false;
  isAdmin = false;
  userCredits = 0;
  pendingServicesCount = 0;

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
      if (user) {
        this.loadPendingServices();
      } else {
        this.pendingServicesCount = 0;
      }
    });

    const user = this.authService.getCurrentUser();
    if (user) {
      this.currentUser = user;
      this.isAuthenticated = true;
      this.isAdmin = this.authService.isAdmin();
      this.userCredits = user.credits ?? 0;
      this.loadPendingServices();
    }
  }

  loadPendingServices(): void {
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
    this.destroy$.next();
    this.destroy$.complete();
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

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
