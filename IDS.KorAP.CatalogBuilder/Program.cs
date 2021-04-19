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

      var newest = new Dictionary<string, string>();
      var size = new Dictionary<string, long>();
      var catalog = new Dictionary<string, List<string>>();

      Console.WriteLine("RESULTS:");
      foreach (var dir in dirs)
      {
        var release = dir.Replace("/vol/corpora/DeReKo/", "");
        catalog.Add(release, new List<string>());

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
          if (new FileInfo(file).Length < 100)
            continue;
          var basePath = file.Replace(".tree_tagger.", ".");
          if (!File.Exists(basePath))
            continue;
          var baseSize = new FileInfo(basePath).Length;
          if (baseSize < 100)
            continue;

          found++;
          var key = Path.GetFileName(basePath);
          if (newest.ContainsKey(key))
          {
            var ukey = newest[key];
            newest[key] = release;
            size[key] = baseSize;
            catalog[ukey].Remove(key);
            catalog[release].Add(key);
          }
          else
          {
            newest.Add(key, release);
            size.Add(key, baseSize);
            catalog[release].Add(key);
          }
        }

        Console.WriteLine($"{found}!");
      }

      File.WriteAllLines("newest.tsv", newest.Select(x=> $"{x.Key}\t{x.Value}"), Encoding.UTF8);
      File.WriteAllLines("size.tsv", size.Select(x => $"{x.Key}\t{x.Value}"), Encoding.UTF8);
      File.WriteAllLines("catalog.tsv", (from x in catalog from y in x.Value select $"{x.Key}\t{y}"), Encoding.UTF8);
    }
  }
}
