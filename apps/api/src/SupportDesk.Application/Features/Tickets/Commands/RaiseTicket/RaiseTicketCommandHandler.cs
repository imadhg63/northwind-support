using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Triage;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer with automatic triage.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    TicketTriageService triageService,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(RaiseTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        // ... rest of your triage, ticket raising, and saving logic

        // ... rest of your triage and ticket raising logic
        var now = clock.UtcNow;

        // Fetch active agents and compute open ticket counts for triage assignment (BR-5)
        var activeAgents = await agents.GetActiveAsync(ct);
        var openTicketCounts = await tickets.GetOpenCountsPerAgentAsync(ct);

        // Run through centralized triage service
        var triageResult = triageService.Triage(
            customer,
            category,
            request.RequestedPriority,
            activeAgents,
            openTicketCounts,
            now);

        // Raise ticket with triaged priority, due date, and assigned agent
        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            triageResult.Priority,
            now);

        // Set SLA due date using the domain method
        ticket.SetDueDate(triageResult.DueAtUtc, now);

        // Assign agent if one was selected by triage
        if (triageResult.AssignedAgent != null)
        {
            // Assign agent using the existing domain method (AssignTo)
            ticket.AssignTo(triageResult.AssignedAgent?.Id, now);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised and triaged for customer {CustomerId}. Priority: {Priority}, Assigned Agent: {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.Priority, triageResult.AssignedAgent?.Id);

        return await getTicket.HandleAsync(ticket.Id, ct);
    }
}