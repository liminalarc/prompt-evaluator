/** A role a catalog model can serve (spec 1.13). */
export type ModelRole = 'subject' | 'judge' | 'generator';

/** Mirrors the .NET ModelResponse DTO — one entry in the workspace-wide Model Catalog (1.13). */
export interface ModelCatalogEntry {
  id: string;
  modelId: string;
  displayName: string;
  provider: string;
  roles: ModelRole[];
  inputPricePerMTokUsd: number | null;
  outputPricePerMTokUsd: number | null;
  isActive: boolean;
  /** Whether the eval-runner has configured credentials for this model's provider (1.13). */
  available: boolean;
}
