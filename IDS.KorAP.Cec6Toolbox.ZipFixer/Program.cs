using System.IO.Compression;
using System.Text;
using Ionic.Zip;

namespace IDS.KorAP.Cec6Toolbox.ZipFixer
{
  internal class Program
  {
    static void Main(string[] args)
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

      if (!args[1].Contains("error-copycat"))
        throw new ArgumentException("[INPUT] [error-copycat from Zip-Checker] [OUTPUT]");

      var input = args[0];
      var fixes = Directory.GetFiles(args[1]); // Erstellt mit ZipChecker (error-copycat)
      var output = args[2];

      foreach (var file in fixes)
      {
        try
        {
          var block = GetBlockInfo(file);
          var name = Path.GetFileName(file);

          if (!File.Exists(Path.Combine(input, name)))
            continue;

          using (var inputStream = new ZipInputStream(Path.Combine(input, name)))
            using (var fs = new FileStream(Path.Combine(output, name), FileMode.Create, FileAccess.Write))
          using (var outputStream = new ZipArchive(fs, ZipArchiveMode.Create))
          {
            while (true)
            {
              var entryInput = inputStream.GetNextEntry();
              if (entryInput == null)
                break;

              var key = entryInput.FileName;

              // Block-Check
              if (block.ContainsKey(key))
              {
                block[key]--;
                if (block[key] < 1)
                  block.Remove(key);
                continue;
              }

              var buffer = new byte[entryInput.UncompressedSize];
              inputStream.Read(buffer, 0, buffer.Length);

              var entry = outputStream.CreateEntry(key, CompressionLevel.Fastest);
              using(var tmp = entry.Open())
                tmp.Write(buffer, 0, buffer.Length);
            }
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
          Console.WriteLine(ex.StackTrace);
        }
      }
    }

    private static Dictionary<string, int> GetBlockInfo(string file)
    {
      var lines = File.ReadAllLines(file);

      var res = new Dictionary<string, int>();
      foreach (var line in lines)
        if (res.ContainsKey(line))
          res[line]++;
        else
          res.Add(line, 1);
      return res;
    }
  }
}