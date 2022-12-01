using System.Diagnostics;

namespace IDS.KorAP.Cec6Toolbox.CopySymbolicLinks
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var input = args[0];
      var filter = args[1];
      var output = args[2];

      Directory.SetCurrentDirectory(output);

      foreach (var file in Directory.GetFiles(input, filter, SearchOption.TopDirectoryOnly))
      {
        var link = Path.Combine(output, Path.GetFileName(file));

        var process = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "ln",
            Arguments = $"-s {file} {link}",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
          }
        };
        process.Start();
        process.WaitForExit();
      }
    }
  }
}