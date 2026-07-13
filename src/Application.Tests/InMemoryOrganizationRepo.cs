using Application.Ports;
using Domain;

namespace Application.Tests;

/// <summary>Shared in-memory <see cref="IOrganizationRepository"/> fake for Application tests.</summary>
internal sealed class InMemoryOrganizationRepo : IOrganizationRepository
{
    public readonly List<Organization> Saved = [];

    public InMemoryOrganizationRepo(params Organization[] seed) => Saved.AddRange(seed);

    public Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        Saved.Add(organization);
        return Task.CompletedTask;
    }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Saved.SingleOrDefault(o => o.Id == id));

    public Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Organization>>(Saved);

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
