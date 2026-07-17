import { OrgRole } from './users/user';

/** Mirrors the .NET OrganizationResponse DTO — the top-level container (1.9). */
export interface Organization {
  id: string;
  name: string;
  /**
   * The current user's role in this org (4.5). Present on the member-scoped `/api/organizations`
   * list; the client uses it to gate owner-only UI (the server enforces authoritatively).
   */
  role?: OrgRole;
}
