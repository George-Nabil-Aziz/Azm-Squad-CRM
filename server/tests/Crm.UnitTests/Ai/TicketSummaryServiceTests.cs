using Crm.Application.Ai;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using Crm.UnitTests.Tickets;

namespace Crm.UnitTests.Ai;

public class TicketSummaryServiceTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private readonly FakeTicketRepository _tickets = new(new FakeTicketCategoryRepository());
    private readonly FakeTicketMessageRepository _messages = new();
    private readonly FakeTicketSummaryRepository _summaries = new();
    private readonly FakeAiTextService _ai = new();
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly TicketSummaryService _service;
    private readonly Ticket _ticket;

    public TicketSummaryServiceTests()
    {
        _service = new TicketSummaryService(_tickets, _messages, _summaries, _ai, new FakeCurrentUser(AgentId), _clock);
        var customerId = _tickets.AddCustomer("Nour Trading");
        _ticket = Ticket.Create(customerId, "Invoice is wrong", "Please call me on +966 50 123 4567", null,
            TicketPriority.High, TicketChannel.Email, null, _clock.UtcNow.UtcDateTime);
        _ticket.AssignNumber(1);
        _tickets.Tickets.Add(_ticket);
    }

    private int _seconds;

    private void AddInbound(string body) =>
        _messages.Add(TicketMessage.Inbound(_ticket.Id, body, TicketChannel.Email, null, _clock.UtcNow.UtcDateTime.AddSeconds(_seconds++)));

    private Task<TicketSummaryResponse> GenerateAsync() => _service.GenerateAsync(_ticket.Id, CancellationToken.None);

    [Fact]
    public async Task Generate_SummarizesTheThread_AndSavesItWithTheClockTime()
    {
        AddInbound("My invoice shows line 3 twice.");
        _ai.Answer = "- Duplicate charge on line 3";

        var result = await GenerateAsync();

        Assert.Equal("- Duplicate charge on line 3", result.Text);
        Assert.Equal(_clock.UtcNow.UtcDateTime, result.GeneratedAt);
        var request = Assert.Single(_ai.Requests);
        Assert.Contains("Invoice is wrong", request.User);
        Assert.Contains("My invoice shows line 3 twice.", request.User);
        Assert.Equal(1, _summaries.SaveCount);
    }

    [Fact]
    public async Task Generate_UsesTheTicketLanguage_ArabicCustomerMessage()
    {
        AddInbound("الفاتورة فيها خطأ في البند الثالث وأحتاج مساعدة عاجلة من فضلكم");
        AddInbound("شكرا لكم على سرعة الرد والمتابعة");

        await GenerateAsync();

        Assert.Contains("Arabic", _ai.Requests[0].System);
        Assert.Equal("ar", (await _service.GetAsync(_ticket.Id, CancellationToken.None)).Language);
    }

    [Fact]
    public async Task Generate_EnglishTicket_AsksForEnglish()
    {
        AddInbound("My invoice shows line 3 twice.");

        await GenerateAsync();

        Assert.Contains("English", _ai.Requests[0].System);
        Assert.Equal("en", (await _service.GetAsync(_ticket.Id, CancellationToken.None)).Language);
    }

    [Fact]
    public async Task Generate_MasksEmailsAndPhones_AndNeverSendsTheCustomerName()
    {
        AddInbound("Reach me at nour@corp.example or 0501234567.");

        await GenerateAsync();

        var sent = _ai.Requests[0].User + _ai.Requests[0].System;
        Assert.DoesNotContain("nour@corp.example", sent);
        Assert.DoesNotContain("0501234567", sent);
        Assert.DoesNotContain("966 50 123", sent);
        Assert.DoesNotContain("Nour Trading", sent);
        Assert.Contains("[email]", sent);
        Assert.Contains("[phone]", sent);
    }

    [Fact]
    public async Task Generate_LabelsInternalNotes_AndAgentReplies()
    {
        _messages.Add(TicketMessage.Staff(_ticket.Id, "Checking with finance.", false, TicketChannel.Email, AgentId, _clock.UtcNow.UtcDateTime));
        _messages.Add(TicketMessage.Staff(_ticket.Id, "Customer is a VIP.", true, TicketChannel.Email, AgentId, _clock.UtcNow.UtcDateTime));

        await GenerateAsync();

        var prompt = _ai.Requests[0].User;
        Assert.Contains("Agent: Checking with finance.", prompt);
        Assert.Contains("Internal note: Customer is a VIP.", prompt);
    }

    [Fact]
    public async Task Regenerating_ReplacesTheSummary_AndMovesTheTimestamp()
    {
        _ai.Answers.AddRange(["first", "second"]);
        await GenerateAsync();
        _clock.UtcNow = _clock.UtcNow.AddHours(2);

        var second = await GenerateAsync();

        var row = Assert.Single(_summaries.Rows);
        Assert.Equal("second", row.Text);
        Assert.Equal(_clock.UtcNow.UtcDateTime, row.GeneratedAt);
        Assert.Equal("second", second.Text);
    }

    [Fact]
    public async Task WhenTheAiFails_NothingIsSaved_AndTheOldSummaryStays()
    {
        _ai.Answer = "kept";
        await GenerateAsync();
        _ai.Failure = new AiFailedException("boom");

        await Assert.ThrowsAsync<AiFailedException>(GenerateAsync);

        Assert.Equal("kept", (await _service.GetAsync(_ticket.Id, CancellationToken.None)).Text);
        Assert.Equal(1, _summaries.SaveCount);
    }

    [Fact]
    public async Task EmptyAnswer_IsAFailure_NothingSaved()
    {
        _ai.Answer = "   ";

        await Assert.ThrowsAsync<AiFailedException>(GenerateAsync);

        Assert.Empty(_summaries.Rows);
    }

    [Fact]
    public async Task NotConfigured_Throws_BeforeAnythingIsSaved()
    {
        _ai.IsConfigured = false;

        await Assert.ThrowsAsync<AiNotConfiguredException>(GenerateAsync);

        Assert.Empty(_summaries.Rows);
        Assert.Empty(_ai.Requests);
    }

    [Fact]
    public async Task UnknownTicket_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GenerateAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_WithoutASummary_HasNullFields()
    {
        var result = await _service.GetAsync(_ticket.Id, CancellationToken.None);

        Assert.Equal(new TicketSummaryResponse(null, null, null), result);
    }

    [Fact]
    public async Task ALongThread_KeepsTheNewestMessages_AndTheSubject()
    {
        for (var i = 0; i < 200; i++)
        {
            AddInbound($"message-{i:D3} " + new string('x', 200));
        }

        await GenerateAsync();

        var prompt = _ai.Requests[0].User;
        Assert.True(prompt.Length < TicketTranscript.MaxChars + 500);
        Assert.Contains("Invoice is wrong", prompt);
        Assert.Contains("message-199", prompt);
        Assert.DoesNotContain("message-000", prompt);
    }
}
