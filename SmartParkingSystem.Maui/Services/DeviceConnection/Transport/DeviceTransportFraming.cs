namespace SmartParkingSystem.Maui.Services.DeviceConnection.Transport;

internal static class DeviceTransportFraming
{
    private const string FrameMarker = "|||";

    public static string WrapPayload(string payload)
    {
        return $"{FrameMarker}{payload}{FrameMarker}";
    }

    public static string UnwrapPayload(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var trimmed = line.Trim();
        var endIndex = trimmed.LastIndexOf(FrameMarker, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return trimmed;
        }

        var leadingMarkerLength = 0;
        while (leadingMarkerLength < trimmed.Length
               && leadingMarkerLength < FrameMarker.Length
               && trimmed[leadingMarkerLength] == '|')
        {
            leadingMarkerLength++;
        }

        if (leadingMarkerLength == 0 || leadingMarkerLength >= endIndex)
        {
            return trimmed;
        }

        return trimmed[leadingMarkerLength..endIndex].Trim();
    }
}