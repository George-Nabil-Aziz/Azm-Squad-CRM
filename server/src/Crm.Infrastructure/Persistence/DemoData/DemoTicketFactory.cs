using Crm.Domain.Customers;
using Crm.Domain.Notifications;
using Crm.Domain.Portal;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Infrastructure.Persistence.DemoData;

internal sealed record DemoAgent(Guid Id, string Name, double Weight, double Speed, double ExtraBreach, double Quality);

internal sealed record DemoCustomer(Customer Customer, bool Arabic);

/// <summary>
/// Builds one demo ticket with its whole life (messages, status changes through the allowed transitions, SLA times from the
/// policies, breaches, survey). Everything is created through the domain methods; nothing is sent to any channel
/// (replies are marked as sent and no outbound message is queued). All times are UTC and never in the future.
/// </summary>
internal sealed class DemoTicketFactory(
    Random rnd, DateTime now, IReadOnlyDictionary<TicketPriority, SlaPolicy> policies, List<object> sink)
{
    private static readonly (TicketChannel Channel, double Weight)[] Channels =
    [
        (TicketChannel.Email, 30), (TicketChannel.WhatsApp, 25), (TicketChannel.WebForm, 10), (TicketChannel.Chat, 10),
        (TicketChannel.Sms, 5), (TicketChannel.Portal, 10), (TicketChannel.Manual, 10),
    ];

    private static readonly (TicketPriority Priority, double Weight)[] Priorities =
        [(TicketPriority.High, 20), (TicketPriority.Mid, 50), (TicketPriority.Low, 30)];

    public required IReadOnlyList<Guid> CategoryIds { get; init; }

    public required IReadOnlyList<Guid?> DepartmentIds { get; init; }

    public required IReadOnlyDictionary<int, IReadOnlyList<DemoAgent>> AgentsByDepartment { get; init; }

    public Guid? SupervisorId { get; init; }

    public List<Ticket> Tickets { get; } = [];

    public int RatedCount { get; private set; }

    private double Frac(double lo, double hi) => lo + ((hi - lo) * rnd.NextDouble());

    private T Pick<T>(IReadOnlyList<T> items) => items[rnd.Next(items.Count)];

    private T Weighted<T>(IReadOnlyList<(T Item, double Weight)> items)
    {
        var roll = rnd.NextDouble() * items.Sum(i => i.Weight);
        foreach (var (item, weight) in items)
        {
            roll -= weight;
            if (roll < 0)
            {
                return item;
            }
        }

        return items[^1].Item;
    }

    private DateTime At(DateTime start, double minutes) => start.AddMinutes(minutes);

    public void Build(int number, DateTime created, DemoCustomer customer, bool forceUnansweredHigh = false)
    {
        var channel = Weighted(Channels);
        var priority = forceUnansweredHigh ? TicketPriority.High : Weighted(Priorities);
        var group = Weighted(DemoDataCatalog.GroupWeights.Select((w, i) => (i, w)).ToArray());
        var arabic = rnd.NextDouble() < 0.93 ? customer.Arabic : !customer.Arabic;
        var template = Pick((arabic ? DemoDataCatalog.TicketsAr : DemoDataCatalog.TicketsEn)[group]);
        var reference = rnd.Next(10000, 99999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var subject = template.Subject.Replace("{ref}", reference, StringComparison.Ordinal);
        var body = template.Body.Replace("{ref}", reference, StringComparison.Ordinal);

        var department = DepartmentIds[DemoDataCatalog.GroupDepartment[group]];
        var pool = AgentsByDepartment.TryGetValue(DemoDataCatalog.GroupDepartment[group], out var inDepartment) && inDepartment.Count > 0
            ? inDepartment
            : [.. AgentsByDepartment.Values.SelectMany(a => a).DistinctBy(a => a.Id)];
        var agent = pool.Count == 0 ? null : Weighted(pool.Select(a => (a, a.Weight)).ToArray());
        var createdBy = channel == TicketChannel.Manual ? agent?.Id : null;

        var policy = policies[priority];
        var ticket = Ticket.Create(customer.Customer.Id, subject, body, CategoryIds[group % CategoryIds.Count], priority, channel, createdBy, created);
        ticket.AssignNumber(number);
        ticket.AssignBranch(customer.Customer.BranchId);
        ticket.ChangeDepartment(department, created);
        ticket.ApplySla(policy);
        Tickets.Add(ticket);
        sink.Add(ticket);
        sink.Add(CustomerInteraction.Create(
            customer.Customer.Id, InteractionType.Ticket, InteractionEvents.TicketCreated, $"{ticket.DisplayNumber} {subject}",
            ticket.Id, createdBy, created));
        sink.Add(TicketMessage.Inbound(ticket.Id, body, channel, null, created));

        // The plan: how fast the first response and the resolution would be, and whether this ticket breaches its SLA.
        var respWindow = (double)policy.ResponseMinutes;
        var resWindow = (double)policy.ResolutionMinutes;
        var pb = 0.083 + (agent?.ExtraBreach ?? 0);
        var roll = rnd.NextDouble();
        var breachResp = forceUnansweredHigh || roll < pb * 0.4;
        var breachRes = roll >= pb * 0.25 && roll < pb;
        var speed = agent?.Speed ?? 1;
        var respMin = forceUnansweredHigh
            ? respWindow * 5
            : breachResp ? respWindow * Frac(1.15, 2.5) : Math.Min(respWindow * Frac(0.08, 0.8) * speed, respWindow * 0.95);
        respMin = Math.Max(respMin, 3);
        var resMin = breachRes ? resWindow * Frac(1.1, 2.2) : Math.Min(resWindow * Frac(0.2, 0.9) * speed, resWindow * 0.97);
        resMin = Math.Max(resMin, respMin + 15);

        var age = (now - created).TotalMinutes;
        var ageDays = age / 1440;
        var canResolve = !forceUnansweredHigh && age > resMin + 30;
        var pResolved = ageDays > 7 ? 0.95 : ageDays > 2 ? 0.8 : ageDays > 0.5 ? 0.45 : 0.1;
        var mustResolve = !breachRes && age > resWindow;
        var staleOpen = breachRes && ageDays > 7 && rnd.NextDouble() < 0.5; // an overdue ticket nobody closed
        var resolved = canResolve && !staleOpen && (rnd.NextDouble() < pResolved || mustResolve || ageDays > 7);
        var hasResponse = !forceUnansweredHigh && age > respMin + 2;

        var events = new List<(DateTime At, Action Do)>();
        var end = now.AddMinutes(-1);
        var reply1 = At(created, respMin);
        TicketStatus final;
        DateTime? resolveAt = null;
        DateTime? pendingAt = null;
        if (resolved)
        {
            resolveAt = At(created, resMin);
            final = TicketStatus.Resolved;
            if (ageDays > 0.3 && resolveAt.Value.AddHours(4) < end && rnd.NextDouble() < 0.65)
            {
                final = TicketStatus.Closed;
            }
        }
        else if (!hasResponse)
        {
            final = TicketStatus.New;
        }
        else
        {
            final = age > respMin + 60 && rnd.NextDouble() < 0.28 ? TicketStatus.Pending : TicketStatus.Open;
        }

        var fullySpan = resolveAt ?? end;
        if (final == TicketStatus.Pending)
        {
            pendingAt = reply1 + ((end - reply1) * Frac(0.3, 0.8));
            fullySpan = pendingAt.Value;
        }

        var lang = arabic;
        if (final != TicketStatus.New)
        {
            var assignAt = At(created, Math.Min(respMin * 0.5, 20));
            if (agent is not null)
            {
                events.Add((assignAt, () =>
                {
                    ticket.AssignTo(agent.Id, assignAt);
                    sink.Add(TicketHistoryEntry.Create(ticket.Id, TicketHistoryField.Assignee, null, agent.Name, null, assignAt));
                }));
            }

            events.Add((reply1, () =>
            {
                sink.Add(Reply(ticket, Pick(lang ? DemoDataCatalog.AgentRepliesAr : DemoDataCatalog.AgentRepliesEn), agent?.Id, reply1));
                ticket.RecordAgentReply(reply1);
                ticket.ChangeStatus(TicketStatus.Open, reply1);
                sink.Add(TicketHistoryEntry.Create(ticket.Id, TicketHistoryField.Status, "new", "open", agent?.Id, reply1));
            }));

            var span = (fullySpan - reply1).TotalMinutes;
            if (span > 40 && rnd.NextDouble() < 0.6)
            {
                var tCustomer = reply1.AddMinutes(span * Frac(0.15, 0.4));
                var tAgent = tCustomer.AddMinutes((fullySpan - tCustomer).TotalMinutes * Frac(0.05, 0.2));
                events.Add((tCustomer, () =>
                {
                    sink.Add(TicketMessage.Inbound(ticket.Id, Pick(lang ? DemoDataCatalog.CustomerFollowUpsAr : DemoDataCatalog.CustomerFollowUpsEn), channel, null, tCustomer));
                    ticket.RecordCustomerMessage(tCustomer);
                }));
                events.Add((tAgent, () =>
                {
                    sink.Add(Reply(ticket, Pick(lang ? DemoDataCatalog.AgentRepliesAr : DemoDataCatalog.AgentRepliesEn), agent?.Id, tAgent));
                    ticket.RecordAgentReply(tAgent);
                }));
            }

            if (span > 20 && rnd.NextDouble() < 0.4)
            {
                var tNote = reply1.AddMinutes(span * Frac(0.1, 0.6));
                events.Add((tNote, () => sink.Add(TicketMessage.Staff(
                    ticket.Id, Pick(DemoDataCatalog.InternalNotes), true, channel, agent?.Id, tNote))));
            }

            if (pendingAt is { } tPending)
            {
                events.Add((tPending, () =>
                {
                    ticket.ChangeStatus(TicketStatus.Pending, tPending);
                    sink.Add(TicketHistoryEntry.Create(ticket.Id, TicketHistoryField.Status, "open", "pending", agent?.Id, tPending));
                }));
            }
            else if (resolveAt is { } tResolved)
            {
                var detour = span > 120 && rnd.NextDouble() < 0.2;
                if (detour)
                {
                    var toPending = reply1.AddMinutes(span * 0.3);
                    var backToOpen = reply1.AddMinutes(span * 0.5);
                    events.Add((toPending, () =>
                    {
                        ticket.ChangeStatus(TicketStatus.Pending, toPending);
                        sink.Add(TicketHistoryEntry.Create(ticket.Id, TicketHistoryField.Status, "open", "pending", agent?.Id, toPending));
                    }));
                    events.Add((backToOpen, () =>
                    {
                        ticket.ChangeStatus(TicketStatus.Open, backToOpen);
                        sink.Add(TicketHistoryEntry.Create(ticket.Id, TicketHistoryField.Status, "pending", "open", agent?.Id, backToOpen));
                    }));
                }

                events.Add((tResolved, () =>
                {
                    sink.Add(Reply(ticket, Pick(lang ? DemoDataCatalog.ResolutionRepliesAr : DemoDataCatalog.ResolutionRepliesEn), agent?.Id, tResolved));
                    ticket.RecordAgentReply(tResolved);
                    ticket.ChangeStatus(TicketStatus.Resolved, tResolved);
                    sink.Add(TicketHistoryEntry.Create(ticket.Id, TicketHistoryField.Status, "open", "resolved", agent?.Id, tResolved));
                }));
                if (final == TicketStatus.Closed)
                {
                    var tClosed = tResolved.AddHours(Frac(4, 40));
                    tClosed = tClosed > end ? end : tClosed;
                    events.Add((tClosed, () =>
                    {
                        ticket.ChangeStatus(TicketStatus.Closed, tClosed);
                        sink.Add(TicketHistoryEntry.Create(ticket.Id, TicketHistoryField.Status, "resolved", "closed", agent?.Id, tClosed));
                    }));
                }
            }
        }

        var order = 0;
        foreach (var (_, action) in events.Select(e => (e.At, e.Do)).OrderBy(e => e.At).ThenBy(_ => order++))
        {
            action();
        }

        AddSlaEvents(ticket, agent);
        if (resolveAt is { } resolvedTime)
        {
            AddSurvey(ticket, agent, resolvedTime, lang);
        }
    }

    private static TicketMessage Reply(Ticket ticket, string text, Guid? authorId, DateTime at)
    {
        var message = TicketMessage.Staff(ticket.Id, text, false, ticket.Channel, authorId, at);
        if (message.DeliveryStatus is not null)
        {
            message.MarkSent(null); // the demo reply counts as delivered; nothing is queued or sent
        }

        return message;
    }

    private void AddSlaEvents(Ticket ticket, DemoAgent? agent)
    {
        var responseBreached = ticket.IsResponseBreachedAt(now);
        var resolutionBreached = ticket.IsResolutionBreachedAt(now);
        if (responseBreached && ticket.MarkResponseBreached())
        {
            sink.Add(TicketSlaEvent.Create(ticket.Id, SlaEventType.ResponseBreached, 0, ticket.ResponseDueAt, ticket.ResponseDueAt!.Value));
        }

        if (resolutionBreached && ticket.MarkResolutionBreached())
        {
            sink.Add(TicketSlaEvent.Create(ticket.Id, SlaEventType.ResolutionBreached, 0, ticket.ResolutionDueAt, ticket.ResolutionDueAt!.Value));
        }

        if ((responseBreached || resolutionBreached) && ticket.ResolvedAt is null)
        {
            var due = (responseBreached ? ticket.ResponseDueAt : ticket.ResolutionDueAt)!.Value;
            var at = due.AddMinutes(30) < now ? due.AddMinutes(30) : now.AddMinutes(-1);
            var level = ticket.Escalate(at);
            sink.Add(TicketSlaEvent.Create(ticket.Id, SlaEventType.Escalated, level, due, at));
            if (SupervisorId is { } supervisor)
            {
                sink.Add(Notification.ForUser(
                    supervisor, ticket.Id, NotificationType.SlaEscalation, level, $"demo:escalation:{ticket.Id}",
                    $"{ticket.DisplayNumber} breached its SLA and was escalated.", at));
            }
        }
    }

    private void AddSurvey(Ticket ticket, DemoAgent? agent, DateTime resolvedAt, bool arabic)
    {
        var survey = TicketSurvey.Issue(ticket.Id, Guid.NewGuid().ToString("N"), resolvedAt, TimeSpan.FromDays(7));
        var ratedAt = resolvedAt.AddHours(Frac(1, 40));
        if (rnd.NextDouble() < 0.68 && ratedAt < now)
        {
            var r = Math.Clamp(rnd.NextDouble() + ((agent?.Quality ?? 0) * 0.2), 0, 0.9999);
            var rating = r < 0.05 ? 1 : r < 0.12 ? 2 : r < 0.25 ? 3 : r < 0.55 ? 4 : 5;
            if ((ticket.ResponseBreached || ticket.ResolutionBreached) && rating > 1 && rnd.NextDouble() < 0.5)
            {
                rating--;
            }

            string? comment = null;
            if (rating <= 3)
            {
                comment = Pick(arabic ? DemoDataCatalog.CsatLowAr : DemoDataCatalog.CsatLowEn);
            }
            else if (rnd.NextDouble() < 0.35)
            {
                comment = Pick(arabic ? DemoDataCatalog.CsatHighAr : DemoDataCatalog.CsatHighEn);
            }

            survey.Rate(rating, comment, ratedAt);
            RatedCount++;
        }

        sink.Add(survey);
    }
}
