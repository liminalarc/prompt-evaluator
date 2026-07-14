/** View models for the landing dashboard (2.4) — assembled from existing read APIs, org-scoped. */

/** A prompt with its most recent score across its datasets (null when never run). */
export interface DashboardPromptCard {
  id: string;
  name: string;
  versionCount: number;
  latestTargetModel: string | null;
  latestScore: { meanValue: number; passRate: number | null; runAt: string } | null;
}

/** A recent eval run, joined to its prompt + dataset for display. */
export interface DashboardRunRow {
  runId: string;
  promptId: string;
  promptName: string;
  datasetId: string;
  datasetName: string;
  createdAt: string;
  fixtureCount: number;
  scoreCount: number;
}

/** An open regression flag, joined to its prompt + dataset. */
export interface DashboardRegressionRow {
  promptId: string;
  promptName: string;
  datasetId: string;
  datasetName: string;
  scorer: string;
  fromVersionNumber: number;
  toVersionNumber: number;
  delta: number;
}

/** Everything the dashboard renders for the active org. */
export interface DashboardView {
  prompts: DashboardPromptCard[];
  recentRuns: DashboardRunRow[];
  openRegressions: DashboardRegressionRow[];
}
