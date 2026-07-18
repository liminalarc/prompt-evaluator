/** Mirrors the .NET ScoreResponse DTO. */
export interface ScoreView {
  scorerKind: string;
  scorerIdentity: string;
  judgeModel: string | null;
  value: number;
  passed: boolean | null;
  detail: string | null;
}

/** Mirrors the .NET FixtureRunResponse DTO. */
export interface FixtureRunView {
  fixtureId: string;
  modelOutput: string;
  latencyMs: number;
  inputTokens: number;
  outputTokens: number;
  costUsd: number | null;
  scores: ScoreView[];
}

/** Mirrors the .NET EvalRunResponse DTO. */
export interface EvalRun {
  id: string;
  promptId: string;
  promptVersionId: string;
  datasetId: string;
  createdAt: string;
  results: FixtureRunView[];
}

/** Mirrors the .NET EvalRunSummaryResponse DTO (list projection). */
export interface EvalRunSummary {
  id: string;
  promptId: string;
  promptVersionId: string;
  createdAt: string;
  fixtureCount: number;
  scoreCount: number;
  scorerKinds: string[];
}

/** Mirrors the .NET ScorerConfigResponse DTO. */
export interface ScorerConfig {
  id: string;
  kind: string;
  config: string;
  judgeModel: string | null;
  identity: string;
  createdAt: string;
}

/** The scorer kinds the API accepts (mirrors Domain.ScorerKind). */
export const SCORER_KINDS = [
  'Regex',
  'JsonSchema',
  'ExactMatch',
  'FuzzyMatch',
  'Latency',
  'Cost',
  'LlmJudge',
] as const;
export type ScorerKind = (typeof SCORER_KINDS)[number];
