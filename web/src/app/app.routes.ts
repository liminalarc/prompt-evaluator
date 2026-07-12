import { Routes } from '@angular/router';
import { Home } from './home';
import { PromptList } from './prompts/prompt-list';
import { PromptDetail } from './prompts/prompt-detail';

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'prompts', component: PromptList },
  { path: 'prompts/:id', component: PromptDetail },
];
