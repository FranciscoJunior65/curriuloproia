import { Routes } from '@angular/router';
import { AnalyzerComponent } from './components/analyzer/analyzer.component';
import { AdminDashboardComponent } from './components/admin-dashboard/admin-dashboard.component';

export const routes: Routes = [
  { path: '', component: AnalyzerComponent },
  { path: 'admin', component: AdminDashboardComponent },
  { path: '**', redirectTo: '' }
];


