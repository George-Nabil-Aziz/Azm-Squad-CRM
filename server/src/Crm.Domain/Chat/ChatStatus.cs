namespace Crm.Domain.Chat;

/// <summary>Where a live chat is: waiting for an agent, running, or finished (stored by name).</summary>
public enum ChatStatus
{
    Waiting,
    Active,
    Ended,
}

/// <summary>Who wrote a chat message (stored by name).</summary>
public enum ChatSender
{
    Visitor,
    Agent,
}
