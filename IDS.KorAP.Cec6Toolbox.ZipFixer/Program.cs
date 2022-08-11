using System.Text;
using Ionic.Zip;

namespace IDS.KorAP.Cec6Toolbox.ZipFixer
{
  internal class Program
  {
    static void Main(string[] args)
    {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

      foreach (var file in args)
      {
        try
        {
          var nFile = file.Replace(".zip", ".new.zip");
          var done = new HashSet<string>();

          using (var input = new ZipInputStream(file))
          using (var output = new ZipFile(nFile, Encoding.UTF8))
          {
            var entry = input.GetNextEntry();
            if (entry == null)
              continue;

            var key = entry.FileName;
            var buffer = new byte[entry.UncompressedSize];
            input.Read(buffer, 0, buffer.Length);

            if (done.Contains(key) && entry.UncompressedSize > 0)
              output.UpdateEntry(key, buffer);
            else
              output.AddEntry(key, buffer);

            done.Add(key);
          }
        }
        catch (Exception ex)
        {
          // ignore
        }
      }
    }
  }
}