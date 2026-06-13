import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from './components/navbar/navbar.component';
import { HeroComponent } from './components/hero/hero.component';
import { FeaturesComponent } from './components/features/features.component';
import { PricingComponent } from './components/pricing/pricing.component';
import { TestimonialsComponent } from './components/testimonials/testimonials.component';
import { FooterComponent } from './components/footer/footer.component';
import { ProcessComponent } from './components/process/process.component';
import { CtaComponent } from './components/cta/cta.component';
import { FaqComponent } from './components/faq/faq.component';
import { TeamComponent } from './components/team/team.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    NavbarComponent,
    HeroComponent,
    ProcessComponent,
    FeaturesComponent,
    PricingComponent,
    TeamComponent,
    TestimonialsComponent,
    FaqComponent,
    CtaComponent,
    FooterComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'CurriculosPro IA';
}
