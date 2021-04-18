using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;

namespace IDS.KorAP.Cec6Processor
{
  class Program
  {
    static void Main(string[] args)
    {
      var entries = GetEntries();

      var level = 0;
      var parallel = 80;
      long min = 0;
      long max = 25 * 1000 * 1000;
      Entry[] selected;

      CorpusExplorerEcosystem.InitializeMinimal();

      do
      {
        level++;

        selected = entries.Where(x => x.Valid(min, max)).ToArray();
        Console.WriteLine($"ENTER {level}-LEVEL: {selected.Length} files");

        Parallel.ForEach(selected, new ParallelOptions {MaxDegreeOfParallelism = parallel}, x =>
        {
          var outputPath = Path.Combine("/home/ruediger/CEC6", Path.GetFileNameWithoutExtension(x.Path) + ".cec6.gz");
          try
          {
            if (File.Exists(outputPath))
            {
              Console.WriteLine($"{outputPath}...already done!");
              return;
            }

            var importer = new ImporterKorapOnlyTreeTagger();
            importer.Execute(new[] {x.Path}).FirstOrDefault()?.Save(outputPath, true);
            Console.WriteLine($"{outputPath}...ok!");
          }
          catch (Exception ex)
          {
            Console.WriteLine($"{outputPath}...error!");
            try
            {
              File.AppendAllLines("error.log", new[]{outputPath, ex.Message, ex.StackTrace});
            }
            catch
            {
              // ignore
            }
          }
        });

        parallel /= 2;
        if (parallel < 2)
          parallel = 2;
      } while (selected.Length > 0);
    }

    private static List<Entry> GetEntries()
    {
      var entries = new List<Entry>();

      var lines = File.ReadAllLines("size.tsv", Encoding.UTF8);
      foreach (var line in lines)
      {
        var entry = line.Split(new[] {"\t"}, StringSplitOptions.RemoveEmptyEntries);
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

      public Entry(string path, string size)
      {
        Path = path;
        Size = long.Parse(size);
      }

      public bool Valid(long min, long max)
      {
        return Size >= min && Size < max;
      }
    }
  }
}
