using System.Diagnostics;
using CorpusExplorer.Sdk.Helper;

namespace IDS.KorAP.Cec6Processor.Thread.HugeFile
{
  internal class Program
  {
    public static bool ImportCoreNlp { get; set; } = false;
    public static bool ImportMalt { get; set; } = false;
    public static bool ImportMarmot { get; set; } = false;
    public static bool ImportOpenNlp { get; set; } = false;
    public static bool ImportTreeTagger { get; set; } = true;

    static void Main(string[] args)
    {
      if (args.Length == 1)
        MainProcess(args[0]);
      else if (args.Length == 4)
        SubProcess(args[0], args[1], args[2], args[3]);
    }

    private static void MainProcess(string path)
    {
      ClusterBuilder.SIZE = 100000;
      var clusters = ClusterBuilder.BuildCluster(path, out var header);
      if (clusters == null)
        return;
      
      using (var dir = new TemporaryDirectory())
      {
        var headFile = new TemporaryFile(dir.Path, ".head");
        Serializer.Serialize(header, headFile.Path, false);

        Parallel.ForEach(clusters, new ParallelOptions { MaxDegreeOfParallelism = 4 }, cluster =>
        {
          var session = new TemporaryFile(dir.Path);
          File.WriteAllLines(session.Path, cluster);

          var output = new TemporaryFile(dir.Path, ".cec6.lz4");

          var process = Process.Start("/home/ruediger/TOOLBOX/PROCESSOR/IDS.KorAP.Cec6Processor.Thread.HugeFile", new[] { path, headFile.Path, session.Path, output.Path });
          process.WaitForExit();
        });
      }
    }

    private static void SubProcess(string path, string head, string list, string output)
    {
      var importer = new ImporterKorapStream(Serializer.Deserialize<Dictionary<string, object>>(head), list)
      {
        ImportCoreNlp = ImportCoreNlp,
        ImportMalt = ImportMalt,
        ImportMarmot = ImportMarmot,
        ImportOpenNlp = ImportOpenNlp,
        ImportTreeTagger = ImportTreeTagger,
        ReadTaxonomy = false
      };
      importer.Execute(new[] { path }).FirstOrDefault()?.Save(output);
    }
  }
}