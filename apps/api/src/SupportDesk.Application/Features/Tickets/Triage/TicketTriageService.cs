using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Triage;

public sealed class TicketTriageService
{
    private readonly SlaOptions _sla = new();

    public TriageResult Triage(
        Customer customer,
        Category category,
        TicketPriority? requestedPriority,
        IReadOnlyCollection<Agent> activeAgents,
        IReadOnlyDictionary<int, int> agentOpenTicketCounts,
        DateTime now)
    {
        // BR-1: Priority Rule
        TicketPriority priority;
        string priorityReason;

        if (category.ForcesCriticalPriority)
        {
            priority = TicketPriority.Critical;
            priorityReason = $"Category '{category.Name}' is always treated as Critical";
        }
        else
        {
            priority = requestedPriority ?? TicketPriority.Medium;
            priorityReason = requestedPriority.HasValue 
                ? $"Requested priority '{requestedPriority.Value}' was respected" 
                : "No priority given, defaulted to Medium";
        }

        // BR-2 & BR-3: SLA Window Rule
        var baseHours = _sla.BaseWindowsHours.GetValueOrDefault(priority.ToString(), 24);
        var isPremium = customer.Tier == CustomerTier.Premium;
        
        double slaHours = baseHours;
        string slaReason;

        if (isPremium)
        {
            slaHours = Math.Max(_sla.MinimumWindowHours, baseHours * (double)_sla.PremiumMultiplier);
            slaReason = $"{priority} base window {baseHours}h, halved for a Premium customer";
        }
        else
        {
            slaReason = $"{priority} base window {baseHours}h for a Standard customer";
        }

        var dueAtUtc = now.AddHours(slaHours);

        // BR-4 & BR-5: Agent Selection Rule
        Agent? selectedAgent = null;
        string assignmentReason;

        var eligibleAgents = activeAgents
            .Where(agent => agent.IsActive)
            .Where(agent => 
            {
                if (category.RequiresSpecialist)
                {
                    return agent.Specializations.Any(s => s.CategoryId == category.Id);
                }
                return true;
            })
            .Where(agent => 
            {
                var openCount = agentOpenTicketCounts.GetValueOrDefault(agent.Id, 0);
                return openCount < agent.MaxOpenTickets;
            })
            .ToList();

        if (eligibleAgents.Count != 0)
        {
            selectedAgent = eligibleAgents
                .OrderBy(a => agentOpenTicketCounts.GetValueOrDefault(a.Id, 0))
                .ThenBy(a => a.Id)
                .First();

            assignmentReason = category.RequiresSpecialist
                ? $"Fewest open tickets among {category.Name} specialists"
                : "Fewest open tickets among eligible active agents";
        }
        else
        {
            assignmentReason = category.RequiresSpecialist
                ? $"No available agent specialised in {category.Name}"
                : "No active agents available under their max open ticket limit";
        }

        return new TriageResult(
            Priority: priority,
            PriorityReason: priorityReason,
            SlaWindowHours: slaHours,
            SlaReason: slaReason,
            DueAtUtc: dueAtUtc,
            AssignedAgent: selectedAgent,
            AssignmentReason: assignmentReason
        );
    }
}

public record TriageResult(
    TicketPriority Priority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    DateTime DueAtUtc,
    Agent? AssignedAgent,
    string AssignmentReason
);