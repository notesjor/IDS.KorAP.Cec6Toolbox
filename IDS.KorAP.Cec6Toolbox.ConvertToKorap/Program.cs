using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Ecosystem.Model;
using CorpusExplorer.Sdk.Extern.Xml.Ids.I5Xml;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;

namespace IDS.KorAP.Cec6Toolbox.ConvertToKorap
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var input = args[0];
      var foundry = args[1];
      var output = args[2];

      CorpusExplorerEcosystem.InitializeMinimal();

      var files = Directory.GetFiles(input, "*.cec6");
      var cnt = 0;
      var cntLock = new object();

      Parallel.ForEach(files, Configuration.ParallelOptions, file =>
      {
        lock (cntLock)
          Console.WriteLine($"Processing {++cnt}/{files.Length}: {file}");

        try
        {
          var exporterKorap = new ExporterKorap { ReplaceCorpusExplorerFoundry = foundry };
          var exporterI5 = new ExporterI5();

          var corpus = CorpusAdapterWriteDirect.Create(file);
          exporterKorap.Export(corpus, Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".zip"));
          exporterI5.Export(corpus, Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".i5.xml"));
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error processing {file}: {ex.Message}");
        }
      });
    }
  }
}
