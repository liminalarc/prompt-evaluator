/** Mirrors the .NET auth `UserResponse` DTO — the signed-in user (4.1). */
export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  /** Workspace-level global admin (1.13) — gates the Model Catalog admin UI. */
  isAdmin: boolean;
}
