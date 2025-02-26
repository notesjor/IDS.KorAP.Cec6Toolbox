using CorpusExplorer.Sdk.Diagnostic;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Utils.DocumentProcessing.Importer.CorpusExplorerV6;

namespace IDS.KorAP.Cec6Toolbox.CorpusMerger
{
  internal class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();
      InMemoryErrorConsole.OnException += (ex) =>
      {
        Console.WriteLine(ex.Message);
        Console.WriteLine(ex.StackTrace);
      };

      if (args.Length != 3)
      {
        Console.WriteLine("Usage: ./IDS.KorAP.Cec6Toolbox.CorpusMerger <mode:FILE/DIR> <input> <output>");
        return;
      }

      var mode = args[0];
      var input = args[1];
      var output = args[2];

      if (mode == "FILE")
      {
        MergeFiles(input, output);
      }
      else if (mode == "DIR")
      {
        ProcessSubDirs(input, output);
      }
      else
      {
        Console.WriteLine("Invalid mode: " + mode);
      }
    }

    private static void MergeFiles(string input, string output)
    {
      var merger = new CorpusExplorer.Sdk.Utils.CorpusManipulation.CorpusMerger();
      var mergeLock = new object();

      Parallel.ForEach(Directory.GetFiles(input, "*.cec6", SearchOption.TopDirectoryOnly),
        new ParallelOptions { MaxDegreeOfParallelism = 10 }, file =>
      {
        try
        {
          var corpus = CorpusAdapterWriteDirect.Create(file);
          lock (mergeLock)
            merger.Input(corpus);
        }
        catch
        {
          Console.WriteLine($"> file: {file}");
        }
      });

      merger.Execute();
      merger.Output.FirstOrDefault()?.Save(output, false);
    }

    private static void ProcessSubDirs(string input, string output)
    {
      if (!Directory.Exists(output))
        Directory.CreateDirectory(output);

      var directories = Directory.GetDirectories(input);
      Console.WriteLine($"{input}: {directories.Length} directories found!");
      Console.WriteLine($"merge files to {output}");
      Console.WriteLine("PRESS ENTER TO START");
      Console.ReadLine();

      foreach (var dir in directories)
      {
        Console.Write($"Merge: {dir}...");
        var path = Path.Combine(output, Path.GetFileName(dir) + ".cec6");

        var merger = new CorpusExplorer.Sdk.Utils.CorpusManipulation.CorpusMerger();
        foreach (var file in Directory.GetFiles(dir, "*.cec6", SearchOption.TopDirectoryOnly))
          try
          {
            merger.Input(CorpusAdapterWriteDirect.Create(file));
          }
          catch
          {
            Console.WriteLine($"> file: {file}");
          }
        merger.Execute();
        merger.Output.FirstOrDefault()?.Save(path, false);

        Console.WriteLine($" ok!");

        GC.Collect();
      }
    }
  }
}