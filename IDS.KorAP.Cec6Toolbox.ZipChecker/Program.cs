using System.Diagnostics;
using CorpusExplorer.Sdk.Helper;

namespace IDS.KorAP.Cec6Toolbox.ZipChecker
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var files = Directory.GetFiles(args[0], "*.zip", SearchOption.TopDirectoryOnly);
      if (!Directory.Exists("error-copycat"))
        Directory.CreateDirectory("error-copycat");
      if (!Directory.Exists("error-zipfile"))
        Directory.CreateDirectory("error-zipfile");

      Console.WriteLine($"{files.Length} ZIP-Files found...");

      Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 40 }, file =>
      {
        try
        {
          var process = Process.Start(new ProcessStartInfo
          {
            FileName = "zipinfo",
            Arguments = $"-1 {file}",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
          });

          var entries = process.StandardOutput.ReadToEnd().Split(Splitter.LineBreaks, StringSplitOptions.RemoveEmptyEntries);
          var zipError = process.StandardError.ReadToEnd();
          process.WaitForExit();

          var known = new HashSet<string>();
          var error = new List<string>();

          foreach (var entry in entries)
          {
            if (known.Contains(entry))
              error.Add(entry);
            known.Add(entry);
          }

          if (error.Count > 0)
            File.WriteAllLines(Path.Combine("error-copycat", Path.GetFileName(file)), error);
          if(!string.IsNullOrWhiteSpace(zipError))
            File.WriteAllText(Path.Combine("error-zipfile", Path.GetFileName(file)), zipError);
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
          Console.WriteLine(ex.StackTrace);
        }
      });
    }
  }
}