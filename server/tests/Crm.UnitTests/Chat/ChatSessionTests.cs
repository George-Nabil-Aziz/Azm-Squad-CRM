using Crm.Domain.Chat;

namespace Crm.UnitTests.Chat;

public class ChatSessionTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc);

    private static ChatSession NewSession() => ChatSession.Start("Nour Ali", "Nour@Example.com", "hash", Now);

    [Fact]
    public void Start_BeginsWaiting_WithATrimmedNameAndALowerCaseEmail()
    {
        var session = ChatSession.Start("  Nour Ali ", "Nour@Example.com", "hash", Now);

        Assert.Equal((ChatStatus.Waiting, "Nour Ali", "nour@example.com", Now), (session.Status, session.VisitorName, session.VisitorEmail, session.StartedAt));
        Assert.Null(session.AgentId);
    }

    [Fact]
    public void Accept_MovesWaitingToActive_WithTheAgent()
    {
        var session = NewSession();
        var agent = Guid.NewGuid();

        session.Accept(agent, "Sara", Now.AddMinutes(1));

        Assert.Equal((ChatStatus.Active, agent, "Sara", (DateTime?)Now.AddMinutes(1)), (session.Status, session.AgentId, session.AgentName, session.AcceptedAt));
    }

    [Fact]
    public void Accept_TwiceOrAfterTheEnd_Throws()
    {
        var session = NewSession();
        session.Accept(Guid.NewGuid(), "Sara", Now);
        Assert.Throws<InvalidOperationException>(() => session.Accept(Guid.NewGuid(), "Omar", Now));

        var ended = NewSession();
        ended.End(Now);
        Assert.Throws<InvalidOperationException>(() => ended.Accept(Guid.NewGuid(), "Omar", Now));
    }

    [Fact]
    public void End_SetsEndedAt_AndCannotBeRepeated()
    {
        var session = NewSession();

        session.End(Now.AddMinutes(5));

        Assert.Equal((ChatStatus.Ended, (DateTime?)Now.AddMinutes(5)), (session.Status, session.EndedAt));
        Assert.Throws<InvalidOperationException>(() => session.End(Now.AddMinutes(6)));
    }

    [Fact]
    public void AddMessage_KeepsOrder_TrimsAndLimitsTheBody()
    {
        var session = NewSession();

        session.AddMessage(ChatSender.Visitor, "Nour Ali", "  Hello  ", Now);
        session.AddMessage(ChatSender.Visitor, "Nour Ali", new string('a', ChatMessage.BodyMaxLength + 50), Now.AddSeconds(1));

        Assert.Equal("Hello", session.Messages[0].Body);
        Assert.Equal(ChatMessage.BodyMaxLength, session.Messages[1].Body.Length);
    }

    [Fact]
    public void AddMessage_AfterTheEnd_Throws()
    {
        var session = NewSession();
        session.End(Now);

        Assert.Throws<InvalidOperationException>(() => session.AddMessage(ChatSender.Agent, "Sara", "Hi", Now));
    }

    [Fact]
    public void AddMessage_WithoutText_Throws()
    {
        Assert.Throws<ArgumentException>(() => NewSession().AddMessage(ChatSender.Visitor, "Nour", "   ", Now));
    }

    [Fact]
    public void LinkTicket_StoresTheTicketId()
    {
        var session = NewSession();
        var ticket = Guid.NewGuid();

        session.LinkTicket(ticket, "TKT-000001");

        Assert.Equal((ticket, "TKT-000001"), (session.TicketId, session.TicketNumber));
    }
}
