namespace Application.Identity;

/// <summary>
/// The lifecycle state of an organization access request (2.21). A user requests access to an org
/// (Pending); its owner (or a workspace admin) approves — granting membership — or denies. Kept
/// framework-free in Application so both the port and the Infrastructure store share it.
/// </summary>
public enum AccessRequestStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
}
