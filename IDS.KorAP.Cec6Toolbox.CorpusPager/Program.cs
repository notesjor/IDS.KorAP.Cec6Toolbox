using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Ecosystem.Model;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Extension;

namespace IDS.KorAP.Cec6Toolbox.CorpusPager
{
  internal class Program
  {
    static void Main(string[] args)
    {
      if (args.Length != 3)
      {
        Console.WriteLine("Usage: IDS.KorAP.Cec6Toolbox.CorpusPager <input> <size> <output>");
        return;
      }

      CorpusExplorerEcosystem.InitializeMinimal();

      var input = args[0];
      var size = int.Parse(args[1]) * 1024 * 1024;
      var output = args[2];

      if (!Directory.Exists(output))
        Directory.CreateDirectory(output);

      foreach (var file in Directory.GetFiles(input, "*.cec6", SearchOption.TopDirectoryOnly))
      {
        Console.Write($"{file}...");
        var fileName = Path.GetFileName(file);
        var fileSize = new FileInfo(file).Length;

        if (fileSize <= size)
        {
          File.Copy(file, Path.Combine(output, fileName));
          Console.WriteLine("ok!");
          continue;
        }

        fileName = Path.GetFileNameWithoutExtension(file);

        var parts = (int)(fileSize / size) + 1;
        var corpus = CorpusAdapterWriteDirect.Create(file);

        var tokenSize = corpus.CountToken;
        var tokenPage = (int)(tokenSize / parts);
        var docs = corpus.DocumentGuids.ToList();

        var currentPage = 1;
        var currentSize = 0;
        var currentDocs = new List<Guid>();

        while (docs.Count > 0 && tokenSize >= tokenPage)
        {
          var idx = Configuration.Random.Next(0, docs.Count);
          var doc = docs[idx];
          docs.RemoveAt(idx);

          currentDocs.Add(doc);
          currentSize += (int)corpus.GetDocumentLengthInWords(doc);

          if (currentSize >= tokenPage)
          {
            WritePage(corpus, currentDocs, output, fileName, currentPage++);
            tokenSize -= currentSize;
            currentSize = 0;
            currentDocs.Clear();
          }
        }

        if (currentDocs.Count > 0)
          WritePage(corpus, currentDocs, output, fileName, currentPage++);

        if (docs.Count > 0)
          WritePage(corpus, docs, output, fileName, currentPage);

        Console.WriteLine("ok!");
      }
    }

    private static void WritePage(CorpusAdapterWriteDirect corpus, List<Guid> currentDocs, string output, string fileName, int currentPage)
    {
      var sub = corpus.ToSelection().CreateTemporary(currentDocs).ToCorpus();
      sub.Save(Path.Combine(output, $"{fileName}_{currentPage:D2}.cec6"), false);
    }
  }
}