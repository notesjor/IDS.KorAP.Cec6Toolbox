using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IDS.KorAP.Cec6Toolbox.Validator
{
  class Program
  {
    static void Main(string[] args)
    {
      var entries = GetEntries();
      var report = new List<string>();
      foreach (var entry in entries)
      {
        var cec6Path = $"/home/ruediger/CEC6/{entry.Key.Replace(".zip", ".cec6.gz")}";
        
        long size = 0;
        if (File.Exists(cec6Path))
          size = new FileInfo(cec6Path).Length;

        report.Add($"{entry.Key}\t{entry.Value}\t{size}");
      }
      File.WriteAllLines("report.csv", report, Encoding.UTF8);
    }

    private static Dictionary<string, long> GetEntries()
    {
      var entries = new Dictionary<string, long>();

      var lines = File.ReadAllLines("catalog.tsv", Encoding.UTF8);
      foreach (var line in lines)
      {
        var entry = line.Split(new[] { "\t" }, StringSplitOptions.RemoveEmptyEntries);
        if (entry.Length != 2)
          continue;

        long size = 0;
        var pathBase = $"/vol/corpora/DeReKo/{entry[0]}/KorAP/zip/{entry[1]}";
        if (File.Exists(pathBase))
          size += new FileInfo(pathBase).Length;
        var pathTree = pathBase.Replace(".zip", ".tree_tagger.zip");
        if (File.Exists(pathTree))
          size += new FileInfo(pathTree).Length;

        entries.Add(entry[1], size);
      }

      return entries;
    }
  }
}
