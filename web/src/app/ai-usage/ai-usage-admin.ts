import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Color, LegendPosition, LineChartModule, ScaleType } from '@swimlane/ngx-charts';
import {
  Breadcrumb,
  Card,
  Crumb,
  EmptyState,
  ErrorState,
  LoadingState,
  PageHeader,
  StatusBadge,
} from '../shared';
import { ModelsApiService } from '../models/models-api.service';
import { ModelCatalogEntry } from '../model';
import { UsersApiService } from '../users/users-api.service';
import { UserDetail } from '../users/user';
import { OrgsAdminApiService } from '../organizations/orgs-admin-api.service';
import { OrganizationAdmin } from '../organizations/org-admin.model';
import { AiUsageApiService } from './ai-usage-api.service';
import {
  AiUsageBreakdownRow,
  AiUsageBudget,
  AiUsageCallsPage,
  AiUsageDimension,
  AiUsageFeature,
  AiUsageFilterState,
  AiUsageMetrics,
  AiUsagePeriod,
  AiUsageStatus,
  AiUsageTimePoint,
  BudgetScope,
  BudgetStatus,
  CreateBudgetBody,
  FEATURES,
  STATUSES,
  budgetLevelVariant,
  emptyFilter,
  featureLabel,
  statusVariant,
} from './ai-usage';

// Spend-over-time uses the primary brand hue at runtime so it tracks light/dark (dataviz skill).
const SERIES_TOKEN = '--sb-primary';
const BREAKDOWN_TOP_N = 10;

interface ChartSeries {
  name: string;
  series: { name: string; value: number }[];
}

/**
 * Admin → AI Usage (spec 6.1.T5/T6). Mirrors the Model Catalog admin structure: a filter bar that
 * drives the whole surface, a dashboard (spend summary tiles + spend-over-time + breakdowns), a
 * paginated calls table, CSV export, and the budget surface (spend-vs-budget + threshold alerts).
 * Reached at /admin/ai-usage, gated to global admins (route + nav). Never renders prompt/response
 * content — the ledger is metadata + token counts only.
 */
@Component({
  selector: 'app-ai-usage-admin',
  imports: [
    FormsModule,
    DatePipe,
    NgTemplateOutlet,
    LineChartModule,
    Breadcrumb,
    Card,
    EmptyState,
    ErrorState,
    LoadingState,
    PageHeader,
    StatusBadge,
  ],
  templateUrl: './ai-usage-admin.html',
  styleUrls: ['../prompts/prompts.css', './ai-usage-admin.css'],
})
export class AiUsageAdmin implements OnInit {
  private readonly api = inject(AiUsageApiService);
  private readonly modelsApi = inject(ModelsApiService);
  private readonly usersApi = inject(UsersApiService);
  private readonly orgsApi = inject(OrgsAdminApiService);

  protected readonly features = FEATURES;
  protected readonly statuses = STATUSES;
  protected readonly scopes: BudgetScope[] = ['Global', 'Model', 'Feature', 'Organization'];
  protected readonly periods: AiUsagePeriod[] = ['Day', 'Week', 'Month'];

  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  // Lookups for the filter dropdowns + friendly names in breakdowns / calls.
  protected readonly models = signal<ModelCatalogEntry[]>([]);
  protected readonly users = signal<UserDetail[]>([]);
  protected readonly orgs = signal<OrganizationAdmin[]>([]);

  private readonly userNames = computed(
    () => new Map(this.users().map((u) => [u.id, u.displayName || u.email])),
  );
  private readonly orgNames = computed(() => new Map(this.orgs().map((o) => [o.id, o.name])));
  private readonly modelNames = computed(
    () => new Map(this.models().map((m) => [m.modelId, m.displayName])),
  );

  // Draft filter (bound to the controls); applied on "Apply filters".
  protected readonly draftFrom = signal<string>('');
  protected readonly draftTo = signal<string>('');
  protected readonly draftModels = signal<string[]>([]);
  protected readonly draftFeatures = signal<AiUsageFeature[]>([]);
  protected readonly draftStatuses = signal<AiUsageStatus[]>([]);
  protected readonly draftUsers = signal<string[]>([]);
  protected readonly draftOrgs = signal<string[]>([]);

  private readonly applied = signal<AiUsageFilterState>(emptyFilter());

  // Dashboard state.
  protected readonly summary = signal<AiUsageMetrics | null>(null);
  protected readonly period = signal<AiUsagePeriod>('Day');
  protected readonly timeSeries = signal<AiUsageTimePoint[]>([]);
  protected readonly modelBreakdown = signal<AiUsageBreakdownRow[]>([]);
  protected readonly featureBreakdown = signal<AiUsageBreakdownRow[]>([]);
  protected readonly userBreakdown = signal<AiUsageBreakdownRow[]>([]);
  protected readonly orgBreakdown = signal<AiUsageBreakdownRow[]>([]);

