/** Mirrors the .NET FixtureResponse DTO. */
export interface Fixture {
  id: string;
  origin: 'Captured' | 'Synthetic';
  input: string;
  upstreamContext: string | null;
  expectedOutput: string | null;
  seedFixtureId: string | null;
  createdAt: string;
}

/** Mirrors the .NET DatasetResponse DTO. */
export interface Dataset {
  id: string;
  promptId: string;
  name: string;
  description: string | null;
  fixtures: Fixture[];
}

/** Mirrors the .NET DatasetSummaryResponse DTO (list/browse projection). */
export interface DatasetSummary {
  id: string;
  promptId: string;
  name: string;
  description: string | null;
  fixtureCount: number;
  capturedCount: number;
  syntheticCount: number;
}

/** Mirrors the .NET CaptureTupleRequest DTO (capture-ingestion schema). */
export interface CaptureTuple {
  promptInput: string;
  input: string | null;
  slmOutput: string | null;
  downstreamResult: string | null;
}

/** Mirrors the .NET GenerationGuidanceRequest DTO. */
export interface GenerationGuidance {
  coverageGoals: string | null;
  edgeCases: string | null;
  constraints: string | null;
}
