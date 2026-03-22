namespace Dofus210.Host.Options;

public sealed class ServerOptions
{
    public const string SectionName = "Server";

    public string Name { get; set; } = "Dofus 2.10 Private Server";

    public string Host { get; set; } = "127.0.0.1";

    public int AuthPort { get; set; } = 5555;

    public int GamePort { get; set; } = 5556;

    public bool AutoMigrate { get; set; }
}

