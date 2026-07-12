import { Routes } from '@angular/router';
import { Home } from './home';
import { PromptList } from './prompts/prompt-list';
import { PromptDetail } from './prompts/prompt-detail';
import { DatasetList } from './datasets/dataset-list';
import { DatasetDetail } from './datasets/dataset-detail';
import { EvalRunDetail } from './eval-runs/eval-run-detail';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'prompts', component: PromptList },
  { path: 'prompts/:id', component: PromptDetail },
  { path: 'datasets', component: DatasetList },
  { path: 'datasets/:id', component: DatasetDetail },
  { path: 'eval-runs/:id', component: EvalRunDetail },
];
