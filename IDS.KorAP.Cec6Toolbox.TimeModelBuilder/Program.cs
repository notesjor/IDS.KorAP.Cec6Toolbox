using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Action.Helper;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Extension;
using CorpusExplorer.Sdk.Utils.Filter;
using CorpusExplorer.Sdk.Utils.Filter.Queries;

namespace IDS.KorAP.Cec6Toolbox.TimeModelBuilder
{
  class Program
  {
    static void Main(string[] args)
    {
      var outDir = "/home/ruediger/CEC6-TIME";
      if (!Directory.Exists(outDir))
        Directory.CreateDirectory(outDir);
      var chkDir = Path.Combine(outDir, "check");
      if (!Directory.Exists(chkDir))
        Directory.CreateDirectory(chkDir);

      CorpusExplorerEcosystem.InitializeMinimal();
      var flock = new object();

      Parallel.ForEach(Directory.GetFiles(args.Length == 0 ? "/home/ruediger/CEC6" : args[0], "*.cec6.gz"), path =>
      {
        var name = Path.GetFileName(path);
        var chkPath = Path.Combine(chkDir, name);
        if (File.Exists(chkPath))
        {
          Console.WriteLine($"{name}...already done!");
          return;
        }

        try
        {
          var corpus = CorpusAdapterWriteDirect.Create(path);
          var selections = UnsupportedQueryParserFeatureHelper.Handle(corpus.ToSelection(),
                                                                      (FilterQueryUnsupportedParserFeature) QueryParser
                                                                       .Parse("XSDatum::DATE;YMD"));
          foreach (var selection in selections)
          {
            var outPath = Path.Combine(outDir, selection.Displayname);
            lock (flock)
              if (!Directory.Exists(outPath))
                Directory.CreateDirectory(outPath);

            selection.ToCorpus().Save(Path.Combine(outPath, name), true);
          }
          File.Create(chkPath).Close();
          Console.WriteLine($"{name}...ok!");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"{name}...error!");
          try
          {
            File.AppendAllText("error.log", $"{ex.Message}\r\n---\r\n{ex.StackTrace}\r\n###\r\n", Encoding.UTF8);
          }
          catch
          {
            // ignore
          }
        }
      });
    }
  }
}
