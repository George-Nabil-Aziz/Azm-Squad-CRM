namespace Crm.Domain.Customers;

/// <summary>
/// Codes of the things a timeline entry can say happened. The client translates them
/// (<c>customers.timeline.events.&lt;code&gt;</c>); later stories add their own codes here.
/// </summary>
public static class InteractionEvents
{
    public const string CustomerCreated = "customerCreated";

    public const string CustomerUpdated = "customerUpdated";

    public const string ContactAdded = "contactAdded";
}
