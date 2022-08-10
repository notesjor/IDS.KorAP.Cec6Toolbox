using System.Diagnostics;

namespace IDS.KorAP.Cec6Toolbox.ZipChecker
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var files = Directory.GetFiles(args[0], "*.zip", SearchOption.TopDirectoryOnly);
      if (!Directory.Exists("output"))
        Directory.CreateDirectory("output");

      Console.WriteLine($"{files.Length} ZIP-Files found...");

      Parallel.ForEach(files, file =>
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
          });

          var lines = process.StandardOutput.ReadToEnd().Split(new []{"\r\n", "\r", "\n"}, StringSplitOptions.RemoveEmptyEntries);
          process.WaitForExit();

          var known = new HashSet<string>();
          var error = new List<string>();

          foreach (var line in lines)
          {
            if (known.Contains(line))
              error.Add(line);
            known.Add(line);
          }

          if (error.Count == 0)
            return;

          File.WriteAllLines(Path.Combine("output", Path.GetFileName(file)), error);
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
          Console.WriteLine(ex.StackTrace);
        }
      });

      Directory.Delete("tmp", true);
    }
  }
}