namespace Urban.Identity.Application.Abstractions;

public sealed class SessionStoreUnavailableException(Exception innerException)
    : Exception("O armazenamento de sessões está indisponível.", innerException);