namespace Application.Identity;

/// <summary>
/// A user's role within an organization they've been granted access to (4.1). The organization is
/// the permission boundary; this distinguishes an owner (may administer membership) from a plain
/// member. Kept framework-free in Application so both the port and the Infrastructure store use it.
/// </summary>
public enum OrgRole
{
    Member = 0,
    Owner = 1,
}