  // Calls table state.
  protected readonly callsPage = signal<AiUsageCallsPage | null>(null);
  protected readonly page = signal(1);
  protected readonly pageSize = 25;
  protected readonly totalPages = computed(() => {
    const p = this.callsPage();
    return p ? Math.max(1, Math.ceil(p.totalCount / p.pageSize)) : 1;
  });

  // Budget state (T6).
  protected readonly budgetStatuses = signal<BudgetStatus[]>([]);
  protected readonly showBudgetForm = signal(false);
  protected readonly bScope = signal<BudgetScope>('Global');
  protected readonly bScopeValue = signal<string>('');
  protected readonly bLimit = signal<string>('');
  protected readonly bThreshold = signal<string>('80');
  protected readonly budgetError = signal<string | null>(null);

  protected readonly crumbs = computed<Crumb[]>(() => [
    { label: 'Dashboard', link: '/' },
    { label: 'AI usage' },
  ]);

  protected readonly chartData = computed<ChartSeries[]>(() => {
    const points = this.timeSeries();
    if (points.length === 0) return [];
    return [
      {
        name: 'Spend',
        series: points.map((p) => ({
          name: this.formatPeriod(p.periodStart),
          value: p.metrics.totalCostUsd,
        })),
      },
    ];
  });

  protected readonly scheme = computed<Color>(() => ({
    name: 'brand',
    selectable: true,
    group: ScaleType.Ordinal,
    domain: this.readTokenColors(),
  }));

  protected readonly legendBelow = LegendPosition.Below;

  ngOnInit(): void {
    this.loadLookups();
    this.reload();
  }

  private loadLookups(): void {
    this.modelsApi.listAllModels().subscribe({ next: (m) => this.models.set(m), error: () => {} });
    this.usersApi.listUsers().subscribe({ next: (u) => this.users.set(u), error: () => {} });
    this.orgsApi.listOrganizations().subscribe({ next: (o) => this.orgs.set(o), error: () => {} });
  }

  /** Copies the draft controls into the applied filter and refreshes the whole surface. */
  protected applyFilters(): void {
    this.applied.set({
      from: this.draftFrom() || null,
      to: this.draftTo() || null,
      models: this.draftModels(),
      features: this.draftFeatures(),
      statuses: this.draftStatuses(),
      users: this.draftUsers(),
      orgs: this.draftOrgs(),
    });
    this.page.set(1);
    this.reload();
  }

  protected resetFilters(): void {
    this.draftFrom.set('');
    this.draftTo.set('');
    this.draftModels.set([]);
    this.draftFeatures.set([]);
    this.draftStatuses.set([]);
    this.draftUsers.set([]);
    this.draftOrgs.set([]);
    this.applied.set(emptyFilter());
    this.page.set(1);
    this.reload();
  }

  protected toggleFeature(f: AiUsageFeature, on: boolean): void {
    this.draftFeatures.update((cur) => (on ? [...cur, f] : cur.filter((x) => x !== f)));
  }

  protected toggleStatus(s: AiUsageStatus, on: boolean): void {
    this.draftStatuses.update((cur) => (on ? [...cur, s] : cur.filter((x) => x !== s)));
  }

  protected setPeriod(p: AiUsagePeriod): void {
    this.period.set(p);
    this.loadTimeSeries();
  }

  private reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const filter = this.applied();

    this.api.summary(filter).subscribe({
      next: (m) => {
        this.summary.set(m);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load AI-usage data.');
        this.loading.set(false);
      },
    });

