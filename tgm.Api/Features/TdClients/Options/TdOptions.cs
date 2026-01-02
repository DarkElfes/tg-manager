namespace tgm.Api.Features.TdClients.Options;

public class TdOptions
{
    public const string Td = "Td";

    public int ApiId { get; set; }
    public string ApiHash { get; set; } = null!;
    public string Directory { get; set; } = null!;
}
