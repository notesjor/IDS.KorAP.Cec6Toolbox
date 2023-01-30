using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using CorpusExplorer.Sdk.Db.RocksDb;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;

namespace IDS.KorAP.Cec6Toolbox.WordlistToolbox;

internal class Program
{
  private static readonly Encoding _utf8WithoutBom = new UTF8Encoding(false);

  /// <summary>
  ///   - Keine Leerzeichen im Token \s
  ///   - Keine Tokens mit mehr als 3 Zeichen aus dem Set + - # ? _ / \ : - z. B. +++ oder :// [https://]
  ///   - Keine Tokens mit &lt; am Anfang oder &gt; am Ende
  /// </summary>
  private static readonly Regex _wordlistCleanFunctionRegex = new(@"\s|[\+\-#\?_\/\:\\]{3,}|^\<|\>$");

  private static void Main()
  {
    CorpusExplorerEcosystem.InitializeMinimal();

    var key = ShowOptions();

    while (true)
    {
      switch (key)
      {
        case "1":
          WordlistExtract();
          break;
        case "2":
          WordlistClean();
          break;
        case "3":
          WordlistTo_W2I();
          break;
        case "4":
          WordlistTo_W2I_and_WI2();
          break;
        case "Q":
        case "q":
          return;
        default:
          Console.WriteLine("Invalid option.");
          break;
      }

      Console.WriteLine("DONE");
      key = ShowOptions();
    }
  }

  private static string ShowOptions()
  {
    Console.WriteLine("Options:");
    Console.WriteLine("1) Extract Wordlist");
    Console.WriteLine("2) Clean Wordlist");
    Console.WriteLine("3) Wordlist to RocksDB (W2I only)");
    Console.WriteLine("4) Wordlist to RocksDB (W2I and WI2)");
    Console.WriteLine("");
    Console.WriteLine("Q) QUIT");
    return Input("Select option (1-5 / Q)");
  }

  private static string Input(string ask)
  {
    Console.Write($"{ask}: ");
    return Console.ReadLine();
  }

