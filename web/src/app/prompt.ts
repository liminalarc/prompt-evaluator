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
