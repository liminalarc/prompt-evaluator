/** Mirrors the .NET PromptVersionResponse DTO. */
export interface PromptVersion {
  id: string;
  versionNumber: number;
  content: string;
  targetModel: string;
  label: string | null;
  sourceApp: string | null;
  createdAt: string;
}

/** Mirrors the .NET PromptResponse DTO. */
export interface Prompt {
  id: string;
  folderId: string | null;
  name: string;
  description: string | null;
  versions: PromptVersion[];
  /** The version the source app runs — the "Current in source" marker (1.16); null until first set. */
  currentVersionId: string | null;
  currentVersionSha: string | null;
  currentVersionSetAt: string | null;
}

/** Mirrors the .NET VersionStatusResponse DTO (1.16) — the derived per-version badges. */
export interface VersionStatus {
  versionId: string;
  versionNumber: number;
  label: string | null;
  isCurrent: boolean;
  /** Underlying signal: this version scores higher than Current (many can). */
  backportEligible: boolean;
  /** The single recommended target — the highest-scoring eligible version. Only this one is badged. */
  isBackportTarget: boolean;
  regressed: boolean;
}

/** Mirrors the .NET PromptVersionStatusResponse DTO (1.16). */
export interface PromptVersionStatus {
  promptId: string;
  currentVersionId: string | null;
  /** The single version to actually ship (highest-scoring above Current), or null when none beats it. */
  backportTargetVersionId: string | null;
  versions: VersionStatus[];
}

/** Mirrors the .NET BackportDiffLineResponse DTO (1.20) — one line of the diff vs Current. */
export interface BackportDiffLine {
  kind: 'context' | 'added' | 'removed';
  text: string;
}

/** Mirrors the .NET BackportScoreDeltaResponse DTO (1.20) — one per-scorer delta (target vs Current). */
export interface BackportScoreDelta {
  datasetName: string;
  scorerLabel: string;
  currentMean: number;
  targetMean: number;
  delta: number;
}

/**
 * Mirrors the .NET BackportArtifactResponse DTO (1.20): the generated artifact for the prompt's single
 * backport target. `content` is the copy-to-clipboard exact prompt; `markdown` is the downloadable
 * `.md`; `diff`/`scoreDeltas` are the structured building blocks.
 */
export interface BackportArtifact {
  promptId: string;
  promptName: string;
  currentVersionNumber: number;
  currentVersionSha: string | null;
  targetVersionNumber: number;
  targetModel: string;
  content: string;
  diff: BackportDiffLine[];
  scoreDeltas: BackportScoreDelta[];
  markdown: string;
  fileName: string;
}

/** Mirrors the .NET PromptSummaryResponse DTO (list/browse projection). */
export interface PromptSummary {
  id: string;
  folderId: string | null;
  name: string;
  description: string | null;
  versionCount: number;
  latestTargetModel: string | null;
}
