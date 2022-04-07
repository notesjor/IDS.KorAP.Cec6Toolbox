using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Extension;

namespace IDS.KorAP.Cec6Toolbox.CorpusSampler
{
  class Program
  {
    private static Random _rnd = new Random();

    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var input = args[0];
      var option = args[1];
      var size = long.Parse(args[2]);
      var output = args[3];

      switch (option)
      {
        case "token":
          SampleToken(input, size, output);
          break;
        case "tokenM":
          SampleToken(input, size * 1000000, output);
          break;
      }
    }

    private static void SampleToken(string input, long size, string output)
    {
      var files = new List<string>();
      files.AddRange(Directory.GetFiles(input, "*.cec6", SearchOption.AllDirectories));
      files.AddRange(Directory.GetFiles(input, "*.cec6.gz", SearchOption.AllDirectories));
      files.AddRange(Directory.GetFiles(input, "*.cec6.lz4", SearchOption.AllDirectories));

      Parallel.ForEach(files, file =>
      {
        var outputFile = Path.Combine(output, Path.GetFileName(file));
        if (File.Exists(outputFile))
          return;

        Console.WriteLine($"LOAD: {file}");

        var corpus = CorpusAdapterWriteDirect.Create(file);

        Console.WriteLine($"GET GUIDs: {file}");
        var guids = corpus.DocumentGuids.ToList();

        Console.WriteLine($"RAND: {file}");

        long cnt = 0;
        var sel = new List<Guid>();
        while (cnt < size && guids.Count > 0)
        {
          var rnds = GetRandomIndices(guids.Count, 1000);
          var breaks = false;

          foreach (var id in rnds)
          {
            var guid = guids[id];
            sel.Add(guid);
            guids.RemoveAt(id);

            cnt += corpus.GetDocumentLengthInWords(guid);
            if (cnt > size)
            {
              breaks = true;
              break;
            }
          }
          if (breaks)
            break;
        }

        Console.WriteLine($"SAVE: {file}");

        corpus.ToSelection().CreateTemporary(sel).ToCorpus().Save(outputFile, true);
      });
    }

    private static int[] GetRandomIndices(int count, int max)
    {
      var res = new int[max];
      for (var i = 0; i < max && count > 0; i++)
        res[i] = _rnd.Next(0, count--);
      return res;
    }
  }
}
