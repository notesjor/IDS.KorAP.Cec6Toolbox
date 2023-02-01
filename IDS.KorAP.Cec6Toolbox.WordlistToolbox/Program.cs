using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CorpusExplorer.Sdk.Db.RocksDb;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using RocksDbSharp;

namespace IDS.KorAP.Cec6Toolbox.WordlistToolbox;

internal class Program
{
  private static readonly Encoding _utf8WithoutBom = new UTF8Encoding(false);

  /// <summary>
  ///   - Keine Leerzeichen im Token \s
  ///   - Keine Sequenz von xyx - entsteht im KorAP-Prozess z. B. xyxHTMLxyx
  ///   - Keine Tokens mit mehr als 3 Zeichen aus dem Set + - # ? _ / \ : - z. B. +++ oder :// [https://]
  ///   - Keine Tokens mit &lt; am Anfang oder &gt; am Ende
  /// </summary>
  private static readonly Regex _wordlistCleanFunctionRegex = new(@"\s|xyx|[\+\-#\?_\/\:\\]{3,}|^\<|\>$");

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
    var parallelMax = int.Parse(Input("Threads (Q1 = x/5 / Q2 = 5)"));
    var pathOutput = Input("Path (output)");

    var files = Directory.GetFiles(pathInput, "*.lst", SearchOption.AllDirectories);
    var fCount = 0;
    var fCountLock = new object();

    Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = parallelMax / 5 }, file =>
    {
      var outputFile = Path.Combine(pathOutput, Path.GetFileName(file));
      try
      {
        var ts = DateTime.Now;

        var vList = new ConcurrentBag<string>();
        var lines = File.ReadAllLines(file, _utf8WithoutBom);
        Parallel.ForEach(lines, new ParallelOptions { MaxDegreeOfParallelism = 5 }, x =>
        {
          if (WordlistCleanFunction(x))
            vList.Add(x);
        });

        File.WriteAllLines(outputFile, vList, _utf8WithoutBom);

        var cIn = lines.Length;
        var cOu = vList.Count;
        var cRm = cIn - cOu;
        
        vList.Clear();
        
        lock (fCountLock)
          Console.WriteLine($"{++fCount:D4}/{files.Length:D4} (in {(DateTime.Now - ts).Seconds}s) - {file} - [in: {cIn} - out: {cOu} = {cRm} removed]");
      }
      catch (Exception ex)
      {
        Console.WriteLine(file);
        Console.WriteLine(ex.Message);
        Console.WriteLine(ex.StackTrace);
        File.Copy(file, outputFile);
      }
    });
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
    var cache = new HashSet<string>();

    var idx = File.Exists(idP) ? int.Parse(File.ReadAllText(idP)) : 0;

    var files = Directory.GetFiles(pathInput, "*.lst", SearchOption.AllDirectories);
    var count = 1;

    foreach (var file in files)
    {
      var ts = DateTime.Now;
      var sum = WordlistTo_W2I_Add(file, w2i, ref idx, ref cache);

      File.WriteAllText(idP, idx.ToString());
      Console.WriteLine(
        $"{count++:D4}/{files.Length:D4} ({sum:D6} new) - {file} - [in {(DateTime.Now - ts).Seconds}s]");
    }

    w2i.Dispose();
  }

  private static int WordlistTo_W2I_Add(string path, EasyRocksDb w2i, ref int idx, ref HashSet<string> cache)
  {
    // Bereinige Token die im Cache sind
    var lines = File.ReadAllLines(path, _utf8WithoutBom);
    var token = new List<string>(lines.Length);
    foreach (var x in lines)
    {
      if(!cache.Contains(x))
        token.Add(x);
    }

    // Leere Cache
    cache.Clear();

    // Finde Token in RDB
    var dict = w2i.GetBatch(token.ToArray());
    var t2i = new Dictionary<string, string>();

    // Sortiere Token in Cache und T2I
    foreach (var x in dict)
    {
      if (string.IsNullOrEmpty(x.Value)) // Füge neuen Token Index hinzu
        t2i.Add(x.Key, idx++.ToString());
      else
        cache.Add(x.Key); // Cache bekannte Token für nächste Runde
    }

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
    var cache = new HashSet<string>();

    var idx = File.Exists(idP) ? int.Parse(File.ReadAllText(idP)) : 0;

    var files = Directory.GetFiles(pathInput, "*.lst", SearchOption.AllDirectories);
    var count = 1;

    foreach (var file in files)
    {
      var ts = DateTime.Now;
      var sum = WordlistTo_W2I_and_WI2_Add(file, w2i, i2w, ref idx, ref cache);

      File.WriteAllText(idP, idx.ToString());
      Console.WriteLine(
        $"{count++:D4}/{files.Length:D4} ({sum:D6} new) - {file} - [in {(DateTime.Now - ts).Seconds}s]");
    }

    w2i.Dispose();
    i2w.Dispose();
  }

  private static int WordlistTo_W2I_and_WI2_Add(string path, EasyRocksDb w2i, EasyRocksDb i2w, ref int idx, ref HashSet<string> cache)
  {
    // Bereinige Token die im Cache sind
    var lines = File.ReadAllLines(path, _utf8WithoutBom);
    var token = new List<string>(lines.Length);
    foreach (var x in lines)
    {
      if(!cache.Contains(x))
        token.Add(x);
    }

    // Leere Cache
    cache.Clear();

    // Finde Token in RDB
    var dict = w2i.GetBatch(token.ToArray());
    var t2i = new Dictionary<string, string>();

    // Sortiere Token in Cache und T2I
    foreach (var x in dict)
    {
      if (string.IsNullOrEmpty(x.Value)) // Füge neuen Token Index hinzu
        t2i.Add(x.Key, idx++.ToString());
      else
        cache.Add(x.Key); // Cache bekannte Token für nächste Runde
    }

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