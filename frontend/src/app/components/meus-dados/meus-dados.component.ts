import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatMenuModule } from '@angular/material/menu';
import { AuthService } from '../../services/auth.service';
import { formatCpfDisplay, getCpfDigits } from '../../utils/cpf.utils';

@Component({
  selector: 'app-meus-dados',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    MatMenuModule
  ],
  templateUrl: './meus-dados.component.html',
  styleUrl: './meus-dados.component.scss'
})
export class MeusDadosComponent implements OnInit {
  name: string = '';
  email: string = '';
  cpf: string = '';
  dateOfBirth: string = '';
  city: string = '';
  country: string = '';
  loading: boolean = false;
  error: string | null = null;
  success: boolean = false;
  currentUser: { name?: string; email?: string; credits?: number } | null = null;
  userCredits = 0;

  constructor(
    public authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    if (!this.authService.isAuthenticated()) {
      this.router.navigate(['/login']);
      return;
    }
    this.authService.currentUser$.subscribe(user => {
      this.currentUser = user;
      this.userCredits = user?.credits ?? 0;
      if (user) {
        this.name = user.name || '';
        this.email = user.email || '';
        this.cpf = formatCpfDisplay(user.cpf ?? '');
        this.dateOfBirth = user.date_of_birth ?? '';
        this.city = user.city ?? '';
        this.country = user.country ?? '';
      }
    });
  }

  getUserDisplayName(): string {
    return this.currentUser?.name || this.currentUser?.email || 'Usuário';
  }

  navigateTo(path: string): void {
    this.router.navigate([path]);
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  onCpfInput(): void {
    const digits = getCpfDigits(this.cpf);
    this.cpf = formatCpfDisplay(digits);
  }

  onCpfKeydown(event: KeyboardEvent): void {
    const key = event.key;
    if (key === 'Backspace' || key === 'Delete' || key === 'Tab' || key === 'ArrowLeft' || key === 'ArrowRight') return;
    if (key.length === 1 && !/\d/.test(key)) {
      event.preventDefault();
    }
  }

  onSubmit(): void {
    this.error = null;
    this.success = false;

    const emailTrim = this.email.trim();
    if (!emailTrim) {
      this.error = 'Email é obrigatório';
      return;
    }
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(emailTrim)) {
      this.error = 'Digite um email válido';
      return;
    }

    const cpfDigits = getCpfDigits(this.cpf);
    if (cpfDigits.length !== 11) {
      this.error = 'CPF é obrigatório e deve conter 11 dígitos';
      return;
    }

    this.loading = true;
    this.authService.updateProfile({
      name: this.name.trim(),
      email: emailTrim,
      cpf: cpfDigits,
      date_of_birth: this.dateOfBirth.trim() || null,
      city: this.city.trim() || null,
      country: this.country.trim() || null
    }).subscribe({
      next: (response) => {
        this.loading = false;
        if (response.success) {
          this.success = true;
          setTimeout(() => {
            this.router.navigate(['/']);
          }, 2000);
        } else {
          this.error = response.error || 'Erro ao atualizar dados';
        }
      },
      error: (err) => {
        this.loading = false;
        this.error = err.error?.error || err.error?.message || 'Erro ao atualizar dados. Tente novamente.';
        if (err.status === 409) {
          this.error = err.error?.error || 'Este email já está em uso por outra conta';
        }
      }
    });
  }
}
