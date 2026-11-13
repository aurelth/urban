namespace Urban.ArchTests;

/// <summary>Raiz do repositório (onde fica o Urban.slnx), para os testes que leem arquivos de infraestrutura.</summary>
internal static class RepositoryRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Urban.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Não achei a raiz do repositório (Urban.slnx).");
    }
}
