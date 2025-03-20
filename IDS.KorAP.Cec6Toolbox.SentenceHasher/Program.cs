using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using Newtonsoft.Json;
using CorpusExplorer.Sdk.Utils.CorpusManipulation;
using CorpusExplorer.Sdk.Model.Extension;

namespace IDS.KorAP.Cec6Toolbox.SentenceHasher
{
  internal class Program
  {
    private struct Item
    {
      public string File { get; set; }
      public Guid Guid { get; set; }
      public int SID { get; set; }
    }

    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var dir = args[0];

      var lod = new object();
      var dic = new Dictionary<string, List<Item>>();
      var ful = new Dictionary<string, string>();
      var don = 0;
      var fil = Directory.GetFiles(dir, "*.cec6");

      Parallel.ForEach(fil, new ParallelOptions { MaxDegreeOfParallelism = 20 }, file =>
      {
        var fn = Path.GetFileName(file);
        var corpus = CorpusAdapterWriteIndirect.Create(file);
        var layer = corpus.GetLayers("Wort").First();

        /* PREFILTER START */
        var prefilter = new HashSet<int>(new[] { "\"", "'", "«" }.Select(x => layer[x]).Where(x => x >= -1));
        if (prefilter.Count == 0)
          return;
        /* PREFILTER END */

        var sha = SHA512.Create();
        var loh = new object();

        Parallel.ForEach(layer.DocumentGuids, dsel =>
        {
          var doc = layer[dsel];
          for (var i = 0; i < doc.Length; i++)
          {
            byte[] data;
            using (var ms = new MemoryStream())
            {
              if (doc[i].Length < 10) // PREFILTER LENGTH
                continue;

              var pretest = false; // PREFILTER TEST
              for (var j = 0; j < doc[i].Length; j++)
              {
                ms.Write(BitConverter.GetBytes(doc[i][j]));
                if (prefilter.Contains(doc[i][j]))
                  pretest = true;
              }

              if (!pretest) // PREFILTER TEST
                continue;

              data = ms.ToArray();
              lock (loh)
                data = sha.ComputeHash(data);
            }

            var hash = Convert.ToBase64String(data);
            var item = new Item { Guid = dsel, SID = i, File = fn };

            lock (lod)
              if (dic.ContainsKey(hash))
              {
                if (dic[hash].Count == 1)
                  ful.Add(hash, string.Join(" ", doc[i].Select(x => layer[x])));
                if (dic[hash].Count == 2)
                {
                  if (ful[hash] != string.Join(" ", doc[i].Select(x => layer[x])))
                    Console.WriteLine("FULLHASH COLLISION: " + hash);
                }
                dic[hash].Add(item);
              }
              else
                dic.Add(hash, [item]);
          }
        });

        lock (lod)
        {
          don++;
          Console.WriteLine($"{don}/{fil.Length}");
        }

        sha.Dispose();
      });

      Console.WriteLine("WRITE OUTPUT");

      dic = dic.Where(x => x.Value.Count > 1).OrderByDescending(x => x.Value.Count).Take(1000).ToDictionary(x => x.Key, x => x.Value);
      //File.WriteAllText("result.json", JsonConvert.SerializeObject(dic));

      var output = "result";
      if (!Directory.Exists(output))
        Directory.CreateDirectory(output);

      File.WriteAllLines("result/fullhash.txt", ful.Select(x => x.Key + "\t" + x.Value));

      foreach (var x in dic)
      {
        var fn = Path.Combine(output, x.Key.Replace("/", "_") + ".cec6");
        var files = x.Value.Select(y => y.File).Distinct().ToArray();
        var merger = new CorpusMerger();

        CorpusAdapterWriteDirect corpus;
        foreach (var file in files)
        {
          corpus = CorpusAdapterWriteDirect.Create(Path.Combine(dir, file));
          var dsel = x.Value.Where(y => y.File == file).Select(y => y.Guid).ToArray();
          var select = corpus.ToSelection(dsel);

          merger.Input(select.ToCorpus());
        }

        merger.Execute();
        corpus = (CorpusAdapterWriteDirect)merger.Output.First();

        corpus.Save(fn);
      }

      Console.WriteLine("END!");
    }
  }
}
