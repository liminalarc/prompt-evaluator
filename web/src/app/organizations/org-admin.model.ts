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

// ── Request-to-join access (2.21) ─────────────────────────────────────────────────────────────────

export type AccessRequestStatus = 'Pending' | 'Approved' | 'Denied';

/** One org in the discovery directory, with the current user's relationship to it (2.21). */
export interface OrgDirectoryEntry {
  id: string;
  name: string;
  isMember: boolean;
  hasPendingRequest: boolean;
}

/** One access request in an owner's Requests queue, projected with the requester's identity (2.21). */
export interface AccessRequest {
  id: string;
  requesterId: string;
  requesterEmail: string;
  requesterDisplayName: string;
  organizationId: string;
  requestedRole: OrgRole;
  status: AccessRequestStatus;
  createdAt: string;
}
