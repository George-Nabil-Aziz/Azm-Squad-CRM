using Crm.Application.Ai;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Paging;
using Crm.Application.Common.Security;
using Crm.Application.Common.Validation;
using Crm.Application.Customers.Timeline;
using Crm.Application.Departments;
using Crm.Application.Settings;
using Crm.Application.Sla;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using FluentValidation;
using ValidationException = Crm.Application.Common.Exceptions.ValidationException;

namespace Crm.Application.Tickets;

/// <summary>
/// Ticket use cases: validation, customer / category checks, the clock, sequential numbers, the customer timeline,
/// storage through <see cref="ITicketRepository"/>.
/// </summary>
public sealed class TicketService(
    ITicketRepository tickets,
    ITicketCategoryRepository categories,
    IInteractionRecorder timeline,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<CreateTicketRequest> createValidator,
    IValidator<ListTicketsQuery> listValidator,
    ISlaPolicyRepository slaPolicies,
    ITicketHistoryRecorder history,
    ISystemSettingsProvider settings,
    IAutoAssignmentService? autoAssigner = null,
    IDepartmentRepository? departments = null,
    IDataScope? dataScope = null,
    IAiClassificationService? aiClassification = null) : ITicketService
{
    public Task<TicketResponse> CreateAsync(CreateTicketRequest request, CancellationToken cancellationToken) =>
        CreateCoreAsync(request, TicketChannel.Manual, currentUser.UserId, cancellationToken);

    public Task<TicketResponse> CreateForCustomerAsync(
        Guid customerId, CreateTicketRequest request, TicketChannel channel, CancellationToken cancellationToken) =>
        CreateCoreAsync(request with { CustomerId = customerId }, channel, null, cancellationToken);

    private async Task<TicketResponse> CreateCoreAsync(
        CreateTicketRequest request, TicketChannel channel, Guid? createdById, CancellationToken cancellationToken)
    {
        await createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var customerId = request.CustomerId!.Value;
        if (!await tickets.CustomerExistsAsync(customerId, cancellationToken))
        {
            throw FieldError("customerId", TicketText.CustomerNotFound);
        }

        if (request.CategoryId is { } categoryId
            && await categories.FindAsync(categoryId, cancellationToken) is not { IsActive: true })
        {
            throw FieldError("categoryId", TicketText.CategoryUnavailable);
        }

        var departmentId = await ResolveDepartmentAsync(request.DepartmentId, cancellationToken);
        var priorityGiven = TicketValues.TryParsePriority(request.Priority, out var parsed);
        var priority = priorityGiven ? parsed : TicketPriority.Mid;
        var categoryId0 = request.CategoryId;
        // CRM-52: the AI suggestion is applied (above the threshold) to what the creator left empty, before the SLA timers and
        // the auto-assignment. Unavailable AI gives null: the ticket is created as usual.
        var aiOutcome = aiClassification is null
            ? null
            : await aiClassification.ClassifyAsync(request.Subject!, request.Description, cancellationToken);
        var categoryApplied = aiOutcome is { MeetsThreshold: true, CategoryId: not null } && categoryId0 is null;
        var priorityApplied = aiOutcome is { MeetsThreshold: true } && !priorityGiven;
        if (categoryApplied)
        {
            categoryId0 = aiOutcome!.CategoryId;
        }

        if (priorityApplied)
        {
            priority = aiOutcome!.Priority;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var runtime = await settings.GetAsync(cancellationToken); // CRM-35: business hours + ticket prefix
        var ticket = Ticket.Create(customerId, request.Subject!, request.Description, categoryId0, priority,
            channel, createdById, now);
        ticket.ChangeDepartment(departmentId, now); // CRM-61
        ticket.AssignBranch(await tickets.GetCustomerBranchAsync(customerId, cancellationToken)); // CRM-62: tickets follow their customer
        // CRM-20: due times come from the policy of the priority now; later policy changes do not move them.
        // CRM-61: a department override of the priority wins over the global policy.
        if (await slaPolicies.FindEffectiveAsync(priority, departmentId, cancellationToken) is { } policy)
        {
            ticket.ApplySla(policy, runtime.Calendar);
        }

        if (aiOutcome is not null)
        {
            aiClassification!.Save(ticket.Id, aiOutcome, categoryApplied, priorityApplied, now);
        }

        var autoAssignee = autoAssigner is null
            ? null
            : await autoAssigner.TryAssignAsync(ticket, now, cancellationToken); // CRM-27: before the save, so the history joins it

        // CRM-10 AC 2: the ticket shows in the customer's timeline; the entry is saved together with the ticket.
        await TicketNumbering.SaveNewAsync(tickets, ticket, runtime.TicketPrefix, () => timeline.Record(
            customerId, InteractionType.Ticket, InteractionEvents.TicketCreated,
            $"{ticket.DisplayNumber} {ticket.Subject}", ticket.Id, now), cancellationToken);

        if (autoAssigner is not null && autoAssignee is { } agent && agent != currentUser.UserId)
        {
            await autoAssigner.NotifyAssignedAsync(ticket.Id, agent, now, cancellationToken); // CRM-28
        }

        return await GetAsync(ticket.Id, cancellationToken);
    }

    public async Task<TicketResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await tickets.GetViewAsync(id, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound));

    public async Task<PagedResult<TicketResponse>> ListAsync(ListTicketsQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateOrThrowAsync(query, cancellationToken);
        var search = query.Search?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            search = null;
        }

        var filter = new TicketListFilter(
            TicketValues.TryParseStatus(query.Status, out var status) ? status : null,
            TicketValues.TryParsePriority(query.Priority, out var priority) ? priority : null,
            query.CategoryId,
            query.AssigneeId,
            query.Unassigned == true,
            StartOfUtcDay(query.CreatedFrom),
            StartOfUtcDay(query.CreatedTo?.AddDays(1)),
            search,
            Ticket.TryParseNumber(search, out var number) ? number : null,
            DepartmentId: query.DepartmentId);

        var page = await tickets.ListAsync(
            filter,
            query.Page ?? PagingDefaults.DefaultPage,
            query.PageSize ?? PagingDefaults.DefaultPageSize,
            cancellationToken);

        return new PagedResult<TicketResponse>([.. page.Items.Select(ToResponse)], page.Page, page.PageSize, page.TotalCount);
    }

    public Task<IReadOnlyList<TicketAssigneeResponse>> ListAssigneesAsync(CancellationToken cancellationToken) =>
        tickets.ListAssigneesAsync(cancellationToken);

    public async Task<TicketResponse> ChangePriorityAsync(
        Guid id, ChangeTicketPriorityRequest request, CancellationToken cancellationToken)
    {
        if (!TicketValues.TryParsePriority(request.Priority, out var priority))
        {
            throw FieldError("priority", TicketText.PriorityInvalid);
        }

        var ticket = await tickets.FindAsync(id, cancellationToken) ?? throw new NotFoundException(TicketText.NotFound);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var old = ticket.Priority;
        // CRM-20 AC 2: the due times are recalculated from CreatedAt with the new priority current policy.
        var calendar = (await settings.GetAsync(cancellationToken)).Calendar;
        ticket.ChangePriority(priority, await slaPolicies.FindEffectiveAsync(priority, ticket.DepartmentId, cancellationToken), now, calendar);
        if (old != priority)
        {
            if (aiClassification is not null)
            {
                await aiClassification.RecordPriorityChangeAsync(ticket.Id, priority, now, cancellationToken); // CRM-52
            }

            history.Record(ticket.Id, TicketHistoryField.Priority, TicketValues.PriorityName(old), TicketValues.PriorityName(priority), now);
        }

        await tickets.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>The API shape of a ticket view (also used by the list).</summary>
    public static TicketResponse ToResponse(TicketView view)
    {
        var ticket = view.Ticket;
        return new TicketResponse(
            ticket.Id,
            ticket.DisplayNumber,
            ticket.Subject,
            ticket.Description,
            TicketValues.StatusName(ticket.Status),
            TicketValues.PriorityName(ticket.Priority),
            TicketValues.ChannelName(ticket.Channel),
            ticket.CustomerId,
            view.CustomerName,
            ticket.CategoryId,
            view.CategoryName,
            ticket.AssigneeId,
            view.AssigneeName,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ResponseDueAt,
            ticket.ResolutionDueAt,
            ticket.FirstResponseAt,
            ticket.ResolvedAt,
            ticket.ResponseBreached,
            ticket.ResolutionBreached,
            ticket.EscalationLevel,
            ticket.ResponseWarnedAt,
            [.. TicketStatusRules.AllowedTargets(ticket.Status).Select(TicketValues.StatusName)],
            ticket.DepartmentId,
            view.DepartmentName,
            ticket.BranchId);
    }

    /// <summary>
    /// CRM-61: the department of a new ticket. Given: it must exist, be active and (for a department-restricted agent) be one
    /// of theirs. Not given: a restricted agent with exactly one department gets it, everyone else leaves it empty.
    /// </summary>
    private async Task<Guid?> ResolveDepartmentAsync(Guid? requested, CancellationToken cancellationToken)
    {
        var restricted = dataScope is { RestrictDepartments: true };
        if (requested is not { } id)
        {
            return restricted && dataScope!.DepartmentIds.Count == 1 ? dataScope.DepartmentIds[0] : null;
        }

        var department = departments is null ? null : await departments.FindAsync(id, cancellationToken);
        if (department is not { IsActive: true })
        {
            throw FieldError("departmentId", DepartmentText.Unavailable);
        }

        if (restricted && !dataScope!.DepartmentIds.Contains(id))
        {
            throw FieldError("departmentId", DepartmentText.NotYours);
        }

        return id;
    }

    private static DateTime? StartOfUtcDay(DateOnly? day) =>
        day?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private static ValidationException FieldError(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
