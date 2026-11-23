namespace Urban.Operations.Application.Abstractions;

/// <summary>Endereços do site que aparecem nos e-mails dos comunicados. Mudam por ambiente, então vêm da configuração.</summary>
public interface IAnnouncementLinks
{
    /// <returns>O endereço do mural de comunicados.</returns>
    Uri GetMuralUri();
}
