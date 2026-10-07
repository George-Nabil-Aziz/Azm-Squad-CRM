using Crm.Application.Chat;

namespace Crm.UnitTests.Chat;

public class AgentPresenceTests
{
    [Fact]
    public void NobodyConnected_MeansNoAgentOnline()
    {
        Assert.False(new InMemoryAgentPresence().HasOnlineAgents);
    }

    [Fact]
    public void AConnectedAgent_IsOnline_UntilTheLastConnectionCloses()
    {
        var presence = new InMemoryAgentPresence();
        var agent = Guid.NewGuid();
        presence.Connected(agent, "tab-1");
        presence.Connected(agent, "tab-2");

        presence.Disconnected("tab-1");
        Assert.True(presence.HasOnlineAgents);
        Assert.Equal([agent], presence.OnlineAgents);

        presence.Disconnected("tab-2");
        Assert.False(presence.HasOnlineAgents);
    }

    [Fact]
    public void DisconnectingAnUnknownConnection_IsIgnored()
    {
        var presence = new InMemoryAgentPresence();

        presence.Disconnected("never-connected");

        Assert.False(presence.HasOnlineAgents);
    }
}
