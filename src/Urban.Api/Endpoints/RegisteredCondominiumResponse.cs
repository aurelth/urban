namespace Urban.Api.Endpoints;

/// <summary>Só o que a tela precisa depois do cadastro; o CPF volta mascarado e nenhum outro dado pessoal volta.</summary>
public sealed record RegisteredCondominiumResponse(Guid CondominiumId, string Name, string Cnpj, string ManagerCpf);
