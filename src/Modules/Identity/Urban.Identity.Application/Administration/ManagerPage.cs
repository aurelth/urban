using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Administration;

public sealed record ManagerPage(IReadOnlyList<User> Items, int Total);
