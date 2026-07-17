import { OrgRole } from '../users/user';

/** Mirrors the .NET admin org DTOs (spec 4.4). */
export interface OrganizationAdmin {
  id: string;
  name: string;
  memberCount: number;
}

export interface OrgMember {
  userId: string;
  email: string;
  displayName: string;
  role: OrgRole;
}
