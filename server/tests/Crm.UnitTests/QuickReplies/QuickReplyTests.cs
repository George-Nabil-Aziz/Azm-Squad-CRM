using Crm.Application.Auth;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Security;
using Crm.Application.QuickReplies;
using Crm.Domain.QuickReplies;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.QuickReplies;

public class QuickReplyTemplateTests
{
    private static readonly QuickReplyValues Values = new("Nour Trading", "TKT-000012", "Printer is down", "Sara Agent");

    [Fact]
    public void Render_ReplacesEveryKnownPlaceholder()
    {
        var text = QuickReplyTemplate.Render(
            "Hello {{customer.name}}, about {{ticket.number}} ({{ticket.subject}}). Regards, {{agent.name}}", Values);

        Assert.Equal("Hello Nour Trading, about TKT-000012 (Printer is down). Regards, Sara Agent", text);
    }

    [Fact]
    public void Render_IgnoresSpacingAndCase_AndLeavesUnknownPlaceholders()
    {
        var text = QuickReplyTemplate.Render("{{ Customer.Name }} / {{TICKET.NUMBER}} / {{order.id}}", Values);

        Assert.Equal("Nour Trading / TKT-000012 / {{order.id}}", text);
    }

    [Fact]
    public void Render_DoesNotExpandPlaceholdersInsideTheValues()
    {
        var text = QuickReplyTemplate.Render("{{customer.name}} {{ticket.number}}", Values with { CustomerName = "{{ticket.number}}" });

        Assert.Equal("{{ticket.number}} TKT-000012", text);
    }

    [Fact]
    public void Render_WithoutPlaceholders_ReturnsTheBody() => Assert.Equal("Thanks!", QuickReplyTemplate.Render("Thanks!", Values));
}

internal sealed class QuickReplyUser(Guid id, bool canManageShared) : ICurrentUser
{
    public Guid? UserId { get; } = id;

    public bool IsInRole(string role) => false;

    public bool HasPermission(string permission) => canManageShared || permission != Permissions.QuickRepliesManageShared;
}

internal sealed class FakeQuickReplyRepository : IQuickReplyRepository
{
    public List<QuickReply> Replies { get; } = [];

    public Dictionary<Guid, QuickReplyValues> Tickets { get; } = [];

    public void Add(QuickReply reply) => Replies.Add(reply);

    public void Remove(QuickReply reply) => Replies.Remove(reply);

    public Task<QuickReply?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Replies.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<QuickReplyResponse>> ListAsync(Guid userId, string? search, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuickReplyResponse>>([.. Replies
            .Where(r => (r.IsShared || r.OwnerId == userId)
                        && (search == null || r.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                            || (r.Shortcut?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(r => r.Title).Select(r => QuickReplyService.ToResponse(r, userId, null))]);

    public Task<QuickReplyResponse?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Replies.FirstOrDefault(r => r.Id == id) is { } r ? QuickReplyService.ToResponse(r, userId, null) : null);

    public Task<QuickReplyValues?> FindTicketValuesAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.FromResult(Tickets.TryGetValue(ticketId, out var values) ? values : null);

    public Task<string?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<string?>("Sara Agent");

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class QuickReplyServiceTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private readonly FakeQuickReplyRepository _repository = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));

    private QuickReplyService Service(Guid? user = null, bool canShare = false) =>
        new(_repository, new QuickReplyUser(user ?? Me, canShare), _clock, new QuickReplyRequestValidator());

    private static QuickReplyRequest Request(string title = "Thanks", string? shortcut = "/thanks", bool? shared = false, string body = "Hi {{customer.name}}") =>
        new(title, shortcut, body, shared);

    [Fact]
    public async Task Create_APersonalReply_BelongsToTheCurrentUser()
    {
        var reply = await Service().CreateAsync(Request(), CancellationToken.None);

        Assert.Equal(("Thanks", "/thanks", false, true), (reply.Title, reply.Shortcut, reply.IsShared, reply.IsMine));
        Assert.Equal(Me, Assert.Single(_repository.Replies).OwnerId);
    }

    [Fact]
    public async Task Create_ASharedReply_NeedsThePermission()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => Service().CreateAsync(Request(shared: true), CancellationToken.None));

