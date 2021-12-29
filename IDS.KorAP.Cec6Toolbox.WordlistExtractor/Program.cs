using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Blocks;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Helper;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Extension;

namespace IDS.KorAP.Cec6Toolbox.WordlistExtractor
{
  class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();
      var utf8WithoutBom = new UTF8Encoding(false);

      var layer = args[0];
      var pathInput = args[1];
      var parallelMax = int.Parse(args[2]);
      var pathOutput = args[3];

      var pathOutputFile = pathOutput + ".lst";
      var pathOutputDir = pathOutput;
      if (!Directory.Exists(pathOutputDir))
        Directory.CreateDirectory(pathOutputDir);

      var files = Directory.GetFiles(pathInput, "*.cec6.gz", SearchOption.AllDirectories);
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

          var corpus = CorpusAdapterWriteDirect.Create(file);
          var select = corpus.ToSelection();
          var block = select.CreateBlock<Frequency1LayerBlock>();
          block.LayerDisplayname = layer;
          block.Calculate();

          var values = block.Frequency?.Keys.ToArray();
          if (values == null)
            return;

          File.WriteAllLines(fn, values, utf8WithoutBom);
          lock (fCountLock)
            Console.WriteLine($"{++fCount:D5}/{files.Length}: {file} (in {(DateTime.Now - ts).Seconds}s)");

          block.Frequency.Clear();
          corpus.Dispose();
        }
        catch (Exception ex)
        {
          lock(fCountLock)
            File.AppendAllLines(error, new[]{ ex.Message, ex.StackTrace }, utf8WithoutBom);
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

      Console.WriteLine("LST GENERATED!");

      if (File.Exists(pathOutputFile))
        File.Delete(pathOutputFile);
      File.Create(pathOutputFile).Close();

      var chunks = (Directory.GetFiles(pathOutputDir, "*.lst", SearchOption.TopDirectoryOnly)).Chunk(parallelMax).ToArray();
      fCount = 0;
      var lstLock = new object();

      Parallel.ForEach(chunks, new ParallelOptions { MaxDegreeOfParallelism = parallelMax / 10 }, chunk =>
      {
        var ts = DateTime.Now;

        // Schritt 1 - Lade Chunk-Liste
        var hashSet = new HashSet<string>();
        foreach (var file in chunk)
          foreach (var line in File.ReadAllLines(file, utf8WithoutBom))
            hashSet.Add(line);

        lock (lstLock)
        {
          // Schritt 2 - Bereinige Chunk-Liste
          using (var fs = new FileStream(pathOutputFile, FileMode.Open, FileAccess.Read))
          using (var buffer = new BufferedStream(fs, 4096))
          using (var reader = new StreamReader(buffer, utf8WithoutBom))
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
          File.AppendAllLines(pathOutputFile, hashSet, utf8WithoutBom);
          lock (fCountLock)
            Console.WriteLine($"{++fCount:D4}/{chunks.Length:D4} (in {(DateTime.Now - ts).Seconds}s)");
        }
      });

      Console.WriteLine("END!");
    }
  }
}
