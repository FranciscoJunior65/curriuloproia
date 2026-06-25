import { Component, OnInit } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from './services/auth.service';
import { CpfEnforcementService } from './services/cpf-enforcement.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet></router-outlet>'
})
export class AppComponent implements OnInit {
  title = 'CurriculosPro IA';

  constructor(
    private authService: AuthService,
    private cpfEnforcement: CpfEnforcementService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.promptCpfIfNeeded(event.urlAfterRedirects || event.url);
      });

    if (this.authService.isAuthenticated()) {
      this.promptCpfIfNeeded(this.router.url);
    }
  }

  private promptCpfIfNeeded(url: string): void {
    if (this.isPublicRoute(url)) {
      return;
    }

    if (!this.authService.isAuthenticated() || this.cpfEnforcement.hasValidCpf()) {
      return;
    }

    this.cpfEnforcement.ensureCpf({ mandatory: true, context: 'login' }).subscribe();
  }

  private isPublicRoute(url: string): boolean {
    const path = url.split('?')[0];
    return (
      path.startsWith('/login') ||
      path.startsWith('/parceiro') ||
      path.startsWith('/politica-de-privacidade') ||
      path.startsWith('/termos-de-uso')
    );
  }
}
