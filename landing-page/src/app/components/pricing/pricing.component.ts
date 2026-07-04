import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  PricingPlansService,
  PublicPlan,
  PriceParts
} from '../../services/pricing-plans.service';

@Component({
  selector: 'app-pricing',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pricing.component.html',
  styleUrl: './pricing.component.css'
})
export class PricingComponent implements OnInit {
  analysisPlans: PublicPlan[] = [];
  englishPlan: PublicPlan | null = null;
  loading = true;
  transactionFeeBRL = 0;

  private readonly planOrder = ['single', 'pack3', 'pack5'];

  constructor(private pricingPlansService: PricingPlansService) {}

  ngOnInit(): void {
    this.pricingPlansService.getPlans().subscribe({
      next: (response) => {
        const fromApi =
          response.analysisPlans?.length
            ? response.analysisPlans
            : (response.plans || []).filter((p) => p.id !== 'english');
        this.analysisPlans = this.sortPlans(fromApi);
        this.englishPlan =
          response.englishPlan ||
          (response.plans || []).find((p) => p.id === 'english') ||
          null;
        if (response.transactionFeeBRL != null) {
          this.transactionFeeBRL = Number(response.transactionFeeBRL);
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  priceParts(priceBRL: number): PriceParts {
    return this.pricingPlansService.formatPriceParts(priceBRL);
  }

  planDisplayPrice(plan: PublicPlan): number {
    return this.pricingPlansService.planDisplayPrice(plan, this.transactionFeeBRL);
  }

  englishDisplayPrice(): number {
    if (this.englishPlan) {
      return this.planDisplayPrice(this.englishPlan);
    }
    return 0;
  }

  isPopularPlan(plan: PublicPlan): boolean {
    return plan.id === 'pack3';
  }

  private sortPlans(plans: PublicPlan[]): PublicPlan[] {
    return [...plans].sort(
      (a, b) => this.planOrder.indexOf(a.id) - this.planOrder.indexOf(b.id)
    );
  }
}
