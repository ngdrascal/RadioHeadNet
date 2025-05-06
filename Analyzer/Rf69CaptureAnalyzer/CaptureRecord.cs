namespace Rf69CaptureAnalyzer;

internal record CaptureRecord(RecordTypes RecordType, double Start, byte? Mosi, byte? Miso);