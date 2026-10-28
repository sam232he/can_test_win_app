using System.IO;
using System.IO.Compression;

internal static class ZipMaker
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
            return 1;
        if (File.Exists(args[1]))
            File.Delete(args[1]);
        ZipFile.CreateFromDirectory(args[0], args[1], CompressionLevel.Optimal, false);
        return 0;
    }
}
