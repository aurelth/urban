namespace Urban.Identity.Application.Abstractions;

/// <summary>Versão vigente dos termos de uso e da política de privacidade, a única que um novo aceite pode citar.</summary>
public interface ICurrentTerms
{
    string Version { get; }
}