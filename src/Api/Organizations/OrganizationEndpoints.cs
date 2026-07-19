using Api.Auth;
using Application.Identity;
using Application.Ports;
using Domain;

namespace Api.Organizations;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/organizations").RequireAuthorization();

        // The switcher: only the organizations the current user is a member of (4.1), each stamped
        // with the caller's role in it (4.5) so the client can gate owner-only UI.
        group.MapGet("/", async (IOrganizationRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            var roles = await access.MyOrgRolesAsync(ct);
            var orgs = await repository.ListAsync(ct);
            return Results.Ok(orgs
                .Where(o => roles.ContainsKey(o.Id))
                .Select(o => OrganizationResponse.From(o, roles[o.Id])));
        });

        // Discovery for request-to-join (2.21): every org, stamped with the caller's relationship to
        // it (member / pending request) so the onboarding can offer a "Request access" action. Org
        // names are visible workspace-wide — the accepted trade-off of directory-based discovery.
        group.MapGet("/directory", async (
            IOrganizationRepository repository, ICurrentUser current, IUserDirectory users, CancellationToken ct) =>
        {
            if (current.UserId is not { } uid)
                return Results.Unauthorized();
            var orgs = await repository.ListAsync(ct);
            var memberOf = (await users.GetAccessibleOrganizationIdsAsync(uid, ct)).ToHashSet();
            var pending = (await users.ListAccessRequestsByRequesterAsync(uid, ct))
                .Where(r => r.Status == AccessRequestStatus.Pending)
                .Select(r => r.OrganizationId)
                .ToHashSet();
            return Results.Ok(orgs.Select(o => new OrgDirectoryEntryResponse(
                o.Id, o.Name, memberOf.Contains(o.Id), pending.Contains(o.Id))));
        });

        group.MapPost("/", async (CreateOrganizationRequest request, IOrganizationRepository repository,
            ICurrentUser current, IUserDirectory users, CancellationToken ct) =>
        {
            try
            {
                var org = Organization.Create(request.Name);
                await repository.AddAsync(org, ct);

                // Grant the creator ownership — otherwise they couldn't access what they just made (4.1).
                if (current.UserId is { } uid)
                    await users.GrantOrganizationAsync(uid, org.Id, OrgRole.Owner, ct);

                return Results.Created($"/api/organizations/{org.Id}",
                    OrganizationResponse.From(org, OrgRole.Owner));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{id:guid}", async (Guid id, RenameOrganizationRequest request,
            IOrganizationRepository repository, OrgAccess access, CancellationToken ct) =>
        {
            var org = await repository.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NotFound();
            if (!await access.CanAccessOrgAsync(id, ct))
                return Results.Forbid();
            try
            {
                org.Rename(request.Name);
                await repository.SaveChangesAsync(ct);
                var role = (await access.MyOrgRolesAsync(ct)).TryGetValue(id, out var r) ? r : OrgRole.Member;
                return Results.Ok(OrganizationResponse.From(org, role));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Deletes an org and everything under it (folders, prompts, datasets cascade). Idempotent —
        // a missing org is a no-op 204; a member is required to delete one that exists (4.1).
        group.MapDelete("/{id:guid}", async (Guid id, IOrganizationRepository repository,
            IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            var org = await repository.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NoContent();
            if (!await access.CanAccessOrgAsync(id, ct))
                return Results.Forbid();

            await repository.DeleteAsync(id, ct);
            // Memberships live in the Identity context (no cross-context FK cascade) — revoke them
            // explicitly so the deleted org leaves no dangling membership rows (2.21).
            await users.RemoveAllMembersAsync(id, ct);
            return Results.NoContent();
        });

        MapMemberEndpoints(group);
        MapAccessRequestEndpoints(group);
        return app;
    }

    /// <summary>
    /// Request-to-join access (2.21) — the pull counterpart to 4.5's add-by-email push. Any
    /// authenticated user may request access to an org (create); an owner/admin sees the pending
    /// queue and approves (→ grant) or denies — the list/decide endpoints share 4.5's
    /// owner-or-admin gate (<see cref="OrgAccess.CanManageOrgMembersAsync"/>). Best-effort email
    /// notifications ride the <see cref="IEmailSender"/> seam (dev logs; real provider at deploy).
    /// </summary>
    private static void MapAccessRequestEndpoints(IEndpointRouteBuilder group)
    {
        // Any authenticated user (who isn't already a member) may request to join.
        group.MapPost("/{id:guid}/access-requests", async (
            Guid id, CreateAccessRequestRequest request, IOrganizationRepository organizations,
            ICurrentUser current, IUserDirectory users, IEmailSender email, IConfiguration config,
            CancellationToken ct) =>
        {
            if (current.UserId is not { } uid)
                return Results.Unauthorized();
            if (!string.IsNullOrWhiteSpace(request.Role)
                && !Enum.TryParse<OrgRole>(request.Role, ignoreCase: true, out _))
                return Results.BadRequest(new { error = $"Unknown role '{request.Role}'." });
            var role = ParseRole(request.Role);
            var org = await organizations.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NotFound();

            var result = await users.RequestOrganizationAccessAsync(uid, id, role, ct);
            switch (result.Outcome)
            {
                case AccessRequestCreateOutcome.AlreadyMember:
                    return Results.BadRequest(new { error = "You are already a member of this organization." });
                case AccessRequestCreateOutcome.DuplicateOpenRequest:
                    return Results.Conflict(new { error = "You already have a pending request to join this organization." });
            }

            await NotifyOwnersAsync(users, email, config, org, uid, ct);
            return Results.Created($"/api/organizations/{id}/access-requests/{result.RequestId}", new { id = result.RequestId });
        });

        // Owner/admin: the pending queue for this org.
        group.MapGet("/{id:guid}/access-requests", async (
            Guid id, IOrganizationRepository organizations, IUserDirectory users, OrgAccess access,
            CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();
            var requests = await users.ListPendingAccessRequestsAsync(id, ct);
            return Results.Ok(requests.Select(AccessRequestResponse.From));
        });

        // Owner/admin: approve → grants membership at the chosen (or requested) role.
        group.MapPost("/{id:guid}/access-requests/{requestId:guid}/approve", async (
            Guid id, Guid requestId, ApproveAccessRequestRequest request, IOrganizationRepository organizations,
            ICurrentUser current, IUserDirectory users, OrgAccess access, IEmailSender email,
            IConfiguration config, CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (!string.IsNullOrWhiteSpace(request.Role)
                && !Enum.TryParse<OrgRole>(request.Role, ignoreCase: true, out _))
                return Results.BadRequest(new { error = $"Unknown role '{request.Role}'." });

            var org = await organizations.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NotFound();
            // The request must belong to this org (guard against a mismatched route).
            var existing = await users.GetAccessRequestAsync(requestId, ct);
            if (existing is null || existing.OrganizationId != id)
                return Results.NotFound();

            var deciderId = current.UserId ?? Guid.Empty;
            var result = await users.ApproveAccessRequestAsync(requestId, deciderId, ParseRoleOrNull(request.Role), ct);
            if (result.Outcome == AccessRequestDecisionOutcome.NotFound)
                return Results.NotFound();

            await NotifyRequesterAsync(users, email, config, org, result.Request!, approved: true, ct);
            return Results.Ok(AccessRequestResponse.From(result.Request!));
        });

        // Owner/admin: deny a pending request.
        group.MapPost("/{id:guid}/access-requests/{requestId:guid}/deny", async (
            Guid id, Guid requestId, IOrganizationRepository organizations, ICurrentUser current,
            IUserDirectory users, OrgAccess access, IEmailSender email, IConfiguration config,
            CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            var org = await organizations.GetByIdAsync(id, ct);
            if (org is null)
                return Results.NotFound();
            var existing = await users.GetAccessRequestAsync(requestId, ct);
            if (existing is null || existing.OrganizationId != id)
                return Results.NotFound();

            var deciderId = current.UserId ?? Guid.Empty;
            var result = await users.DenyAccessRequestAsync(requestId, deciderId, ct);
            return result.Outcome switch
            {
                AccessRequestDecisionOutcome.NotFound => Results.NotFound(),
                AccessRequestDecisionOutcome.AlreadyApproved =>
                    Results.Conflict(new { error = "This request was already approved." }),
                _ => await DenyOkAsync(users, email, config, org, result.Request!, ct),
            };
        });
    }

    private static OrgRole ParseRole(string? role)
        => ParseRoleOrNull(role) ?? OrgRole.Member;

    private static OrgRole? ParseRoleOrNull(string? role)
        => !string.IsNullOrWhiteSpace(role) && Enum.TryParse<OrgRole>(role, ignoreCase: true, out var r) ? r : null;

    private static async Task<IResult> DenyOkAsync(
        IUserDirectory users, IEmailSender email, IConfiguration config, Organization org,
        AccessRequestInfo request, CancellationToken ct)
    {
        await NotifyRequesterAsync(users, email, config, org, request, approved: false, ct);
        return Results.Ok(AccessRequestResponse.From(request));
    }

    // Best-effort: notify every owner of a new pending request. Never fails the request itself (2.21).
    private static async Task NotifyOwnersAsync(
        IUserDirectory users, IEmailSender email, IConfiguration config, Organization org,
        Guid requesterId, CancellationToken ct)
    {
        try
        {
            var requester = await users.FindByIdAsync(requesterId, ct);
            var members = await users.ListOrganizationMembersAsync(org.Id, ct);
            var link = LinkTo(config, $"/organizations/{org.Id}?tab=requests");
            foreach (var owner in members.Where(m => m.Role == OrgRole.Owner))
                await email.SendAsync(new EmailMessage(
                    owner.Email,
                    $"Access request for {org.Name}",
                    $"{requester?.DisplayName ?? "A user"} ({requester?.Email}) has requested to join {org.Name}. "
                    + $"Review pending requests: {link}"), ct);
        }
        catch
        {
            // Notification is a nice-to-have; the request is already recorded.
        }
    }

    // Best-effort: notify the requester of the decision.
    private static async Task NotifyRequesterAsync(
        IUserDirectory users, IEmailSender email, IConfiguration config, Organization org,
        AccessRequestInfo request, bool approved, CancellationToken ct)
    {
        try
        {
            var verb = approved ? "approved" : "declined";
            var body = approved
                ? $"Your request to join {org.Name} was approved. Open it here: {LinkTo(config, "/")}"
                : $"Your request to join {org.Name} was declined.";
            await email.SendAsync(new EmailMessage(
                request.RequesterEmail, $"Your request to join {org.Name} was {verb}", body), ct);
        }
        catch
        {
            // Best-effort.
        }
    }

    private static string LinkTo(IConfiguration config, string path)
        => (config["Auth:WebBaseUrl"]?.TrimEnd('/') ?? "") + path;

    /// <summary>
    /// Owner-facing member management (4.5): list/add-by-email/set-role/remove, gated
    /// owner-or-admin per org (<see cref="OrgAccess.CanManageOrgMembersAsync"/>) → 403 otherwise.
    /// A last-owner guard blocks demoting/removing an org's final Owner here; a global admin can
    /// still fix an org via the 4.4 admin surface (no guard there).
    /// </summary>
    private static void MapMemberEndpoints(IEndpointRouteBuilder group)
    {
        group.MapGet("/{id:guid}/members", async (
            Guid id, IOrganizationRepository organizations, IUserDirectory users, OrgAccess access,
            CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();
            var members = await users.ListOrganizationMembersAsync(id, ct);
            return Results.Ok(members.Select(OrgMemberResponse.From));
        });

        group.MapPost("/{id:guid}/members", async (
            Guid id, AddOrgMemberByEmailRequest request, IOrganizationRepository organizations,
            IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (!Enum.TryParse<OrgRole>(request.Role, ignoreCase: true, out var role))
                return Results.BadRequest(new { error = $"Unknown role '{request.Role}'." });
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();

            var user = string.IsNullOrWhiteSpace(request.Email)
                ? null
                : await users.FindByEmailAsync(request.Email.Trim(), ct);
            if (user is null)
                return Results.BadRequest(new { error = "No user with that email. Users must register first." });

            await users.GrantOrganizationAsync(user.Id, id, role, ct);
            return Results.NoContent();
        });

        group.MapPut("/{id:guid}/members/{userId:guid}", async (
            Guid id, Guid userId, SetOrgMemberRoleRequest request, IOrganizationRepository organizations,
            IUserDirectory users, OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (!Enum.TryParse<OrgRole>(request.Role, ignoreCase: true, out var role))
                return Results.BadRequest(new { error = $"Unknown role '{request.Role}'." });
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();

            var members = await users.ListOrganizationMembersAsync(id, ct);
            var target = members.FirstOrDefault(m => m.UserId == userId);
            if (target is null)
                return Results.NotFound();

            // Last-owner guard: don't let the final Owner be demoted out of ownership.
            if (target.Role == OrgRole.Owner && role != OrgRole.Owner && IsLastOwner(members, userId))
                return Results.BadRequest(new { error = "An organization must keep at least one owner." });

            await users.GrantOrganizationAsync(userId, id, role, ct);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (
            Guid id, Guid userId, IOrganizationRepository organizations, IUserDirectory users,
            OrgAccess access, CancellationToken ct) =>
        {
            if (!await access.CanManageOrgMembersAsync(id, ct))
                return Results.Forbid();
            if (await organizations.GetByIdAsync(id, ct) is null)
                return Results.NotFound();

            var members = await users.ListOrganizationMembersAsync(id, ct);
            var target = members.FirstOrDefault(m => m.UserId == userId);
            if (target is null)
                return Results.NoContent(); // already not a member — idempotent

            // Last-owner guard: don't strand the org with no owner.
            if (target.Role == OrgRole.Owner && IsLastOwner(members, userId))
                return Results.BadRequest(new { error = "An organization must keep at least one owner." });

            await users.RevokeOrganizationAsync(userId, id, ct);
            return Results.NoContent();
        });
    }

    private static bool IsLastOwner(IReadOnlyList<OrgMemberInfo> members, Guid userId)
        => members.Count(m => m.Role == OrgRole.Owner) == 1
           && members.Single(m => m.Role == OrgRole.Owner).UserId == userId;
}
