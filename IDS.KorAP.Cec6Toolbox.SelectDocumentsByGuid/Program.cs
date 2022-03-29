using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Helper;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Extension;

namespace IDS.KorAP.Cec6Toolbox.SelectDocumentsByGuid
{
  class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      if (args.Length != 3)
      {
        Help();
        return;
      }

      var inputFiles = Directory.GetFiles(args[0], "*.cec6").ToList();
      inputFiles.AddRange(Directory.GetFiles(args[0], "*.cec6.gz"));
      Console.WriteLine($"{inputFiles.Count} corpora selected!");

      var queries = new HashSet<Guid>(File.ReadAllLines(args[1], Encoding.UTF8).Where(x => !string.IsNullOrWhiteSpace(x)).Select(Guid.Parse));
      Console.WriteLine($"{queries.Count} queries");

      var dir = args[2];
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      foreach (var cec6 in inputFiles)
      {
        var corpus = CorpusAdapterWriteDirect.Create(cec6);

        var select = (from m
                        in corpus.DocumentGuids
                      where queries.Contains(m)
                      select m).ToArray();

        Console.WriteLine($"{cec6}... found: {select.Length} docs");

        if (select.Length > 0)
        {
          foreach (var dsel in select)
          {
            var text = corpus.GetReadableDocument(dsel, "Wort").ReduceDocumentToText();
            var sigle = corpus.GetDocumentMetadata(dsel, "Sigle", "").Replace("/", "_");
            if (sigle == "")
              sigle = $"{dsel:N}";

            File.WriteAllText(Path.Combine(dir, $"{sigle}.txt"), text, Encoding.UTF8);
          }

          var selection = corpus.ToSelection().CreateTemporary(select);
          selection.ToCorpus().Save(Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(cec6).Replace(".cec6", "")}_selected.cec6.gz"));
        }

        corpus.Dispose();
      }
    }

    private static void Help()
    {
      Console.WriteLine("./byGuid [DIR-INPUT] [QUERY-FILE] [OUTPUT]");
      Console.WriteLine("[DIR-INPUT]\t-\tInput directory (*.cec6.gz / *.cec6)");
      Console.WriteLine("[QUERY-FILE]\t-\tQuery-File - 1 GUID per Line");
      Console.WriteLine("[OUTPUT]\t-\tOutput directory");
    }
  }
}
