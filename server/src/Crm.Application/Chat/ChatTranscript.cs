using System.Globalization;
using System.Text;
using Crm.Domain.Chat;
using Crm.Domain.Tickets;

namespace Crm.Application.Chat;

/// <summary>The text of a finished chat, one line per message ("[2026-10-06 08:01] Sara: Hello"), UTC times.</summary>
public static class ChatTranscript
{
    public static string Build(IEnumerable<ChatMessage> messages, int maxLength = Ticket.DescriptionMaxLength)
    {
        var text = new StringBuilder();
        foreach (var message in messages.OrderBy(m => m.SentAt))
        {
            var line = string.Create(CultureInfo.InvariantCulture, $"[{message.SentAt:yyyy-MM-dd HH:mm}] {message.SenderName}: {message.Body}");
            if (text.Length + line.Length + 1 > maxLength)
            {
                // The oldest lines are kept; the rest is cut.
                var room = maxLength - text.Length - 1;
                if (room > 1)
                {
                    text.Append(line.AsSpan(0, room - 1)).Append('…');
                }

                break;
            }

            text.Append(line).Append('\n');
        }

        return text.Length == 0 ? ChatText.NoMessages : text.ToString().TrimEnd('\n');
    }
}
