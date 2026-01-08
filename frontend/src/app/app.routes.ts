import { Routes } from '@angular/router';
import { AnalyzerComponent } from './components/analyzer/analyzer.component';
import { AdminDashboardComponent } from './components/admin-dashboard/admin-dashboard.component';
import { LoginComponent } from './components/login/login.component';
import { FinanceiroComponent } from './components/financeiro/financeiro.component';
import { ChangePasswordComponent } from './components/change-password/change-password.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', component: AnalyzerComponent, canActivate: [authGuard] },
  { path: 'admin', component: AdminDashboardComponent, canActivate: [authGuard] },
  { path: 'financeiro', component: FinanceiroComponent, canActivate: [authGuard] },
  { path: 'trocar-senha', component: ChangePasswordComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '/login' }
];


