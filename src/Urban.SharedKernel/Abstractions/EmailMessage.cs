namespace Urban.SharedKernel.Abstractions;

public sealed record EmailMessage(string To, string Subject, string TextBody);