/** A role a catalog model can serve (spec 1.13). */
export type ModelRole = 'subject' | 'judge' | 'generator';

/** Where a catalog entry's displayed price comes from (6.2). */
export type PriceSource = 'override' | 'table' | 'none';

/** Mirrors the .NET ModelResponse DTO — one entry in the workspace-wide Model Catalog (1.13). */
export interface ModelCatalogEntry {
  id: string;
  modelId: string;
  displayName: string;
  provider: string;
  roles: ModelRole[];
  /** The per-model price override (kept, editable); null when the model uses the table rate (6.2). */
  inputPricePerMTokUsd: number | null;
  outputPricePerMTokUsd: number | null;
  /**
   * The **displayed** price = override ?? authoritative ledger pricing-table rate (6.2). This is the
   * single number the catalog shows, matching what the AI-usage ledger charges.
   */
  effectiveInputPricePerMTokUsd: number | null;
  effectiveOutputPricePerMTokUsd: number | null;
  /** Whether the displayed price is an override, the table rate, or unavailable (6.2). */
  priceSource: PriceSource;
  isActive: boolean;
  /** Whether the eval-runner has configured credentials for this model's provider (1.13). */
  available: boolean;
}
