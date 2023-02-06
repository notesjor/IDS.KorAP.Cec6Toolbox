using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;

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
          break;
        case "4":
          break;
        case "5":
          WordlistTo_WFile();
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
    Console.WriteLine("5) Wordlist WFile");
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
          Console.WriteLine($"{++fCount:D5}/{files.Length}: {file} (in {(DateTime.Now - ts).TotalSeconds}s)");
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
          Console.WriteLine($"{++fCount:D4}/{files.Length:D4} (in {(DateTime.Now - ts).TotalSeconds}s) - {file} - [in: {cIn} - out: {cOu} = {cRm} removed]");
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

  private static void WordlistTo_WFile()
  {
    var pathInput = Input("Path (input)");
    var pathOutput = Input("File (output)");

    var tlist = GetWordIndexStack(pathInput);
    var tcount = tlist.Count;

    using (var fs = new FileStream(pathOutput, FileMode.Create, FileAccess.Write))
    using (var buffer = new BufferedStream(fs, 100000000))
    using (var writer = new StreamWriter(buffer, _utf8WithoutBom))
    {
      writer.NewLine = "\n";
      foreach (var x in tlist)
        writer.WriteLine(x);
    }

    File.WriteAllText(pathOutput + ".idx", tlist.Count.ToString());
  }

  private static HashSet<string> GetWordIndexStack(string pathInput)
  {
    var files = Directory.GetFiles(pathInput, "*.lst", SearchOption.AllDirectories);
    var count = 1;

    var tlist = new HashSet<string>(150000000);

    foreach (var file in files)
    {
      var ts = DateTime.Now;

      var cold = tlist.Count;
      var lines = File.ReadAllLines(file, _utf8WithoutBom);
      foreach (var x in lines)
        tlist.Add(x);

      Console.WriteLine(
        $"{count++:D4}/{files.Length:D4} ({cold:D6} > {lines.Length:D6} > {tlist.Count} [{tlist.Count - cold} new]) - {file} - [in {(DateTime.Now - ts).TotalSeconds}s]");
    }

    return tlist;
  }
}