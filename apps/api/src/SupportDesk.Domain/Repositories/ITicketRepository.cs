using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(int id, CancellationToken ct);
    Task<string> NextReferenceAsync(CancellationToken ct);
    Task AddAsync(Ticket ticket, CancellationToken ct);
    Task<Dictionary<int, int>> GetOpenCountsPerAgentAsync(CancellationToken ct);
}