  private static void WordlistExtract()
  {
    var layer = Input("Layer");
    var pathInput = Input("Path (input)");
    var parallelMax = int.Parse(Input("Threads"));
    var pathOutput = Input("Path (output)");

    var pathOutputDir = pathOutput;
    if (!Directory.Exists(pathOutputDir))
      Directory.CreateDirectory(pathOutputDir);

    var files = Directory.GetFiles(pathInput, "*.cec6", SearchOption.AllDirectories);
    var fCount = 0;
    var fCountLock = new object();

    var error = pathOutput + ".error";
    var gcCount = 0;
    var gcLock = new object();

    // Erstelle Listen
    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = parallelMax }, file =>
    {
      try
      {
        var fn = Path.Combine(pathOutputDir, Path.GetFileName(file) + ".lst");
        if (File.Exists(fn) && new FileInfo(fn).Length > 0)
          return;
        var ts = DateTime.Now;

        var corpus = CorpusAdapterWriteIndirect.Create(file);
        File.WriteAllLines(fn, corpus.GetLayers(layer).Single().Values, _utf8WithoutBom);
        lock (fCountLock)
        {
          Console.WriteLine($"{++fCount:D5}/{files.Length}: {file} (in {(DateTime.Now - ts).Seconds}s)");
        }

        corpus.Dispose();
      }
      catch (Exception ex)
      {
        lock (fCountLock)
        {
          File.AppendAllLines(error, new[] { ex.Message, ex.StackTrace }, _utf8WithoutBom);
        }
      }

      lock (gcLock)
      {
        gcCount++;
        if (gcCount < 100)
          return;

        gcCount = 0;
        GC.Collect();
      }
    });
  }

  private static void WordlistClean()
  {
    var pathInput = Input("Path (input)");
    var parallelMax = int.Parse(Input("Threads (Q1 = x/10 / Q2 = 10)"));
    var pathOutput = Input("Path (output)");

    var files = Directory.GetFiles(pathInput, "*.lst", SearchOption.AllDirectories);
    var fCount = 0;
    var fCountLock = new object();

    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = parallelMax / 10 }, file =>
    {
      var outputFile = Path.Combine(pathOutput, Path.GetFileName(file));
      try
      {
        var ts = DateTime.Now;
        var max = 10000;

        using (var fsIn = new FileStream(file, FileMode.Open, FileAccess.Read))
        using (var bufferIn = new BufferedStream(fsIn, 4096))
        using (var reader = new StreamReader(bufferIn, _utf8WithoutBom))
        {
          var aList = new List<string>(max);
          do
          {
            var line = reader.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
              if (reader.EndOfStream)
                break;
              continue;
            }

            aList.Add(line);
            if (aList.Count < max)
              continue;

            WordlistClean_Add(aList, pathOutput);
          } while (!reader.EndOfStream);

          if(aList.Count > 0)
            WordlistClean_Add(aList, pathOutput);
        }

        lock (fCountLock)
        {
          Console.WriteLine($"{++fCount:D4}/{files.Length:D4} (in {(DateTime.Now - ts).Seconds}s)");
        }
      }
      catch
      {
        File.Copy(file, outputFile);
      }
    });
  }

  private static void WordlistClean_Add(List<string> aList, string pathOutput)
  {
    var bList = new ConcurrentBag<string>();
    Parallel.ForEach(aList, new ParallelOptions { MaxDegreeOfParallelism = 10 }, x =>
    {
      if (WordlistCleanFunction(x))
        bList.Add(x);
    });

    File.AppendAllLines(pathOutput, bList, _utf8WithoutBom);
  }

  private static bool WordlistCleanFunction(string token)
  {
    return token.Length < 75 &&
           !_wordlistCleanFunctionRegex.IsMatch(token);
  }

  private static void WordlistTo_W2I()
  {
    var pathInput = Input("Path (input)");
    var pathOutput = Input("Path (output)");

    var idP = Path.Combine(pathOutput, "current.idx");
    var w2i = new EasyRocksDb(Path.Combine(pathOutput, "w2i"), true);

    var idx = File.Exists(idP) ? int.Parse(File.ReadAllText(idP)) : 0;
    var max = 10000;

    var files = Directory.GetFiles(pathInput, "*.lst", SearchOption.AllDirectories);
    var count = 0;

    foreach (var file in files)
    {
      var ts = DateTime.Now;
      var token = new List<string>(max);
      var sum = 0;

      using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
      using (var buffer = new BufferedStream(fs, 4096))
      using (var reader = new StreamReader(buffer, Encoding.UTF8))
      {
        while (!reader.EndOfStream)
        {
          token.Add(reader.ReadLine());
          if (token.Count < max)
            continue;

          sum += WordlistTo_W2I_Add(token, w2i, ref idx);
          token.Clear();
        }
      }

      if (token.Count > 0)
      {
        sum += WordlistTo_W2I_Add(token, w2i, ref idx);
        token.Clear();
      }

      File.WriteAllText(idP, idx.ToString());
      Console.WriteLine(
        $"{count + 1:D4}/{files.Length:D4} ({sum:D6} new) - {file} - [in {(DateTime.Now - ts).Seconds}s]");
    }

    w2i.Dispose();
  }

  private static int WordlistTo_W2I_Add(List<string> token, EasyRocksDb w2i, ref int idx)
  {
    // Finde nur neue Token
    var t2i = w2i.GetBatch(token.ToArray())
      .Where(x => string.IsNullOrEmpty(x.Value))
      .ToDictionary(x => x.Key, x => x.Value);

    // Füge neuen Token Index hinzu
    foreach (var k in t2i.Keys)
      t2i[k] = idx++.ToString();

    w2i.PutBatch(t2i);

    var res = t2i.Count;

    t2i.Clear();

    return res;
  }

  private static void WordlistTo_W2I_and_WI2()
  {
    var pathInput = Input("Path (input)");
    var pathOutput = Input("Path (output)");

    var idP = Path.Combine(pathOutput, "current.idx");
    var w2i = new EasyRocksDb(Path.Combine(pathOutput, "w2i"), true);
    var i2w = new EasyRocksDb(Path.Combine(pathOutput, "i2w"), true);

    var idx = File.Exists(idP) ? int.Parse(File.ReadAllText(idP)) : 0;
    var max = 10000;

    var files = Directory.GetFiles(pathInput, "*.lst", SearchOption.AllDirectories);
    var count = 0;

    foreach (var file in files)
    {
      var ts = DateTime.Now;
      var token = new List<string>(max);
      var sum = 0;

      using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
      using (var buffer = new BufferedStream(fs, 4096))
      using (var reader = new StreamReader(buffer, Encoding.UTF8))
      {
        while (!reader.EndOfStream)
        {
          token.Add(reader.ReadLine());
          if (token.Count < max)
            continue;

          sum += WordlistTo_W2I_and_WI2_Add(token, w2i, i2w, ref idx);
          token.Clear();
        }
      }

      if (token.Count > 0)
      {
        sum += WordlistTo_W2I_and_WI2_Add(token, w2i, i2w, ref idx);
        token.Clear();
      }

      File.WriteAllText(idP, idx.ToString());
      Console.WriteLine(
        $"{count + 1:D4}/{files.Length:D4} ({sum:D6} new) - {file} - [in {(DateTime.Now - ts).Seconds}s]");
    }

    w2i.Dispose();
    i2w.Dispose();
  }

  private static int WordlistTo_W2I_and_WI2_Add(List<string> token, EasyRocksDb w2i, EasyRocksDb i2w, ref int idx)
  {
    // Finde nur neue Token
    var t2i = w2i.GetBatch(token.ToArray())
      .Where(x => string.IsNullOrEmpty(x.Value))
      .ToDictionary(x => x.Key, x => x.Value);

    // Füge neuen Token Index hinzu
    foreach (var k in t2i.Keys)
      t2i[k] = idx++.ToString();

    // Index spiegeln
    var i2t = t2i.ToDictionary(x => x.Value, x => x.Key);

    w2i.PutBatch(t2i);
    i2w.PutBatch(i2t);

    var res = t2i.Count;

    t2i.Clear();
    i2t.Clear();

    return res;
  }
}