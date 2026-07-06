import { Routes } from '@angular/router';
import { AnalyzerComponent } from './components/analyzer/analyzer.component';
import { AdminDashboardComponent } from './components/admin-dashboard/admin-dashboard.component';
import { LoginComponent } from './components/login/login.component';
import { FinanceiroComponent } from './components/financeiro/financeiro.component';
import { ChangePasswordComponent } from './components/change-password/change-password.component';
import { MeusDadosComponent } from './components/meus-dados/meus-dados.component';
import { PrivacyPolicyComponent } from './components/privacy-policy/privacy-policy.component';
import { TermsOfUseComponent } from './components/terms-of-use/terms-of-use.component';
import { AnalysesHistoryComponent } from './components/analyses-history/analyses-history.component';
import { PurchaseConfirmationComponent } from './components/purchase-confirmation/purchase-confirmation.component';
import { CaktoPopupReturnComponent } from './components/cakto-popup-return/cakto-popup-return.component';
import { KiwifyPopupReturnComponent } from './components/kiwify-popup-return/kiwify-popup-return.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'parceiro/:codigo', component: LoginComponent },
  { path: 'politica-de-privacidade', component: PrivacyPolicyComponent },
  { path: 'termos-de-uso', component: TermsOfUseComponent },
  { path: '', component: AnalyzerComponent, canActivate: [authGuard] },
  { path: 'admin', component: AdminDashboardComponent, canActivate: [authGuard] },
  { path: 'financeiro', component: FinanceiroComponent, canActivate: [authGuard] },
  { path: 'historico-analises', component: AnalysesHistoryComponent, canActivate: [authGuard] },
  { path: 'trocar-senha', component: ChangePasswordComponent, canActivate: [authGuard] },
  { path: 'meus-dados', component: MeusDadosComponent, canActivate: [authGuard] },
  { path: 'compra/sucesso', component: PurchaseConfirmationComponent, canActivate: [authGuard] },
  { path: 'compra/cakto-popup-retorno', component: CaktoPopupReturnComponent },
  { path: 'compra/kiwify-popup-retorno', component: KiwifyPopupReturnComponent },
  { path: 'compra/pendente', component: PurchaseConfirmationComponent, canActivate: [authGuard] },
  { path: 'compra/falha', component: PurchaseConfirmationComponent, canActivate: [authGuard] },
  { path: 'compra/cancelada', component: PurchaseConfirmationComponent, canActivate: [authGuard] },
  { path: 'payment/cancel', redirectTo: 'compra/cancelada', pathMatch: 'full' },
  { path: '**', redirectTo: '/login' }
];


