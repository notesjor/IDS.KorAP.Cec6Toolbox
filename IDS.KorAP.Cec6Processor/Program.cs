using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.KorAP.Cec6Processor
{
  class Program
  {
    static void Main(string[] args)
    {
      Console.Write("THREADS: ");
      var threads = int.Parse(Console.ReadLine());

      var ignore = new[] { "wpd", "wpd", "wdd", "wud" };
      var entries = GetEntries();

      var level = 0;
      //var parallel = 10;
      long min = 0;
      long max = 25 * 1000 * 1000;
      Entry[] selected;
      
      do
      {
        if (File.Exists("/home/ruediger/cec6processor.stop"))
          return;

        level++;

        selected = entries.Where(x => x.Valid(min, max)).ToArray();
        Console.WriteLine($"ENTER {level}-LEVEL: {selected.Length} files");

        Parallel.ForEach(selected, new ParallelOptions { MaxDegreeOfParallelism = threads }, x =>
          {
            if (File.Exists("/home/ruediger/cec6processor.stop"))
              return;
            if (ignore.Any(prefix => Path.GetFileName(x.Path).StartsWith(prefix)))
              return;


            var ts = DateTime.Now;
            var outputPath = Path.Combine("/home/ruediger/CEC6", Path.GetFileNameWithoutExtension(x.Path) + ".cec6.gz");
            try
            {
              if (File.Exists(outputPath))
                return;

              var process = Process.Start(new ProcessStartInfo
              {
                FileName = "./IDS.KorAP.Cec6Processor.Thread",
                Arguments = $"\"{x.Path}\" \"{outputPath}\"",
              });
              process.WaitForExit();

              var iSize = new FileInfo(x.Path).Length;
              iSize += new FileInfo(x.Path.Replace(".zip", ".tree_tagger.zip")).Length;
              var oSize = (new FileInfo(outputPath)).Length;
              var span = DateTime.Now - ts;

              Console.WriteLine($"{level}> {outputPath}...ok ({span})! >>> in: {iSize / 1000}KB / out: {oSize / 1000}KB ({Math.Round(iSize / 1000.0 / span.TotalSeconds, 2)}KB/s)");
            }
            catch (Exception ex)
            {
              Console.WriteLine($"{level}> {outputPath}...error ({DateTime.Now - ts})!");
              try
              {
                File.AppendAllLines("error.log", new[] { outputPath, ex.Message, ex.StackTrace });
              }
              catch
              {
                // ignore
              }
            }
          });

        //parallel--;
        //if (parallel < 2)
        //  parallel = 2;
        min = max;
        max *= 2;
      } while (selected.Length > 0);

      Console.WriteLine("!END!");
    }

    private static List<Entry> GetEntries()
    {
      var entries = new List<Entry>();

      var lines = File.ReadAllLines("catalog.tsv", Encoding.UTF8);
      foreach (var line in lines)
      {
        var entry = line.Split(new[] { "\t" }, StringSplitOptions.RemoveEmptyEntries);
        if (entry.Length != 2)
          continue;

        entries.Add(new Entry(entry[0], entry[1]));
      }

      return entries;
    }

    struct Entry
    {
      public long Size { get; set; }
      public string Path { get; set; }

      public Entry(string corpus, string filename)
      {
        Path = $"/vol/corpora/DeReKo/{corpus}/KorAP/zip/{filename}";
        Size = (new FileInfo(Path)).Length;
      }

      public bool Valid(long min, long max)
      {
        return Size >= min && Size < max;
      }
    }
  }
}
