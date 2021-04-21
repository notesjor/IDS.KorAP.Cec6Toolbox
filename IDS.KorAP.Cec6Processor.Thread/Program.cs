using System;
using System.Linq;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP;

namespace IDS.KorAP.Cec6Processor.Thread
{
  class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var importer = new ImporterKorapInMemory();
      var corpus = importer.Execute(new[] { args[0] }).FirstOrDefault();
      corpus?.Save(args[1], true);
      if (corpus is IDisposable c)
        c.Dispose();
      importer.Dispose();
    }
  }
}
