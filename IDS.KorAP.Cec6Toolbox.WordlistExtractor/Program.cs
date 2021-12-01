using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Helper;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;

namespace IDS.KorAP.Cec6Toolbox.WordlistExtractor
{
  class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

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

      // Erstelle Listen
      Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = parallelMax }, file =>
      {
        try
        {
          var fn = Path.Combine(pathOutputDir, Path.GetFileName(file) + ".lst");
          if (File.Exists(fn) && new FileInfo(fn).Length > 0)
            return;
          var ts = DateTime.Now;

          var values = CorpusAdapterWriteDirect.Create(file).GetLayers(layer).FirstOrDefault()?.Values?.ToArray();
          if (values == null)
            return;

          File.WriteAllLines(fn, values, Encoding.UTF8);
          lock (fCountLock)
            Console.WriteLine($"{++fCount:D5}/{files.Length}: {file} (in {(DateTime.Now - ts).Seconds}s)");
        }
        catch
        {
          // ignore
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
          foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
            hashSet.Add(line);

        lock (lstLock)
        {
          // Schritt 2 - Bereinige Chunk-Liste
          using (var fs = new FileStream(pathOutputFile, FileMode.Open, FileAccess.Read))
          using (var buffer = new BufferedStream(fs, 4096))
          using (var reader = new StreamReader(buffer, Encoding.UTF8))
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
          File.AppendAllLines(pathOutputFile, hashSet, Encoding.UTF8);
          lock (fCountLock)
            Console.WriteLine($"{++fCount:D4}/{chunks.Length:D4} (in {(DateTime.Now - ts).Seconds}s)");
        }
      });

      Console.WriteLine("END!");
    }
  }
}
