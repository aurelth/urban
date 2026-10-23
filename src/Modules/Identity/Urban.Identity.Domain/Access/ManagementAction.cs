namespace Urban.Identity.Domain.Access;

/// <summary>Ações de gestão de contas de usuário sujeitas à <see cref="AccessPolicy"/>.</summary>
public enum ManagementAction
{
    Create,
    Deactivate,
    Activate,
    Suspend,
    Reactivate,
    Delete,
}
