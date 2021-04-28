using System;
using System.IO;
using System.Linq;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Utils.CorpusManipulation;

namespace IDS.KorAP.Cec6Toolbox.CustomCorpusCompiler
{
  class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var lines = File.ReadAllLines(args[0]);
      EstimateFileSize(lines);

      Console.Write("Start CCC (y/n)?: ");
      if (Console.ReadLine() != "y")
        return;

      CompileCustomCorpus(lines);
    }

    private static void CompileCustomCorpus(string[] lines)
    {
      Console.Write("New corpus name: ");
      var name = Console.ReadLine();
      var outPath = $"/home/ruediger/{name}.cec6.gz";
      Console.WriteLine($"> {outPath}");

      var merger = new CorpusMerger();
      foreach (var line in lines)
      {
        var path = LineToPath(line);
        Console.Write($"{path}...load...");
        using (var corpus = CorpusAdapterWriteDirect.Create(path))
        {
          Console.Write("merge...");
          merger.Input(corpus);
          Console.WriteLine("ok!");
        }
      }

      Console.Write("Recompile corpus...");
      merger.Execute();
      Console.WriteLine("ok!");

      merger.Output.First().Save(outPath, true);
    }

    private static void EstimateFileSize(string[] lines)
    {
      long sum = 0;

      foreach (var line in lines)
      {
        var path = LineToPath(line);
        if (File.Exists(path))
          sum += new FileInfo(path).Length;
        else
          Console.WriteLine($"{path}... not found!");
      }

      Console.WriteLine($"Estimated file size: {Math.Round((sum / 1000.0 / 1000.0), 2)} MB");
    }

    private static string LineToPath(string line)
      => $"/home/ruediger/CEC6/{line.Trim().ToLower()}.cec6.gz";
  }
}
