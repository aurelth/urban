using System.Collections.Concurrent;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

/// <summary>Guarda os e-mails em memória em vez de enviá-los, para os testes lerem o link de confirmação.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private const string TokenMarker = "token=";

    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    /// <summary>Ligado, faz o envio falhar como se o provedor o tivesse recusado; nada é guardado.</summary>
    public bool RefuseDeliveries { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (RefuseDeliveries)
        {
            throw new EmailDeliveryException("O provedor recusou o envio (simulado no teste).");
        }

        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public EmailMessage[] SentTo(string address) => _messages.Where(m => m.To == address).ToArray();

    public static string TokenIn(EmailMessage message)
    {
        int start = message.TextBody.IndexOf(TokenMarker, StringComparison.Ordinal) + TokenMarker.Length;
        string rest = message.TextBody[start..];
        int end = rest.IndexOfAny(['\r', '\n', ' ']);

        return Uri.UnescapeDataString(end < 0 ? rest : rest[..end]);
    }
}