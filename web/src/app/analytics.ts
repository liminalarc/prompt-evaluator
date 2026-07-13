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
}

/** Mirrors the .NET FixtureDeltaResponse DTO. */
export interface FixtureDelta {
  fixtureId: string;
  fromValue: number | null;
  toValue: number | null;
  delta: number | null;
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

/** A human-readable label for a scorer series (kind + judge model when present). */
export function scorerLabel(scorer: ScorerRef): string {
  return scorer.judgeModel ? `${scorer.kind} (${scorer.judgeModel})` : scorer.kind;
}
