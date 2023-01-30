using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace IDS.KorAP.Cec6Toolbox.WordlistToolbox
{
  internal class Program
  {
    private static Encoding _utf8WithoutBom = new UTF8Encoding(false);

    static void Main()
    {
      CorpusExplorerEcosystem.InitializeMinimal();
      
      var key = ShowOptions();

      while(true)
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
            WordlistMerge();
            break;
          case "4":
            WordlistTo_W2I();
            break;
          case "5":
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
      Console.WriteLine("3) Merge Wordlist");
      Console.WriteLine("4) Wordlist to RocksDB (W2I only)");
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
            Console.WriteLine($"{++fCount:D5}/{files.Length}: {file} (in {(DateTime.Now - ts).Seconds}s)");

          corpus.Dispose();
        }
        catch (Exception ex)
        {
          lock(fCountLock)
            File.AppendAllLines(error, new[]{ ex.Message, ex.StackTrace }, _utf8WithoutBom);
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
      var parallelMax = int.Parse(Input("Threads"));
      var pathOutput = Input("Path (output)");

      var files = Directory.GetFiles(pathInput, "*.lst", SearchOption.AllDirectories);
      var fCount = 0;
      var fCountLock = new object();

      Parallel.ForEach(files, new ParallelOptions{ MaxDegreeOfParallelism = parallelMax}, file=>
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
            var idx = 0;
            var aList = new string[max];
            while(true)
            {
              aList[idx++] = reader.ReadLine();
              if (idx < max)
                continue;

              var bList = new List<string>(max);
              for (var i = 0; i < aList.Length; i++)
              {
                if (WordlistCleanFunction(ref aList[i]))
                  bList.Add(aList[i]);
              }

              File.AppendAllLines(pathOutput, bList, _utf8WithoutBom);
            }
          }
          
          lock (fCountLock)
            Console.WriteLine($"{++fCount:D4}/{files.Length:D4} (in {(DateTime.Now - ts).Seconds}s)");
        }
        catch
        {
          File.Copy(file, outputFile);
        }
      });
    }

    private static Regex _wordlistCleanFunctionRegex01 = new(@"[\+\-#\?_\/\:\\]{3,}");
    private static Regex _wordlistCleanFunctionRegex02 = new(@"^\<");
    private static Regex _wordlistCleanFunctionRegex03 = new(@"\>$");
    
    private static bool WordlistCleanFunction(ref string token) 
      => token.Length < 75 &&
         !_wordlistCleanFunctionRegex01.IsMatch(token) &&
         !_wordlistCleanFunctionRegex02.IsMatch(token) && 
         !_wordlistCleanFunctionRegex03.IsMatch(token);

    private static void WordlistMerge()
    {
      var pathInput = Input("Path (input)");
      var parallelMax = int.Parse(Input("Threads"));
      var pathOutput = Input("File (output)");

      var chunks = Directory.GetFiles(pathInput, "*.lst", SearchOption.TopDirectoryOnly).Chunk(parallelMax).ToArray();
      var fCount = 0;
      var fCountLock = new object();
      var lstLock = new object();

      Parallel.ForEach(chunks, new ParallelOptions { MaxDegreeOfParallelism = parallelMax }, chunk =>
      {
        var ts = DateTime.Now;

        // Schritt 1 - Lade Chunk-Liste
        var hashSet = new HashSet<string>();
        foreach (var file in chunk)
        foreach (var line in File.ReadAllLines(file, _utf8WithoutBom))
          hashSet.Add(line);

        lock (lstLock)
        {
          // Schritt 2 - Bereinige Chunk-Liste
          using (var fs = new FileStream(pathOutput, FileMode.Open, FileAccess.Read))
          using (var buffer = new BufferedStream(fs, 4096))
          using (var reader = new StreamReader(buffer, _utf8WithoutBom))
          {
            while (!reader.EndOfStream)
            {
              var token = reader.ReadLine();
              if (string.IsNullOrWhiteSpace(token))
                continue; 
              hashSet.Remove(token);
            }
          }

          // Schritt 3 - Schreibe Chunk-Liste
          File.AppendAllLines(pathOutput, hashSet, _utf8WithoutBom);
          lock (fCountLock)
            Console.WriteLine($"{++fCount:D4}/{chunks.Length:D4} (in {(DateTime.Now - ts).Seconds}s)");
        }
      });

      Console.WriteLine("END!");
    }

    private static void WordlistTo_W2I()
    {
      throw new NotImplementedException();
    }

    private static void WordlistTo_W2I_and_WI2()
    {
      throw new NotImplementedException();
    }
  }
}