using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Ecosystem.Model;
using CorpusExplorer.Sdk.Helper;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Utils.CorpusManipulation;
using CorpusExplorer.Sdk.Utils.DocumentProcessing.Builder;

namespace IDS.KorAP.Cec6Processor.Thread.HugeFile
{
  internal class Program
  {
    public static bool ImportCoreNlp { get; set; } = false;
    public static bool ImportMalt { get; set; } = false;
    public static bool ImportMarmot { get; set; } = false;
    public static bool ImportOpenNlp { get; set; } = false;
    public static bool ImportTreeTagger { get; set; } = true;
    public static int SIZE { get; set; } = 100000;

    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      if (args.Length == 1)
        MainProcess(args[0]);
      else if (args.Length == 4)
        SubProcess(args[0], args[1], args[2], args[3]);
    }

    private static void MainProcess(string path)
    {
      var clusters = GetClusters(path, out var header);
      if (clusters == null)
        return;
      GC.Collect();

      using (var dir = new TemporaryDirectory())
      {
        var head = Guid.NewGuid();
        var headFile = Path.Combine(dir.Path, $"{head:N}.head");
        Serialize(header, headFile);

        var files = new List<string>();

        Parallel.ForEach(clusters, new ParallelOptions { MaxDegreeOfParallelism = 4 }, cluster =>
        {
          var session = Path.Combine(dir.Path, $"{Guid.NewGuid():N}.txt");
          File.WriteAllLines(session, cluster);

          var output = Path.Combine(dir.Path, $"{Guid.NewGuid():N}.cec6.lz4");
          files.Add(output);

#if DEBUG
          SubProcess(path, headFile, session, output);
#else
          var process = Process.Start("/home/ruediger/TOOLBOX/PROCESSOR/IDS.KorAP.Cec6Processor.Thread.HugeFile", new[] { path, headFile, session, output });
          process.WaitForExit();
#endif
        });

        var merger = new CorpusMerger();
        foreach (var file in files)
          merger.Input(CorpusAdapterWriteDirect.Create(file));

        merger.Execute();
        #if DEBUG
        merger.Output.First().Save(Path.Combine("corpus.cec6.lz4"));
        #else
        merger.Output.First().Save(Path.Combine($"/home/ruediger/CEC6/{Path.GetFileNameWithoutExtension(path)}.cec6.lz4"));
        #endif
      }
    }

    private static string[][] GetClusters(string path, out Dictionary<string, object> header)
    {
      var builder = new ClusterBuilder(SIZE);
      return builder.BuildCluster(path, out header);
    }

    private static void SubProcess(string path, string head, string list, string output)
    {
      var importer = new ImporterKorapStream(Deserialize(head), list)
      {
        ImportCoreNlp = ImportCoreNlp,
        ImportMalt = ImportMalt,
        ImportMarmot = ImportMarmot,
        ImportOpenNlp = ImportOpenNlp,
        ImportTreeTagger = ImportTreeTagger,
        ReadTaxonomy = false
      };
      importer.Execute(new[] { path }).FirstOrDefault().Save(output);
    }

    private static void Serialize(Dictionary<string, object> data, string path)
    {
      using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
      {
        var bf = new BinaryFormatter();
        bf.Serialize(fs, data);
      }
    }

    private static Dictionary<string, object> Deserialize(string path)
    {
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
      {
        var bf = new BinaryFormatter();
        return bf.Deserialize(fs) as Dictionary<string, object>;
      }
    }
  }
}