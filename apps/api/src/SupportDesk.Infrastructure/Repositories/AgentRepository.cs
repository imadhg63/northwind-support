using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    public async Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        await db.Set<Agent>().Include(a => a.Specializations).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Agent>> GetActiveAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .Include(a => a.Specializations)
            .Where(a => a.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);
}