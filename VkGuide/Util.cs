using System.Reflection;

namespace VkGuide;

public static class Util
{
    private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
    public static byte[] ReadBytesFromResource(string filename)
    {
        var resName = CurrentAssembly.GetManifestResourceNames().FirstOrDefault(s => s.EndsWith(filename));
        if (resName == null) throw new ApplicationException($"Could not find resource name for {filename}");
        using var stream = CurrentAssembly.GetManifestResourceStream(resName);
        using var ms = new MemoryStream();
        if (stream is null) return Array.Empty<byte>();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
