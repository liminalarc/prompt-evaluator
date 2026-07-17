import { Routes } from '@angular/router';
import { Dashboard } from './dashboard/dashboard';
import { Home } from './home';
import { PromptList } from './prompts/prompt-list';
import { PromptDetail } from './prompts/prompt-detail';
import { DatasetList } from './datasets/dataset-list';
import { DatasetDetail } from './datasets/dataset-detail';
import { EvalRunDetail } from './eval-runs/eval-run-detail';
import { AnalyticsDashboard } from './analytics/analytics-dashboard';
import { ModelAdmin } from './models/model-admin';
import { authGuard } from './auth/auth.guard';
import { adminGuard } from './auth/admin.guard';
import { Login } from './auth/login';
import { Register } from './auth/register';
import { ForgotPassword } from './auth/forgot-password';
import { ResetPassword } from './auth/reset-password';

export const routes: Routes = [
  // Public auth routes (4.1) — no guard.
  { path: 'login', component: Login },
  { path: 'register', component: Register },
  { path: 'forgot-password', component: ForgotPassword },
  { path: 'reset-password', component: ResetPassword },

  // The authenticated app — every route requires a signed-in session (4.1).
  { path: '', component: Dashboard, canActivate: [authGuard] },
  { path: 'prompts', component: PromptList, canActivate: [authGuard] },
  { path: 'prompts/:id', component: PromptDetail, canActivate: [authGuard] },
  { path: 'datasets', component: DatasetList, canActivate: [authGuard] },
  { path: 'datasets/:id', component: DatasetDetail, canActivate: [authGuard] },
  { path: 'eval-runs/:id', component: EvalRunDetail, canActivate: [authGuard] },
  { path: 'analytics', component: AnalyticsDashboard, canActivate: [authGuard] },
  // Workspace-admin: Model Catalog management (1.13), gated to global admins.
  { path: 'admin/models', component: ModelAdmin, canActivate: [authGuard, adminGuard] },
  // The 0.1 walking-skeleton round-trip, kept off the primary path as a wiring smoke test only.
  { path: '_skeleton', component: Home, canActivate: [authGuard] },
];
