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
          var name = GetFileName(corpus.GetDocumentMetadata(doc));

          File.WriteAllText(Path.Combine(dir, $"{name.Replace("/", "_")}.json"), JsonConvert.SerializeObject(json));
        }
      }
    }

    private static string GetFileName(Dictionary<string, object> dictionary)
    {
      if (dictionary.ContainsKey("Sigle"))
        return dictionary["Sigle"]?.ToString();
      if (dictionary.ContainsKey("Titel"))
        return dictionary["Titel"]?.ToString();      
      if (dictionary.ContainsKey("Firma") && dictionary.ContainsKey("Jahr"))
        return $"{dictionary["Firma"]}_{dictionary["Jahr"]}";
      if (dictionary.ContainsKey("GUID"))
        return dictionary["GUID"]?.ToString();
      return "---ERROR---";
    }
  }
}