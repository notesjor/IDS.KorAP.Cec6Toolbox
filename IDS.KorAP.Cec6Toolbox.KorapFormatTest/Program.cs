using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP;
using CorpusExplorer.Sdk.Model.Adapter.Corpus.Abstract;
using CorpusExplorer.Sdk.Utils.DocumentProcessing.Exporter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.KorAP.Cec6Toolbox.KorapFormatTest
{
  internal class Program
  {
    private static string _basePath = @"C:\Users\Jan\Desktop\CEKOR\Test-02\";

    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();
      /*
      var t01 = Import("T00.zip");
      Export(t01, "T01");
      var t02 = Import("T01.zip");
      Export(t02, "T02");
      var t03 = Import("T02.zip");

      if(!Directory.Exists(_basePath + "B"))
        Directory.CreateDirectory(_basePath + "B");

      Export(t03, @"B\T02");
      */
      var t01 = Import("ngaci.zip");
      Export(t01, "T01");

      var t02 = Import("T01.zip");
      Export(t02, "T02");

      var t03 = Import("T02.zip");
      if (!Directory.Exists(_basePath + "B"))
        Directory.CreateDirectory(_basePath + "B");
      Export(t03, @"B\T02");
    }

    private static void Export(AbstractCorpusAdapter corpus, string name)
    {
      corpus.Save(_basePath + name + ".cec6", false);
      (new ExporterKorap()).Export(corpus, _basePath + name + ".zip");
      File.Delete(_basePath + name + "..zip");
    }

    private static AbstractCorpusAdapter Import(string name)
    {
      var korap = new ImporterKorap2021();
      return korap.Execute(new[] { _basePath + name }).First();
    }
  }
}
