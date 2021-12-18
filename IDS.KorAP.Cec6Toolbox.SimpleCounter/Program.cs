using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Blocks;
using CorpusExplorer.Sdk.Db.RocksDb;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Extension;
using IDS.KorAP.Cec6Toolbox.Corpuslist2Index;
using IDS.KorAP.Cec6Toolbox.SimpleCounter.Config;
using Newtonsoft.Json;

namespace IDS.KorAP.Cec6Toolbox.SimpleCounter
{
  class Program
  {
    static void Main(params string[] arguments)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var config = LoadConfig(arguments[0]);

      var parallel = config.Parallel;
      var dir = config.Cec6InputDir;
      var lst = new CorpuslistIndex(config.PathCorpuslist);
      var idx = new EasyRocksDb(config.RDBWordlist, false);
      var output = config.PathOutput;

      PrepareEnviroment(output,  out var outputC);
      var outputFile = Path.Combine(output, "frequency.dat");
      var errorFile = Path.Combine(output, "error.log");
      var outputFileLock = new object();

      Parallel.ForEach(Directory.GetFiles(dir, "*.cec6.gz", SearchOption.AllDirectories),
                       new ParallelOptions { MaxDegreeOfParallelism = parallel },
                       file =>
                       {
                         try
                         {
                           var cid = lst[file];
                           var cname = file.Replace(dir, "").Replace("/", "_-_").Replace(".cec6.gz", ".freq");
                           var outC = Path.Combine(outputC, cname);
                           if (File.Exists(outC))
                             return;

                           Console.WriteLine($"{file} [{cid:D5}]...");

                           var corpus = CorpusAdapterWriteDirect.Create(file);
                           var select = corpus.ToSelection();

                           var block = select.CreateBlock<Frequency1LayerBlock>();
                           block.LayerDisplayname = "Wort";
                           block.Calculate();

                           var res = new ConcurrentBag<string>();

                           Parallel.ForEach(block.Frequency, f =>
                           {
                             try
                             {
                               var tId = int.Parse(idx.Get(f.Key));

                               var secC = Convert(cid);
                               var secT = Convert(tId);
                               var secF = Convert((int)f.Value);

                               res.Add($"{secC}{secT}{secF}");
                             }
                             catch
                             {
                               Console.WriteLine($"{f.Key} not found");
                             }
                           });

                           lock (outputFileLock)
                             File.AppendAllLines(outputFile, res, Encoding.ASCII);

                           File.WriteAllText(outC, corpus.CountToken.ToString(), Encoding.ASCII);

                           Console.WriteLine($"{file}...ok!");
                         }
                         catch (Exception ex)
                         {
                           lock (outputFileLock)
                             File.AppendAllLines(errorFile, new []{ex.Message, ex.StackTrace, ""}, Encoding.UTF8);
                         }
                       });
    }

    private static string Convert(int val) 
      => string.Join("", BitConverter.GetBytes(val).Select(System.Convert.ToChar));

    private static ConfigModel LoadConfig(string path)
    {
      try
      {
        return JsonConvert.DeserializeObject<ConfigModel>(File.ReadAllText(path));
      }
      catch
      {
        var res = new ConfigModel();
        File.WriteAllText(path, JsonConvert.SerializeObject(res));
        return res;
      }
    }

    private static void PrepareEnviroment(string output, out string outputC)
    {
      outputC = Path.Combine(output, "corpora");
      if (!Directory.Exists(outputC))
        Directory.CreateDirectory(outputC);
    }
  }
}