    this.loadTimeSeries();
    this.loadBreakdown('Model', this.modelBreakdown, BREAKDOWN_TOP_N);
    this.loadBreakdown('Feature', this.featureBreakdown);
    this.loadBreakdown('User', this.userBreakdown, BREAKDOWN_TOP_N);
    this.loadBreakdown('Organization', this.orgBreakdown, BREAKDOWN_TOP_N);
    this.loadCalls();
    this.loadBudgets();
  }

  private loadTimeSeries(): void {
    this.api.timeSeries(this.applied(), this.period()).subscribe({
      next: (t) => this.timeSeries.set(t),
      error: () => {},
    });
  }

  private loadBreakdown(
    dimension: AiUsageDimension,
    target: { set: (v: AiUsageBreakdownRow[]) => void },
    topN?: number,
  ): void {
    this.api.breakdown(this.applied(), dimension, topN).subscribe({
      next: (rows) => target.set(rows),
      error: () => {},
    });
  }

  private loadCalls(): void {
    this.api.calls(this.applied(), this.page(), this.pageSize).subscribe({
      next: (p) => this.callsPage.set(p),
      error: () => {},
    });
  }

  protected prevPage(): void {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
      this.loadCalls();
    }
  }

  protected nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update((p) => p + 1);
      this.loadCalls();
    }
  }

  protected exportCsv(): void {
    this.error.set(null);
    this.api.exportCsv(this.applied()).subscribe({
      next: (csv) => this.triggerDownload(csv),
      error: () => this.error.set('Could not export the CSV.'),
    });
  }

  private triggerDownload(csv: string): void {
    if (typeof document === 'undefined') return;
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'ai-usage.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  // ── Budgets (T6) ──────────────────────────────────────────────────────────────────────────────
  private loadBudgets(): void {
    this.api.budgetStatus().subscribe({
      next: (s) => this.budgetStatuses.set(s),
      error: () => {},
    });
  }

  protected startCreateBudget(): void {
    this.bScope.set('Global');
    this.bScopeValue.set('');
    this.bLimit.set('');
    this.bThreshold.set('80');
    this.budgetError.set(null);
    this.showBudgetForm.set(true);
  }

  protected cancelBudget(): void {
    this.showBudgetForm.set(false);
    this.budgetError.set(null);
  }

  protected saveBudget(event: Event): void {
    event.preventDefault();
    const limit = Number(this.bLimit());
    if (!Number.isFinite(limit) || limit <= 0) {
      this.budgetError.set('Enter a limit greater than zero.');
      return;
    }
    const scope = this.bScope();
    const scopeValue = scope === 'Global' ? null : this.bScopeValue().trim() || null;
    if (scope !== 'Global' && !scopeValue) {
      this.budgetError.set('A scoped budget needs a scope value.');
      return;
    }
    const threshold = this.bThreshold().trim() === '' ? null : Number(this.bThreshold());
    const body: CreateBudgetBody = {
      scope,
      scopeValue,
      limitUsd: limit,
      alertThresholdPercent: threshold,
    };
    this.budgetError.set(null);
    this.api.createBudget(body).subscribe({
      next: () => {
        this.showBudgetForm.set(false);
        this.loadBudgets();
      },
      error: () => this.budgetError.set('Could not create the budget — check the fields.'),
    });
  }

  protected deleteBudget(b: AiUsageBudget): void {
    this.api.deleteBudget(b.id).subscribe({
      next: () => this.loadBudgets(),
      error: () => this.budgetError.set('Could not delete the budget.'),
    });
  }

  // ── Display helpers ───────────────────────────────────────────────────────────────────────────
  protected money(n: number | null | undefined): string {
    if (n == null) return '—';
    return `$${n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`;
  }

  protected num(n: number | null | undefined): string {
    return n == null ? '—' : n.toLocaleString('en-US');
  }

  protected pct(fraction: number | null | undefined): string {
    return fraction == null ? '—' : `${(fraction * 100).toFixed(1)}%`;
  }

  protected featureLabel = featureLabel;
  protected statusVariant = statusVariant;
  protected budgetLevelVariant = budgetLevelVariant;

  /** Friendly label for a breakdown key, by dimension. */
  protected keyLabel(dimension: AiUsageDimension, key: string): string {
    if (key === 'unattributed') return 'Unattributed';
    switch (dimension) {
      case 'Feature':
        return featureLabel(key);
      case 'Model':
        return this.modelNames().get(key) ?? key;
      case 'User':
        return this.userNames().get(key) ?? key;
      case 'Organization':
        return this.orgNames().get(key) ?? key;
      default:
        return key;
    }
  }

  protected userLabel(id: string | null): string {
    if (!id) return '—';
    return this.userNames().get(id) ?? id;
  }

  protected orgLabel(id: string | null): string {
    if (!id) return '—';
    return this.orgNames().get(id) ?? id;
  }

  protected budgetScopeLabel(b: AiUsageBudget): string {
    if (b.scope === 'Global') return 'Workspace (global)';
    const value = b.scopeValue ?? '';
    switch (b.scope) {
      case 'Feature':
        return `Feature · ${featureLabel(value)}`;
      case 'Model':
        return `Model · ${this.modelNames().get(value) ?? value}`;
      case 'Organization':
        return `Org · ${this.orgNames().get(value) ?? value}`;
      default:
        return value;
    }
  }

  protected formatPeriod(iso: string): string {
    const d = new Date(iso);
    if (isNaN(d.getTime())) return iso;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  private readTokenColors(): string[] {
    if (typeof document === 'undefined' || typeof getComputedStyle !== 'function') {
      return [SERIES_TOKEN];
    }
    const styles = getComputedStyle(document.documentElement);
    const value = styles.getPropertyValue(SERIES_TOKEN).trim();
    return value ? [value] : [SERIES_TOKEN];
  }
}
