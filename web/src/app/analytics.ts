/** Mirrors the .NET ScorerRefResponse DTO. */
export interface ScorerRef {
  identity: string;
  kind: string;
  judgeModel: string | null;
}

/** Mirrors the .NET TrendPointResponse DTO. */
export interface TrendPoint {
  promptVersionId: string;
  versionNumber: number;
  versionLabel: string | null;
  runId: string;
  runAt: string;
  meanValue: number;
  passRate: number | null;
  fixtureCount: number;
}

/** Mirrors the .NET TrendSeriesResponse DTO. */
export interface TrendSeries {
  scorer: ScorerRef;
  points: TrendPoint[];
}

/** Mirrors the .NET CompositeTrendPointResponse DTO — the weighted composite per version (2.9). */
export interface CompositeTrendPoint {
  promptVersionId: string;
  versionNumber: number;
  versionLabel: string | null;
  runId: string;
  runAt: string;
  compositeValue: number;
  scorerCount: number;
}

/**
 * How sure we are a threshold-clearing drop is a real regression. `Confirmed` — the drop is also
 * statistically significant. `Unverified` — the drop cleared the threshold but significance
 * couldn't be established (too few fixtures) or wasn't significant; a possible regression, shown
 * distinctly rather than hidden.
 */
export type RegressionConfidence = 'Confirmed' | 'Unverified';

/** Mirrors the .NET RegressionFlagResponse DTO. */
export interface RegressionFlag {
  scorer: ScorerRef;
  fromVersionId: string;
  fromVersionNumber: number;
  fromVersionLabel: string | null;
  toVersionId: string;
  toVersionNumber: number;
  toVersionLabel: string | null;
  priorMean: number;
  currentMean: number;
  delta: number;
  pValue: number | null;
  pairedFixtureCount: number;
  confidence: RegressionConfidence;
}

/** Mirrors the .NET FixtureDeltaResponse DTO. */
export interface FixtureDelta {
  fixtureId: string;
  fixtureLabel: string | null;
  fromValue: number | null;
  toValue: number | null;
  delta: number | null;
  /** The judge rationale on each side — the "why" behind the score (2.14 rationale-diff). */
  fromRationale: string | null;
  toRationale: string | null;
}

/** Mirrors the .NET ScorerComparisonResponse DTO. */
export interface ScorerComparison {
  scorer: ScorerRef;
  fromMean: number | null;
  toMean: number | null;
  delta: number | null;
  fixtures: FixtureDelta[];
}

/** Mirrors the .NET VersionComparisonResponse DTO. */
export interface VersionComparison {
  fromVersionId: string;
  fromVersionNumber: number;
  fromVersionLabel: string | null;
  fromRunId: string | null;
  toVersionId: string;
  toVersionNumber: number;
  toVersionLabel: string | null;
  toRunId: string | null;
  scorers: ScorerComparison[];
}

/** Mirrors the .NET VarianceStatResponse DTO — mean ± spread over repeated runs (2.14). */
export interface VarianceStat {
  mean: number;
  stdDev: number;
  sampleCount: number;
  min: number;
  max: number;
}

/** Mirrors the .NET FixtureVarianceResponse DTO. */
export interface FixtureVariance {
  fixtureId: string;
  value: VarianceStat;
}

/** Mirrors the .NET VersionVarianceResponse DTO. */
export interface VersionVariance {
  promptVersionId: string;
  versionNumber: number;
  versionLabel: string | null;
  runCount: number;
  aggregate: VarianceStat;
  fixtures: FixtureVariance[];
}

/** Mirrors the .NET ScorerVarianceResponse DTO. */
export interface ScorerVariance {
  scorer: ScorerRef;
  versions: VersionVariance[];
}

/** A human-readable label for a scorer series (kind + judge model when present). */
export function scorerLabel(scorer: ScorerRef): string {
  return scorer.judgeModel ? `${scorer.kind} (${scorer.judgeModel})` : scorer.kind;
}

/**
 * Whether a scorer is stochastic (varies run-to-run) vs deterministic (fixed for a given output).
 * Only the LLM judge is stochastic; Regex/JsonSchema/ExactMatch/etc. are deterministic, so their
 * run-to-run spread is always 0 and their "score" is near-always 1.0. Variance and headline-mean
 * both key off this (2.19 W30/W33) — deterministic scorers are noise for stability and inflate a
 * naive mean, so they're hidden from stability and never the headline score.
 */
export function isStochasticScorer(kind: string): boolean {
  return kind === 'LlmJudge';
}
