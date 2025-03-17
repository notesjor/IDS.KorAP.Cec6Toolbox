using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using Newtonsoft.Json;

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

      Parallel.ForEach(Directory.GetFiles(dir, "*.cec6"), new ParallelOptions { MaxDegreeOfParallelism = 20 }, file =>
      {
        var fn = Path.GetFileName(file);
        var corpus = CorpusAdapterWriteIndirect.Create(file);
        var layer = corpus.GetLayers("Wort").First();

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
              for (var j = 0; j < doc[i].Length; j++)
                ms.Write(BitConverter.GetBytes(doc[i][j]));
              data = ms.ToArray();
              lock (loh)
                data = sha.ComputeHash(data);
            }

            var hash = Convert.ToBase64String(data);
            var item = new Item { Guid = dsel, SID = i, File = fn };

            lock (lod)
              if (dic.ContainsKey(hash))
                dic[hash].Add(item);
              else
                dic.Add(hash, [item]);
          }
        });

        sha.Dispose();
      });

      dic = dic.OrderByDescending(x => x.Value.Count).Take(1000).ToDictionary(x => x.Key, x => x.Value);
      File.WriteAllText("result.json", JsonConvert.SerializeObject(dic));
    }
  }
}
