using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Helper;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using Newtonsoft.Json;

namespace IDS.KorAP.Cec6Toolbox.ConvertEveryDocumentToJson
{
  internal class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var dir = args[0];
      foreach (var file in Directory.GetFiles(dir, "*.cec6", SearchOption.TopDirectoryOnly))
      {
        var corpus = CorpusAdapterWriteDirect.Create(file);
        var docs = corpus.DocumentGuids;

        foreach (var doc in docs)
        {
          var json = corpus.GetReadableDocument(doc, "Wort").ReduceDocumentToStreamDocument().ToArray();
          var sigle = corpus.GetDocumentMetadata(doc)["Sigle"]?.ToString();

          File.WriteAllText(Path.Combine(dir, $"{sigle.Replace("/", "_")}.json"), JsonConvert.SerializeObject(json));
        }
      }
    }
  }
}