        var reply = await Service(canShare: true).CreateAsync(Request(shared: true), CancellationToken.None);

        Assert.True(reply.IsShared);
    }

    [Fact]
    public async Task Update_ASharedReplyWithoutPermission_IsForbidden()
    {
        var shared = await Service(Other, canShare: true).CreateAsync(Request(shared: true), CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service().UpdateAsync(shared.Id, Request("Changed", shared: true), CancellationToken.None)); // AC 3
        await Assert.ThrowsAsync<ForbiddenException>(() => Service().DeleteAsync(shared.Id, CancellationToken.None));

        var updated = await Service(canShare: true).UpdateAsync(shared.Id, Request("Changed", shared: true), CancellationToken.None);
        Assert.Equal("Changed", updated.Title);
    }

    [Fact]
    public async Task SomebodyElsesPersonalReply_IsNotFound()
    {
        var personal = await Service(Other).CreateAsync(Request(), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().UpdateAsync(personal.Id, Request(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().DeleteAsync(personal.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().RenderAsync(personal.Id, new RenderQuickReplyRequest(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Update_MakingMyReplyShared_NeedsThePermission()
    {
        var mine = await Service().CreateAsync(Request(), CancellationToken.None);

        await Assert.ThrowsAsync<ForbiddenException>(() => Service().UpdateAsync(mine.Id, Request(shared: true), CancellationToken.None));
    }

    [Fact]
    public async Task List_ShowsMineAndSharedOnly_AndSearchesByTitleOrShortcut()
    {
        await Service().CreateAsync(Request("Greeting", "/hi"), CancellationToken.None);
        await Service(Other).CreateAsync(Request("Secret", "/secret"), CancellationToken.None);
        await Service(Other, canShare: true).CreateAsync(Request("Refund policy", "/refund", true), CancellationToken.None);

        var all = await Service().ListAsync(new ListQuickRepliesQuery(null), CancellationToken.None);
        var byTitle = await Service().ListAsync(new ListQuickRepliesQuery("refund pol"), CancellationToken.None);
        var byShortcut = await Service().ListAsync(new ListQuickRepliesQuery("/hi"), CancellationToken.None);

        Assert.Equal(["Greeting", "Refund policy"], all.Select(r => r.Title));
        Assert.Equal(["Refund policy"], byTitle.Select(r => r.Title)); // AC 4
        Assert.Equal(["Greeting"], byShortcut.Select(r => r.Title));
    }

    [Fact]
    public async Task Render_FillsThePlaceholdersFromTheTicket()
    {
        var ticket = Guid.NewGuid();
        _repository.Tickets[ticket] = new QuickReplyValues("Nour Trading", "TKT-000012", "Printer is down", "ignored");
        var reply = await Service().CreateAsync(Request(body: "Hi {{customer.name}}, {{ticket.number}}. {{agent.name}}"), CancellationToken.None);

        var rendered = await Service().RenderAsync(reply.Id, new RenderQuickReplyRequest(ticket), CancellationToken.None);

        Assert.Equal("Hi Nour Trading, TKT-000012. Sara Agent", rendered.Text); // AC 2
    }

    [Fact]
    public async Task Render_ForAnUnknownTicket_IsNotFound()
    {
        var reply = await Service().CreateAsync(Request(), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => Service().RenderAsync(reply.Id, new RenderQuickReplyRequest(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithoutTitleOrBody_IsAValidationError()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(new QuickReplyRequest(" ", null, "", null), CancellationToken.None));

        Assert.Contains("title", error.Errors.Keys);
        Assert.Contains("body", error.Errors.Keys);
    }
}
