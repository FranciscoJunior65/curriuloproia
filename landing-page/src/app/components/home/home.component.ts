import { Component } from '@angular/core';
import { NavbarComponent } from '../navbar/navbar.component';
import { HeroComponent } from '../hero/hero.component';
import { FeaturesComponent } from '../features/features.component';
import { PricingComponent } from '../pricing/pricing.component';
import { TestimonialsComponent } from '../testimonials/testimonials.component';
import { FooterComponent } from '../footer/footer.component';
import { ProcessComponent } from '../process/process.component';
import { CtaComponent } from '../cta/cta.component';
import { FaqComponent } from '../faq/faq.component';
import { TeamComponent } from '../team/team.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [
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
  templateUrl: './home.component.html'
})
export class HomeComponent {}
