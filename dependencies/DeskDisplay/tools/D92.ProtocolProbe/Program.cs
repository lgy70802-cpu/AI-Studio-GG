using System.Text.Json;
using System.IO;
using D92.ProtocolProbe;

if (args.Length == 2 && args[0] == "--clock-self-test")
{
    var directory = Path.GetFullPath(args[1]);
    if (Directory.Exists(directory)) throw new ArgumentException("Use a new clock evidence directory");
    Directory.CreateDirectory(directory);
    await using var clock = new DiagnosticClockFrames();
    var time = DateTimeOffset.Now;
    var first = await clock.RenderAsync(time);
    var firstHash = System.Security.Cryptography.SHA256.HashData(first);
    var second = await clock.RenderAsync(time.AddSeconds(1));
    if (!firstHash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(first)))
        throw new InvalidOperationException("Clock JPEG snapshot mutated");
    if (first.SequenceEqual(second)) throw new InvalidOperationException("Clock did not change with time");
    foreach (var bytes in new[] { first, second })
    {
        using var input = new MemoryStream(bytes, writable: false);
        var frame = new System.Windows.Media.Imaging.JpegBitmapDecoder(input,
            System.Windows.Media.Imaging.BitmapCreateOptions.None,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames[0];
        if (frame.PixelWidth != 480 || frame.PixelHeight != 1280)
            throw new InvalidOperationException("Wrong D69 transport dimensions");
    }
    File.WriteAllBytes(Path.Combine(directory, "clock-first.jpg"), first);
    File.WriteAllBytes(Path.Combine(directory, "clock-next-second.jpg"), second);
    Console.WriteLine(JsonSerializer.Serialize(new { result = "pass", dimensions = "480x1280",
        scene = "ClockScene/SceneView", changedWithTime = true, independentJpegSnapshots = true, deviceCommandsSent = 0 }));
    return 0;
}

if (args.Length == 2 && args[0] == "--send")
{
    try
    {
        await DirectImageSender.Send(args[1]);
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new { result = "send_failed", error = exception.Message,
            errorType = exception.GetType().Name, hresult = exception.HResult, visualConfirmation = "not_established" }));
        return 1;
    }
}

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: D92.ProtocolProbe <vendor-header-fixtures.json> | --send <explicit-device-plan.json>");
    return 2;
}

using var fixture = JsonDocument.Parse(File.ReadAllText(args[0]));
var comparisons = 0;
foreach (var c in fixture.RootElement.GetProperty("cases").EnumerateArray())
{
    int Get(string name) => c.GetProperty(name).GetInt32();
    var actual = CandidateFrameEncoder.Header(Get("payloadLength"), (byte)Get("field12"),
        Get("width"), Get("height"), Get("x"), Get("y"));
    var expected = Convert.FromHexString(c.GetProperty("headerHex").GetString()!);
    if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("Vendor header comparison failed");
    comparisons++;
}
if (comparisons == 0) throw new InvalidOperationException("No vendor fixtures");

// Boundary checks validate candidate transport framing, not hardware acceptance.
var framingCases = 0;
foreach (var reportLength in new[] { 2, 17, 65, 513, 1025 })
foreach (var length in new[] { 0, 1, 31, 32, 33, 991, 992, 993, 1024, 2049 })
{
    var payload = Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();
    var header = CandidateFrameEncoder.Header(length, 7, 123, 456, 9, 11);
    var reports = CandidateFrameEncoder.Reports(header, payload, reportLength, 0x5a).ToArray();
    var expectedCount = (header.Length + length + reportLength - 2) / (reportLength - 1);
    if (reports.Length != expectedCount || reports.Any(r => r.Length != reportLength || r[0] != 0x5a))
        throw new InvalidOperationException("Report boundary or ID failure");
    var reconstructed = reports.SelectMany(r => r.Skip(1)).ToArray();
    var expected = header.Concat(payload).ToArray();
    if (!reconstructed.Take(expected.Length).SequenceEqual(expected) || reconstructed.Skip(expected.Length).Any(b => b != 0))
        throw new InvalidOperationException("Data loss or nonzero padding");
    framingCases++;
}

var rejectedInputs = 0;
void MustReject(Action action)
{
    try { action(); } catch (ArgumentException) { rejectedInputs++; return; }
    throw new InvalidOperationException("Invalid input was accepted");
}
MustReject(() => CandidateFrameEncoder.Header(-1, 0, 1, 1, 0, 0));
MustReject(() => CandidateFrameEncoder.Header(int.MaxValue, 0, 1, 1, 0, 0));
MustReject(() => CandidateFrameEncoder.Header(0, 0, 0, 1, 0, 0));
MustReject(() => CandidateFrameEncoder.Header(0, 0, 1, 65536, 0, 0));
MustReject(() => CandidateFrameEncoder.Header(0, 0, 1, 1, -1, 0));
MustReject(() => CandidateFrameEncoder.Header(0, 0, 1, 1, 0, 65536));
MustReject(() => CandidateFrameEncoder.Reports(CandidateFrameEncoder.Header(0, 0, 1, 1, 0, 0), [], 1, 0).ToArray());
MustReject(() => CandidateFrameEncoder.Reports(CandidateFrameEncoder.Header(1, 0, 1, 1, 0, 0), [], 65, 0).ToArray());
Console.WriteLine(JsonSerializer.Serialize(new { result = "pass", vendorHeaderComparisons = comparisons,
    framingCases, rejectedInputs, deviceCommandsSent = 0, hardwareAcceptance = "not_verified" }));
return 0;
