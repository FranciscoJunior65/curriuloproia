import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css'
})
export class NavbarComponent {
  mobileMenuOpen = false;

  constructor(private router: Router) {}

  toggleMobileMenu(): void {
    this.mobileMenuOpen = !this.mobileMenuOpen;
  }

  scrollTo(sectionId: string): void {
    this.mobileMenuOpen = false;

    if (this.router.url !== '/') {
      this.router.navigateByUrl('/').then(() => {
        setTimeout(() => this.scrollToElement(sectionId), 100);
      });
      return;
    }

    this.scrollToElement(sectionId);
  }

  scrollToTop(): void {
    this.mobileMenuOpen = false;
    if (this.router.url !== '/') {
      this.router.navigateByUrl('/');
      return;
    }
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  private scrollToElement(sectionId: string): void {
    const element = document.getElementById(sectionId);
    if (element) {
      const offset = 80;
      const elementPosition = element.getBoundingClientRect().top + window.pageYOffset;
      window.scrollTo({
        top: elementPosition - offset,
        behavior: 'smooth'
      });
    }
  }
}
