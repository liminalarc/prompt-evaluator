import { Routes } from '@angular/router';
import { Dashboard } from './dashboard/dashboard';
import { Home } from './home';
import { PromptList } from './prompts/prompt-list';
import { PromptDetail } from './prompts/prompt-detail';
import { DatasetList } from './datasets/dataset-list';
import { DatasetDetail } from './datasets/dataset-detail';
import { EvalRunDetail } from './eval-runs/eval-run-detail';
import { AnalyticsDashboard } from './analytics/analytics-dashboard';

export const routes: Routes = [
  { path: '', component: Dashboard },
  { path: 'prompts', component: PromptList },
  { path: 'prompts/:id', component: PromptDetail },
  { path: 'datasets', component: DatasetList },
  { path: 'datasets/:id', component: DatasetDetail },
  { path: 'eval-runs/:id', component: EvalRunDetail },
  { path: 'analytics', component: AnalyticsDashboard },
  // The 0.1 walking-skeleton round-trip, kept off the primary path as a wiring smoke test only.
  { path: '_skeleton', component: Home },
];
