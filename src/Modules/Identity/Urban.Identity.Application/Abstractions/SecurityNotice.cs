namespace Urban.Identity.Application.Abstractions;

/// <summary>Avisos de segurança enviados ao e-mail da conta quando algo sensível acontece nela.</summary>
public enum SecurityNotice
{
    /// <summary>A senha foi trocada. Vai ao e-mail atual.</summary>
    PasswordChanged,

    /// <summary>Alguém pediu para trocar o e-mail. Vai ao e-mail atual e cita o endereço pedido.</summary>
    EmailChangeRequested,

    /// <summary>O e-mail foi trocado. Vai ao e-mail anterior e cita o novo.</summary>
    EmailChanged,
}
