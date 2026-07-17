/** Mirrors the .NET admin user DTOs (spec 4.3). */
export type OrgRole = 'Owner' | 'Member';

export interface UserMembership {
  organizationId: string;
  role: OrgRole;
}

export interface UserDetail {
  id: string;
  email: string;
  displayName: string;
  isAdmin: boolean;
  memberships: UserMembership[];
}
