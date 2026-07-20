import { BadgeVariant } from '../shared';

/**
 * DTO mirrors + view helpers for the Admin → AI Usage area (spec 6.1.T5/T6). These mirror the .NET
 * responses in `src/Api/AiUsage/AiUsageEndpoints.cs` (enums arrive as their PascalCase names — the
 * Api stringifies feature/status/scope/level at the edge; there is no global string-enum converter).
 */

/** Which kind of model call a ledger record is for (6.1). Wire values match the .NET enum names. */
export type AiUsageFeature = 'SubjectExecution' | 'LlmJudge' | 'SyntheticGeneration';

/** The outcome of a model call (6.1). */
export type AiUsageStatus = 'Success' | 'Refusal' | 'Error';

/** Time-series bucket period. */
export type AiUsagePeriod = 'Day' | 'Week' | 'Month';

/** A breakdown grouping dimension. */
export type AiUsageDimension = 'Model' | 'Feature' | 'User' | 'Organization';

/** Per-slice metrics (6.1.T3) — mirrors `AiUsageMetrics`. */
export interface AiUsageMetrics {
  totalCostUsd: number;
  inputTokens: number;
  outputTokens: number;
  cacheCreationTokens: number;
  cacheReadTokens: number;
  callCount: number;
  avgCostPerCall: number;
  avgTokensPerCall: number;
  successRate: number;
  latencyP50Ms: number;
  latencyP95Ms: number;
}

/** One breakdown row — a dimension key + its metrics. */
export interface AiUsageBreakdownRow {
  key: string;
  metrics: AiUsageMetrics;
}

/** One point on the spend-over-time series. */
export interface AiUsageTimePoint {
  periodStart: string;
  metrics: AiUsageMetrics;
}

/** One individual call in the drill-down table (metadata only — no prompt/response content). */
export interface AiUsageCall {
  id: string;
  occurredAt: string;
  feature: AiUsageFeature;
  model: string;
  inputTokens: number;
  outputTokens: number;
  cacheCreationTokens: number;
  cacheReadTokens: number;
  costUsd: number | null;
  organizationId: string | null;
  userId: string | null;
  status: AiUsageStatus;
  latencyMs: number;
  requestId: string | null;
}

/** A page of the calls table. */
export interface AiUsageCallsPage {
  items: AiUsageCall[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** A budget's scope (6.1.T6). */
export type BudgetScope = 'Global' | 'Model' | 'Feature' | 'Organization';

/** Where current-period spend sits against a budget (6.1.T6). */
export type BudgetStatusLevel = 'Ok' | 'Warning' | 'Over';

/** A configured budget (6.1.T6) — mirrors the Api `BudgetResponse`. */
export interface AiUsageBudget {
  id: string;
  scope: BudgetScope;
  scopeValue: string | null;
  limitUsd: number;
  period: string;
  alertThresholdPercent: number;
  createdAt: string;
}

/** A budget plus its current-period spend + threshold classification (6.1.T6). */
export interface BudgetStatus {
  budget: AiUsageBudget;
  spendUsd: number;
  percentUsed: number;
  level: BudgetStatusLevel;
}

/** Create-budget payload (6.1.T6). */
export interface CreateBudgetBody {
  scope: BudgetScope;
  scopeValue: string | null;
  limitUsd: number;
  alertThresholdPercent: number | null;
}

/**
 * The current filter — drives every query (summary, breakdowns, time-series, calls, export) so the
 * whole surface moves together. `from`/`to` are ISO date strings (`yyyy-mm-dd`); the rest are the
 * selected wire values (empty array = no filter on that dimension).
 */
export interface AiUsageFilterState {
  from: string | null;
  to: string | null;
  models: string[];
  features: AiUsageFeature[];
  statuses: AiUsageStatus[];
  users: string[];
  orgs: string[];
}

/** An empty filter — no constraint on any dimension. */
export function emptyFilter(): AiUsageFilterState {
  return { from: null, to: null, models: [], features: [], statuses: [], users: [], orgs: [] };
}

/** The three features, with human labels for the filter bar + breakdown display. */
export const FEATURES: readonly { value: AiUsageFeature; label: string }[] = [
  { value: 'SubjectExecution', label: 'Subject execution' },
  { value: 'LlmJudge', label: 'LLM judge' },
  { value: 'SyntheticGeneration', label: 'Synthetic generation' },
];

/** The three statuses, with the brand badge variant each maps to. */
export const STATUSES: readonly { value: AiUsageStatus; label: string; variant: BadgeVariant }[] = [
  { value: 'Success', label: 'Success', variant: 'success' },
  { value: 'Refusal', label: 'Refusal', variant: 'warn' },
  { value: 'Error', label: 'Error', variant: 'error' },
];

const FEATURE_LABEL = new Map<string, string>(FEATURES.map((f) => [f.value, f.label]));
const STATUS_VARIANT = new Map<string, BadgeVariant>(STATUSES.map((s) => [s.value, s.variant]));

/** Human label for a feature wire value (falls back to the raw value). */
export function featureLabel(feature: string): string {
  return FEATURE_LABEL.get(feature) ?? feature;
}

/** Brand badge variant for a status wire value. */
export function statusVariant(status: string): BadgeVariant {
  return STATUS_VARIANT.get(status) ?? 'neutral';
}

/** Brand badge variant for a budget status level. */
export function budgetLevelVariant(level: BudgetStatusLevel): BadgeVariant {
  switch (level) {
    case 'Over':
      return 'error';
    case 'Warning':
      return 'warn';
    default:
      return 'success';
  }
}
