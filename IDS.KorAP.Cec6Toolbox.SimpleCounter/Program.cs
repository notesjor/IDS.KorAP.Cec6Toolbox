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
    private static int _idx;
    private static object _idxLock = new object();
    private static string _idxPath;
    private static string _killFlag = "stop.flag";
    private static byte[] _eol = new byte[] { (byte)'\n' };

    static void Main(params string[] arguments)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      if (File.Exists(_killFlag))
        File.Delete(_killFlag);

      var config = LoadConfig(arguments[0]);
      Console.WriteLine($"{arguments[0]}... loaded!");
      Console.WriteLine($"parallel: {config.Parallel}");
      Console.WriteLine($"input (subdir: {config.UseSubDirs}): {config.Cec6InputDir}");
      Console.WriteLine($"output: {config.PathOutput}");

      var parallel = config.Parallel;
      var dir = config.Cec6InputDir;
      var lst = new CorpuslistIndex(config.PathCorpuslist);
      var w2i = new EasyRocksDb(config.RDBWordlist + "/w2i", true);
      var i2w = new EasyRocksDb(config.RDBWordlist + "/i2w", true);
      _idxPath = config.RDBWordlist + "/idx";
      _idx = int.Parse(File.ReadAllText(_idxPath));
      var output = config.PathOutput;

      PrepareEnviroment(output, out var outputDirectory);

      if (config.UseSubDirs)
      {
        ProcessSubDirs(dir, parallel, lst, w2i, i2w, outputDirectory);
      }
      else
      {
        ProcessDirectory(dir, parallel, lst, w2i, i2w, outputDirectory);
      }
    }

    private static void ProcessSubDirs(string dir, int parallel, CorpuslistIndex lst, EasyRocksDb w2i, EasyRocksDb i2w, string outputDirectory)
    {
      Parallel.ForEach(Directory.GetDirectories(dir), new ParallelOptions { MaxDegreeOfParallelism = parallel },
                       directory =>
                       {
                         if (File.Exists(_killFlag))
                           return;

                         if (directory.EndsWith("/"))
                           directory = directory.Substring(0, directory.Length - 1);

                         var name = directory.Replace(Path.GetDirectoryName(directory), "").Replace("/", "");
                         var pathErr = Path.Combine(outputDirectory, name + ".err");

                         try
                         {
                           var cid = lst[directory];
                           if (File.Exists(Path.Combine(outputDirectory, name + ".dat")))
                             return;

                           var files = Directory.GetFiles(directory, "*.cec6.gz", SearchOption.AllDirectories);
                           var corpus = CorpusMerger.Merge(files, new ImporterCec6()) as CorpusAdapterWriteDirect;
                           var select = corpus.ToSelection();

                           var block = @select.CreateBlock<Frequency1LayerBlock>();
                           block.LayerDisplayname = "Wort";
                           block.Calculate();

                           Console.WriteLine($"{directory} [{cid:D6} - files: {files.Length}]...");

                           Process(w2i, i2w, block.Frequency, cid, outputDirectory, name);
                         }
                         catch (Exception ex)
                         {
                           File.AppendAllLines(pathErr, new[] { ex.Message, ex.StackTrace, "" }, Encoding.UTF8);
                         }
                       });
    }

    private static void ProcessDirectory(string dir, int parallel, CorpuslistIndex lst, EasyRocksDb w2i, EasyRocksDb i2w, string outputDirectory)
    {
      Parallel.ForEach(Directory.GetFiles(dir, "*.cec6.gz", SearchOption.TopDirectoryOnly),
                       new ParallelOptions { MaxDegreeOfParallelism = parallel },
                       file =>
                       {
                         if (File.Exists(_killFlag))
                           return;

                         var name = file.Replace(dir, "").Replace("/", "_-_").Replace(".cec6.gz", "");
                         var pathErr = Path.Combine(outputDirectory, name + ".err");

                         try
                         {
                           var cid = lst[file];
                           if (File.Exists(Path.Combine(outputDirectory, name + ".dat")))
                             return;

                           Console.WriteLine($"{file} [{cid:D6}]...");

                           var corpus = CorpusAdapterWriteDirect.Create(file);
                           var select = corpus.ToSelection();

                           var block = @select.CreateBlock<Frequency1LayerBlock>();
                           block.LayerDisplayname = "Wort";
                           block.Calculate();

                           Process(w2i, i2w, block.Frequency, cid, outputDirectory, name);
                         }
                         catch (Exception ex)
                         {
                           File.AppendAllLines(pathErr, new[] { ex.Message, ex.StackTrace, "" }, Encoding.UTF8);
                         }
                       });
    }

    private static void Process<T>(EasyRocksDb w2i, EasyRocksDb i2w, T data, int cid, string outputDirectory, string name)
    {
      var pathOut = Path.Combine(outputDirectory, name + ".dat");
      var pathErr = Path.Combine(outputDirectory, name + ".err");
      var pathSum = Path.Combine(outputDirectory, name + ".sum");

      var sum = 0.0;
      var unks = new Dictionary<string, int>();

      using (var fs = new FileStream(pathOut, FileMode.Create, FileAccess.Write))
      using (var buffer = new BufferedStream(fs))
        if (data is Dictionary<string, double> input)
        {
          Dictionary<string, string> keys;
          keys = w2i.GetBatch(input.Keys.ToArray()).ToDictionary(x => x.Key, x => x.Value);

          foreach (var f in input)
          {
            try
            {
              if (int.TryParse(keys[f.Key], out var tId))
                Write(buffer, cid, tId, (int)f.Value);
              else
                unks.Add(f.Key, (int)f.Value);
            }
            catch (Exception ex)
            {
              File.AppendAllLines(pathErr, new[] { ex.Message, ex.StackTrace, "" }, Encoding.UTF8);
            }
          }

          sum = input.Sum(x => x.Value);

          if (unks.Count > 0)
            lock (_idxLock)
            {
              var word2index = new Dictionary<string, string>();
              var index2word = new Dictionary<string, string>();

              foreach (var x in unks)
              {
                var tId = _idx;
                word2index.Add(x.Key, tId.ToString());
                index2word.Add(tId.ToString(), x.Key);

                Write(buffer, cid, tId, x.Value);

                _idx++;
              }

              w2i.PutBatch(word2index);
              i2w.PutBatch(index2word);

              File.WriteAllText(_idxPath, _idx.ToString());
            }
        }

      File.WriteAllText(pathSum, sum.ToString(), Encoding.ASCII);

      Console.WriteLine($"{name}... [len: {sum}]...ok!");
    }

    private static void Write(Stream writer, int cid, int tid, int freq)
    {
      var buffer = Combine(BitConverter.GetBytes(cid),
                           BitConverter.GetBytes(tid),
                           BitConverter.GetBytes(freq),
                           _eol);

      writer.Write(buffer, 0, buffer.Length);
    }

    private static byte[] Combine(params byte[][] arrays)
    {
      var res = new byte[arrays.Sum(a => a.Length)];
      var offset = 0;
      foreach (var array in arrays)
      {
        Buffer.BlockCopy(array, 0, res, offset, array.Length);
        offset += array.Length;
      }
      return res;
    }

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
