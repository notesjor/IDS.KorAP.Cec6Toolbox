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
using CorpusExplorer.Sdk.Model;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Extension;
using CorpusExplorer.Sdk.Utils.CorpusManipulation;
using CorpusExplorer.Sdk.Utils.DocumentProcessing.Importer.CorpusExplorerV6;
using IDS.KorAP.Cec6Toolbox.Corpuslist2Index;
using IDS.KorAP.Cec6Toolbox.SimpleCounter.Config;
using Newtonsoft.Json;

namespace IDS.KorAP.Cec6Toolbox.SimpleCounter
{
  class Program
  {
    private static object _lockOutputFile = new object();
    private static object _lockErrorFile = new object();

    static void Main(params string[] arguments)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var config = LoadConfig(arguments[0]);
      Console.WriteLine($"{arguments[0]}... loaded!");
      Console.WriteLine($"parallel: {config.Parallel}");
      Console.WriteLine($"input (subdir: {config.UseSubDirs}): {config.Cec6InputDir}");
      Console.WriteLine($"output: {config.PathOutput}");

      var parallel = config.Parallel;
      var dir = config.Cec6InputDir;
      var lst = new CorpuslistIndex(config.PathCorpuslist);
      var idx = new EasyRocksDb(config.RDBWordlist, false);
      var output = config.PathOutput;

      PrepareEnviroment(output, out var outputC);
      var outputFile = Path.Combine(output, "frequency.dat");
      var errorFile = Path.Combine(output, "error.log");

      if (config.UseSubDirs)
      {
        ProcessSubDirs(dir, parallel, lst, idx, outputC, outputFile, errorFile);
      }
      else
      {
        ProcessDirectory(dir, parallel, lst, idx, outputC, outputFile, errorFile);
      }
    }

    private static void ProcessSubDirs(string dir, int parallel, CorpuslistIndex lst, EasyRocksDb idx, string outputC, string outputFile, string errorFile)
    {
      Parallel.ForEach(Directory.GetDirectories(dir), new ParallelOptions { MaxDegreeOfParallelism = parallel },
                       directory =>
                       {
                         try
                         {
                           if (directory.EndsWith("/"))
                             directory = directory.Substring(0, directory.Length - 1);

                           var cid = lst[directory];
                           var cname = directory.Replace(Path.GetDirectoryName(directory), "").Replace("/", "") + ".freq";
                           var outC = Path.Combine(outputC, cname);
                           if (File.Exists(outC))
                             return;

                           var files = Directory.GetFiles(directory, "*.cec6.gz", SearchOption.AllDirectories);
                           var corpus = CorpusMerger.Merge(files, new ImporterCec6()) as CorpusAdapterWriteDirect;
                           var select = corpus.ToSelection();

                           var block = @select.CreateBlock<Frequency1LayerBlock>();
                           block.LayerDisplayname = "Wort";
                           block.Calculate();

                           Console.WriteLine($"{directory} [{cid:D6} - files: {files.Length}]...");

                           Process(idx, outputFile, errorFile, block.Frequency, cid, outC, corpus, cname);
                         }
                         catch (Exception ex)
                         {
                           lock (_lockErrorFile)
                             File.AppendAllLines(errorFile, new[] { ex.Message, ex.StackTrace, "" }, Encoding.UTF8);
                         }
                       });
    }

    private static void ProcessDirectory(string dir, int parallel, CorpuslistIndex lst, EasyRocksDb idx, string outputC, string outputFile, string errorFile)
    {
      Parallel.ForEach(Directory.GetFiles(dir, "*.cec6.gz", SearchOption.TopDirectoryOnly),
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

                           Console.WriteLine($"{file} [{cid:D6}]...");

                           var corpus = CorpusAdapterWriteDirect.Create(file);
                           var select = corpus.ToSelection();

                           var block = @select.CreateBlock<Frequency1LayerBlock>();
                           block.LayerDisplayname = "Wort";
                           block.Calculate();

                           Process(idx, outputFile, errorFile, block.Frequency, cid, outC, corpus, file);
                         }
                         catch (Exception ex)
                         {
                           lock (_lockErrorFile)
                             File.AppendAllLines(errorFile, new[] { ex.Message, ex.StackTrace, "" }, Encoding.UTF8);
                         }
                       });
    }

    private static object idxLock = new object();

    private static void Process<T>(EasyRocksDb idx, string outputFile, string errorFile,
                                T data, int cid, string outC, CorpusAdapterWriteDirect corpus, string label)
    {
      var res = new StringBuilder();

      if (data is Dictionary<string, double> input)
      {
        Dictionary<string, string> keys;
        lock (idxLock)
          keys = idx.GetBatch(input.Keys.ToArray()).ToDictionary(x => x.Key, x => x.Value);

        foreach (var f in input)
        {
          try
          {
            if (keys.ContainsKey(f.Key))
            {
              if (int.TryParse(keys[f.Key], out var tId))
              {
                // var secC = Convert(cid);
                // var secT = Convert(tId);
                // var secF = Convert((int)f.Value);

                res.Append($"{Convert(cid)}{Convert(tId)}{Convert((int)f.Value)}\n");
              }
              else
              {
                Console.WriteLine($"Cannot parse {f.Key} - {keys[f.Key]}");
                lock (_lockErrorFile)
                  File.AppendAllLines(errorFile + ".lst", new[] { f.Key }, Encoding.UTF8);
              }
            }
            else
            {
              Console.WriteLine($"{f.Key} not found in dict.");
              lock (_lockErrorFile)
                File.AppendAllLines(errorFile + ".lst", new[] { f.Key }, Encoding.UTF8);
            }
          }
          catch (Exception ex)
          {
            lock (_lockErrorFile)
              File.AppendAllLines(errorFile, new[] { ex.Message, ex.StackTrace, "" }, Encoding.UTF8);
          }
        }
      }

      lock (_lockOutputFile)
        File.AppendAllText(outputFile, res.ToString(), Encoding.ASCII);

      File.WriteAllText(outC, corpus.CountToken.ToString(), Encoding.ASCII);

      Console.WriteLine($"{label}... [len: {res.Length}]...ok!");
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
