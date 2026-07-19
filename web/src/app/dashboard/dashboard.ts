import { Component, computed, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { OrgContextStore } from '../shared/org-context.store';
import {
  BadgeVariant,
  Chip,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
  StatusBadge,
} from '../shared';
import { DashboardFacade } from './dashboard.facade';
import { DashboardView } from './dashboard.model';

/**
 * The landing dashboard (2.4) — replaces the walking-skeleton echo toy. For the active org it
 * narrates the eval loop: prompts with their latest score, recent runs, and open regressions,
 * all assembled by {@link DashboardFacade} from existing read APIs.
 */
@Component({
  selector: 'app-dashboard',
  imports: [
    RouterLink,
    DatePipe,
    PageHeader,
    LoadingState,
    EmptyState,
    ErrorState,
    StatusBadge,
    Chip,
  ],
  template: `
    <section class="panel">
      <app-page-header heading="Dashboard" [subtitle]="subtitle()">
        <a actions class="sb-btn sb-btn--primary" routerLink="/prompts">Browse prompts</a>
      </app-page-header>

      @if (loading()) {
        <app-loading-state label="Loading your workspace…" />
      } @else if (error(); as message) {
        <app-error-state [message]="message" [retryable]="true" (retry)="reload()" />
      } @else if (view(); as v) {
        <!-- W34: lead with what needs attention (open regressions), then prompt status, then
             activity — so the operator sees problems first, not a wall of cards. -->
        <h2 class="section-title">Needs attention</h2>
        @if (v.openRegressions.length === 0) {
          <app-empty-state
            message="All good — no open regressions. Scores are holding."
            data-testid="no-regressions"
          />
        } @else {
          <table class="sb-table" data-testid="dash-regressions">
            <thead>
              <tr>
                <th>Prompt</th>
                <th>Dataset</th>
                <th>Scorer</th>
                <th>Change</th>
                <th>Δ</th>
              </tr>
            </thead>
            <tbody>
              @for (
                f of v.openRegressions;
                track f.promptId + f.datasetId + f.scorer + f.toVersionNumber
              ) {
                <tr data-testid="dash-regression-row">
                  <td>
                    <a [routerLink]="['/prompts', f.promptId]">{{ f.promptName }}</a>
                  </td>
                  <td>{{ f.datasetName }}</td>
                  <td>{{ f.scorer }}</td>
                  <td>v{{ f.fromVersionNumber }} → v{{ f.toVersionNumber }}</td>
                  <td>
                    <app-status-badge
                      [variant]="f.delta <= -0.1 ? 'error' : 'warn'"
                      [label]="fmtDelta(f.delta)"
                    />
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }

        <h2 class="section-title">Prompts</h2>
        @if (v.prompts.length === 0) {
          <app-empty-state message="No prompts in this organization yet." data-testid="no-prompts">
            <a class="sb-btn sb-btn--primary sb-btn--sm" routerLink="/prompts">Create one</a>
          </app-empty-state>
        } @else {
          <div class="dash-grid" data-testid="dash-prompts">
            @for (p of v.prompts; track p.id) {
              <a
                class="sb-card dash-prompt"
                [routerLink]="['/prompts', p.id]"
                data-testid="dash-prompt-card"
              >
                <div class="dash-prompt__head">
                  <span class="dash-prompt__name">{{ p.name }}</span>
                  @if (p.latestScore; as s) {
                    <app-status-badge
                      [variant]="scoreVariant(s.passRate)"
                      [label]="fmt(s.meanValue)"
                    />
                  } @else {
                    <app-status-badge variant="neutral" label="No runs" />
                  }
                </div>
                <div class="dash-prompt__meta">
                  <app-chip [label]="versionLabel(p.versionCount)" />
                  @if (p.latestTargetModel) {
                    <app-chip [label]="p.latestTargetModel" />
                  }
                </div>
              </a>
            }
          </div>
        }

        <h2 class="section-title">Recent activity</h2>
        @if (v.recentRuns.length === 0) {
          <app-empty-state
            message="No eval runs yet — run a prompt over a dataset to see activity."
            data-testid="no-runs"
          />
        } @else {
          <table class="sb-table" data-testid="dash-runs">
            <thead>
              <tr>
                <th>Prompt</th>
                <th>Dataset</th>
                <th>When</th>
                <th>Score</th>
                <th>Test cases</th>
              </tr>
            </thead>
            <tbody>
              @for (r of v.recentRuns; track r.runId) {
                <tr>
                  <td>
                    <a [routerLink]="['/eval-runs', r.runId]">{{ r.promptName }}</a>
                  </td>
                  <td>{{ r.datasetName }}</td>
                  <td>{{ r.createdAt | date: 'medium' }}</td>
                  <td>{{ r.meanScore != null ? fmt(r.meanScore) : '—' }}</td>
                  <td>{{ r.fixtureCount }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      }
    </section>
  `,
  styleUrls: ['../prompts/prompts.css', './dashboard.css'],
})
export class Dashboard {
  private readonly facade = inject(DashboardFacade);
  private readonly orgStore = inject(OrgContextStore);

  protected readonly view = signal<DashboardView | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly subtitle = computed(() => {
    const name = this.orgStore.currentOrg()?.name;
    return name ? `How ${name}'s prompts are doing.` : 'How your prompts are doing.';
  });

  constructor() {
    // Reload the dashboard whenever the active org changes.
    effect(() => {
      const orgId = this.orgStore.currentOrgId();
      if (orgId) {
        this.run(orgId);
      } else {
        this.view.set(null);
      }
    });
  }

  protected reload(): void {
    const orgId = this.orgStore.currentOrgId();
    if (orgId) this.run(orgId);
  }

  protected scoreVariant(passRate: number | null): BadgeVariant {
    if (passRate === null) return 'neutral';
    if (passRate >= 0.8) return 'success';
    if (passRate >= 0.5) return 'warn';
    return 'error';
  }

  protected fmt(value: number): string {
    return value.toFixed(2);
  }

  protected fmtDelta(value: number): string {
    return value.toFixed(3);
  }

  protected versionLabel(count: number): string {
    return `${count} ${count === 1 ? 'version' : 'versions'}`;
  }

  private run(orgId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.facade.load(orgId).subscribe({
      next: (v) => {
        this.view.set(v);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load the dashboard — is the stack running?');
        this.loading.set(false);
      },
    });
  }
}
