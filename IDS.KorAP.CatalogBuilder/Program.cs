using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IDS.KorAP.CatalogBuilder
{
  class Program
  {
    static void Main(string[] args)
    {
      var dirs = Directory.GetDirectories("/vol/corpora/DeReKo", "DeReKo-*").OrderBy(x => x).ToList();
      dirs.Add("/vol/corpora/DeReKo/current");

      Console.WriteLine("RELEASES:");
      foreach (var dir in dirs)
        Console.WriteLine($"\t{dir}");

      var entries = new Dictionary<string, Entry>();

      Console.WriteLine("RESULTS:");
      foreach (var dir in dirs)
      {
        var release = dir.Replace("/vol/corpora/DeReKo/", "");

        var korapDir = Path.Combine(dir, "KorAP/zip/");
        if (!Directory.Exists(korapDir))
        {
          Console.WriteLine($"{korapDir}... not found!");
          continue;
        }

        var files = Directory.GetFiles(korapDir, "*.tree_tagger.zip");
        Console.Write($"{dir}...{files.Length} files...");
        var found = 0;

        foreach (var file in files)
        {
          var size = new FileInfo(file).Length;
          if (size < 100)
            continue;
          var basePath = file.Replace(".tree_tagger.", ".");
          if (!File.Exists(basePath))
            continue;

          found++;
          var key = Path.GetFileName(basePath);
          if (entries.ContainsKey(key))
          {
            entries[key].Size = size;
            entries[key].Versions.Add(release);
          }
          else
          {
            var newEntry = new Entry { Size = size };
            newEntry.Versions.Add(release);
            entries.Add(key, newEntry);
          }
        }

        Console.WriteLine($"{found}!");
      }

      File.WriteAllLines("size.tsv", entries.Select(x => $"{x.Key}\t{x.Value.Size}"), Encoding.UTF8);
      File.WriteAllLines("catalog.tsv", entries.Select(x => $"{x.Value.LastVersion}\t{x.Key}"), Encoding.UTF8);
    }

    public class Entry
    {
      public List<string> Versions { get; set; } = new List<string>();
      public string LastVersion => Versions.Last();
      public long Size { get; set; }
    }
  }
}
