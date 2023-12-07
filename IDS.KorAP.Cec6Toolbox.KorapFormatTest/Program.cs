using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP;
using CorpusExplorer.Sdk.Model.Adapter.Corpus.Abstract;
using CorpusExplorer.Sdk.Utils.DocumentProcessing.Exporter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.KorAP.Cec6Toolbox.KorapFormatTest
{
  internal class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

    }

    private static void Export(AbstractCorpusAdapter corpus, string path)
    {
      var exporter = new ExporterKorap();
      exporter.Export(corpus, path);
    }

    private static AbstractCorpusAdapter Import(string path)
    {

    }

    private static void ToText(AbstractCorpusAdapter corpus, string path)
    {

    }
  }
}
