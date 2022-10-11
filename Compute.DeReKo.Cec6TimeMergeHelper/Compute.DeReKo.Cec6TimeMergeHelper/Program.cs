using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Helper;

namespace Compute.DeReKo.Cec6TimeMergeHelper
{
  class Program
  {
    private static string _inputDir = "/home/ruediger/CEC6-TIME";
    private static string _outputDir = "/home/ruediger/CEC6-TIME-MERGED";
    private static string _unknownDir = "/home/ruediger/CEC6-TIME-UNDONE";

    static void Main(string[] args)
    {
      var source = GetList(_inputDir);
      var dest = GetList(_outputDir);

      var unknown = FindUnknown(source, dest);

      Console.Write($"{unknown.Length} corpora found - move them to {_unknownDir} ? (y/n):");
      var ask = Console.ReadLine();
      if (ask == "y")
        MoveCorpora(unknown);
    }

    private static void MoveCorpora(string[] unknown)
    {
      if(Directory.Exists(_unknownDir))
        Directory.Delete(_unknownDir, true);
      Directory.CreateDirectory(_unknownDir);

      Parallel.ForEach(unknown, dir =>
      {
        var iP = Path.Combine(_inputDir, dir);
        var oP = Path.Combine(_unknownDir, dir);

        Directory.CreateDirectory(oP);
        var files = Directory.GetFiles(iP);
        Parallel.ForEach(files, file =>
        {
          var dest = Path.Combine(oP, Path.GetFileName(file));
          File.Copy(file, dest);
        });
      });
    }

    private static string[] FindUnknown(string[] source, string[] dest)
    {
      var filter = new HashSet<string>(dest);
      return source.Where(x => !filter.Contains(x)).ToArray();
    }

    private static string[] GetList(string path)
    {
      var info = new ProcessStartInfo
      {
        Arguments = $"-l {path}",
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        FileName = "ls"
      };
      var process = Process.Start(info);
      var text = process.StandardOutput.ReadToEnd();
      process.WaitForExit();


      var lines = text.Split(Splitter.LineBreaks, StringSplitOptions.RemoveEmptyEntries);
      return (from line in lines select line.Split(Splitter.TabAndSpace, StringSplitOptions.RemoveEmptyEntries) into split where split.Length >= 5 select split.Last().Replace(".cec6.gz", "").Replace(".cec6", "")).ToArray();
    }
  }
}
