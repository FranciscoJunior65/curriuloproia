import { Routes } from '@angular/router';
import { AnalyzerComponent } from './components/analyzer/analyzer.component';
import { AdminDashboardComponent } from './components/admin-dashboard/admin-dashboard.component';
import { LoginComponent } from './components/login/login.component';
import { FinanceiroComponent } from './components/financeiro/financeiro.component';
import { ChangePasswordComponent } from './components/change-password/change-password.component';
import { PrivacyPolicyComponent } from './components/privacy-policy/privacy-policy.component';
import { TermsOfUseComponent } from './components/terms-of-use/terms-of-use.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'politica-de-privacidade', component: PrivacyPolicyComponent },
  { path: 'termos-de-uso', component: TermsOfUseComponent },
  { path: '', component: AnalyzerComponent, canActivate: [authGuard] },
  { path: 'admin', component: AdminDashboardComponent, canActivate: [authGuard] },
  { path: 'financeiro', component: FinanceiroComponent, canActivate: [authGuard] },
  { path: 'trocar-senha', component: ChangePasswordComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '/login' }
];


