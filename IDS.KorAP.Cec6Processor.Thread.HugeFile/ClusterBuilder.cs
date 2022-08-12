using System.Text.RegularExpressions;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP.LoadStrategy;

namespace IDS.KorAP.Cec6Processor.Thread.HugeFile
{
  public class ClusterBuilder
  {
    private readonly int _size;

    public  ClusterBuilder(int size)
    {
      _size = size;
    }

    public string[][] BuildCluster(string path, out Dictionary<string, object> header)
    {
      using (var zip = new KorapZip(path))
      {
        var helper = new KorapScraper(); // Helper

        // -- siehe ScraperKorap --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> --> 
        var corpusRegex = new Regex(@"^[a-zA-Z0-9]*\/header\.xml$");
        var mainHeader = zip.Entries.Single(x => corpusRegex.IsMatch(x));

        if (string.IsNullOrWhiteSpace(mainHeader))
        {
          header = null;
          return null;
        }

        header = helper.GetZipHeader(path, zip, mainHeader);
        if (header == null)
          return null;

        var dataRegex = new Regex(@"^[a-zA-Z0-9]*\/[a-zA-Z0-9]*\/[a-zA-Z0-9]*\/data\.xml$");
        // <-- siehe ScraperKorap <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- <-- 
        return MakePartition(zip.Entries.Where(x => dataRegex.IsMatch(x)).ToArray());
      }
    }

    private string[][] MakePartition(string[] docs)
    {
      var res = new List<string[]>();
      var cur = new List<string>();

      foreach (var d in docs)
      {
        var doc = d.Replace("data.xml", "");
        if (doc.EndsWith("/"))
          doc = doc.Substring(0, doc.Length - 1);

        cur.Add(doc);
        if (cur.Count <= _size)
          continue;

        res.Add(cur.ToArray());
        cur.Clear();
      }
      if (cur.Count > 0)
      {
        res.Add(cur.ToArray());
        cur.Clear();
      }

      return res.ToArray();
    }
  }
}
