using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorpusExplorer.Sdk.Ecosystem;
using IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature;

namespace IDS.KorAP.Cec6Toolbox.AdditionalTagger
{
  class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      if(args is not {Length: 3})
        (new FeatureHelp()).Execute(null, null, null);

      var options = args[1];
      var path = args[2];
      var files = GetFiles(path);

      switch (args[0])
      {
        case "app":
          (new FeatureAddAnnotationByApp()).Execute(options, path, files);
          break;
        case "korap":
          (new FeatureAddAnnotationFromKorap()).Execute(options, path, files);
          break;
        default:
          (new FeatureHelp()).Execute(null, null, null);
          break;
      }
    }

    private static string[] GetFiles(string path)
    {
      var res = new HashSet<string>();
      foreach (var file in Directory.GetFiles(path, "*.cec6"))
        res.Add(file);
      foreach (var file in Directory.GetFiles(path, "*.cec6.gz"))
        res.Add(file);
      return res.ToArray();
    }
  }
}
