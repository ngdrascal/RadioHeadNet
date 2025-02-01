using System.ComponentModel.DataAnnotations;

namespace Rf69CaptureAnalyzer;

internal class ParseOptions
{
    [Required]
    public required int RecordTypeIndex { get; set; }
    [Required]
    public required int MosiIndex { get; set; }
    [Required]
    public required int MisoIndex { get; set; }
}