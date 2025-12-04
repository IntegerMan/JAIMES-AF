using MattEland.Jaimes.Domain;

namespace MattEland.Jaimes.ServiceDefinitions.Messages;

public class CrackDocumentMessage
{
    public string FilePath { get; set; } = string.Empty;
    public string? RelativeDirectory { get; set; }
    public string RulesetId { get; set; } = string.Empty;
    public string DocumentKind { get; set; } = DocumentKinds.Sourcebook;
}







