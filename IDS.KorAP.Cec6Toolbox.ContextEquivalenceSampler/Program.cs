using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusExplorer.Sdk.Blocks;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Ecosystem.Model;
using CorpusExplorer.Sdk.Model;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Cache;
using CorpusExplorer.Sdk.Model.Extension;

namespace IDS.KorAP.Cec6Toolbox.ContextEquivalenceSampler
{
  class Program
  {
    private static Random _rnd = new Random();

    static void Main(string[] args)
    {
      if (args == null || args.Length < 7)
      {
        PrintHelp();
        return;
      }

      var layer = args[0]; // Layer (alle Korpora müssen diesen Layer beinahlten)
      var query = args[1]; // Token für das die Kontext-Äquivalenz hergestellt werden soll
      var min = double.Parse(args[2]); // Minimale Anpassung (wenn unterschritten > Abbruch)
      var max = int.Parse(args[3]); // Maximale Anzahl an Runden (wenn überschritten > Abbruch)
      var log = args[4]; // Ausgabedatei

      // Initialisiere den CorpusExplorer
      var project = CorpusExplorerEcosystem.InitializeMinimal(new CacheStrategyDisableCaching());
      Configuration.MinimumFrequency = 10;
      Configuration.MinimumSignificance = 1.0;

      var files = new List<string>(args);
      files.RemoveRange(0, 5); // zwei oder mehr Korpora

      PrintInfo(files, layer, query, min, max, log);

      var corpora = files.Select(x => CorpusAdapterWriteDirect.Create(x)).ToArray();
      foreach (var c in corpora)
        project.Add(c);

      // Ratios - in der ersten Runde immer 1.0
      // Danach werden die Ratios angepasst.
      var ratios = new double[files.Count];
      for (var i = 0; i < ratios.Length; i++)
        ratios[i] = 1.0;

      // Berechne einmalig für ein Korpus die Kookkurrenzen zum Suchwort
      var parts = GetSelectionsCooccurrences(corpora.Select(x => x.ToSelection()).ToArray(), layer, query);
      var targets = parts.Select(x => x.Count).ToArray();

      for (var i = 1; i <= max; i++)
      {
        var selections = new Selection[corpora.Length];
        for (var j = 0; j < corpora.Length; j++)
          // Zufällige Auswahl an Texten im Verhältnis Korpusgröße (in Token) * Ratio (z. B. 0.75)
          selections[j] = SampleCorpus(corpora[j], ratios[j]);

        // Mische alle zufällig ausgewählten Texte zusammen und berechne die Kookkurrenzen zum Suchwort
        var top = GetTopCooccurrences(project.SelectAll.CreateTemporary(MergeSelections(selections)), layer, query);
        // Berechne wiviele von den Korpus-Kookkurrenzen in der Mischung unter den TOP-Kookkurrenzen vorkommen.
        var matches = GetMatches(top, parts);
        // Berechne eine neue Ratio
        var ratiosNew = Resolve(matches, targets);

        LogResults(log, i, max, files, ratios, matches, targets, ratiosNew);

        // Abbruch, wenn Schwelle unterschritten
        // Abbruch, wenn ein Korpus überhaupt nicht mehr gebraucht wird.
        if ((CalcChangeDiff(ratios, ratiosNew) < min) || (ratiosNew.Any(x => x == 0)))
        {
          Save(files, query, selections);
          break;
        }

        ratios = ratiosNew;
      }
    }

    private static void Save(List<string> files, string query, Selection[] selections)
    {
      for (var i = 0; i < files.Count; i++)
      {
        var file = files[i];
        var path = Path.Combine(Path.GetDirectoryName(file), $"CES_{query}_{Path.GetFileName(file)}");
        selections[i].ToCorpus().Save(path);
      }
    }

    private static void PrintInfo(List<string> files, string layer, string query, double min, int max, string log)
    {
      Console.WriteLine("ContextEquivalenceSampler - (c) 2022 by Jan Oliver Rüdiger");
      Console.WriteLine($"{files.Count} files - Layer: {layer} - Query: {query}");
      Console.WriteLine($"min. change: {min} - max. rounds: {max}");
      Console.WriteLine($"output: {log}");
    }

    private static double CalcChangeDiff(double[] ratios, double[] ratiosNew)
      => ratios.Select((t, i) => Math.Abs(t - ratiosNew[i])).Sum();

    private static void LogResults(string log, int i, int max, List<string> files, double[] ratios, int[] matches,
                                   int[] targets, double[] ratiosNew)
    {
      if (!File.Exists(log))
        LogHeader(log, files);

      var stb = new List<string>();
      stb.Add(i.ToString());
      stb.Add(max.ToString());
      for (var j = 0; j < files.Count; j++)
      {
        stb.Add(Path.GetFileNameWithoutExtension(files[j]));
        stb.Add(ratios[j].ToString());
        stb.Add(matches[j].ToString());
        stb.Add(targets[j].ToString());
        stb.Add((matches[j] / (double)targets[j] * 100.0).ToString());
        stb.Add(ratiosNew[j].ToString());
      }

      File.AppendAllText(log, string.Join("\t", stb) + "\r\n", Encoding.UTF8);
    }

    private static void LogHeader(string log, List<string> files)
    {
      var stb = new List<string>();
      stb.Add("ROUND");
      stb.Add("ROUND (MAX)");
      for (var j = 0; j < files.Count; j++)
      {
        stb.Add($"FILE ({j})");
        stb.Add($"RATIO - PRE ({j})");
        stb.Add($"MATCHES ({j})");
        stb.Add($"TARGET ({j})");
        stb.Add($"MATCHES% ({j})");
        stb.Add($"RATIO - POST ({j})");
      }

      File.AppendAllText(log, string.Join("\t", stb) + "\r\n", Encoding.UTF8);
    }

    private static double[] Resolve(int[] matches, int[] targets)
    {
      var diff = new List<double>();
      for (var i = 0; i < matches.Length; i++)
      {
        var x = matches[i];
        if (x == 0)
        {
          diff.Add(1);
          continue;
        }

        var val = 1.0 + (targets[i] - (double)x) / targets[i];
        diff.Add(val);
      }

      var max = diff.Select(Math.Abs).Max();
      return diff.Select(x => x / max).ToArray();
    }

    private static int[] GetMatches(string[] top, HashSet<string>[] parts)
    {
      var res = new int[parts.Length];
      for (var i = 0; i < parts.Length; i++)
        res[i] = top.Count(x => parts[i].Contains(x));
      return res;
    }

    private static string[] GetTopCooccurrences(Selection all, string layer, string query) =>
      GetCooccurrences(layer, query, all).Select(x => x.Key)
                                         .ToArray();

    private static IEnumerable<Guid> MergeSelections(Selection[] selections)
    {
      var res = new List<Guid>();
      foreach (var s in selections)
        res.AddRange(s.DocumentGuids);
      return res;
    }

    private static HashSet<string>[] GetSelectionsCooccurrences(Selection[] selections, string layer, string query)
    {
      var res = new HashSet<string>[selections.Length];
      for (var i = 0; i < selections.Length; i++)
        res[i] = new HashSet<string>(GetCooccurrences(layer, query, selections[i]).Keys);

      return res;
    }

    private static Dictionary<string, double> GetCooccurrences(string layer, string query, Selection s)
    {
      var block = s.CreateBlock<CooccurrenceSelectiveBlock>();
      block.LayerDisplayname = layer;
      block.LayerQueries = new[] { query };
      block.Calculate();

      return block.CooccurrenceSignificance;
    }

    private static Selection SampleCorpus(CorpusAdapterWriteDirect cec6, double ratio)
    {
      if (ratio >= 1.0)
        return cec6.ToSelection();

      var input = cec6.DocumentGuids.ToList();
      var output = new List<Guid>();

      var size = cec6.CountToken * ratio;
      long cnt = 0;

      while (cnt < size && input.Count > 0)
      {
        var rnds = GetRandomIndices(input.Count, 1000);
        var breaks = false;

        foreach (var id in rnds)
        {
          var guid = input[id];
          output.Add(guid);
          input.RemoveAt(id);

          cnt += cec6.GetDocumentLengthInWords(guid);
          if (cnt > size)
          {
            breaks = true;
            break;
          }
        }
        if (breaks)
          break;
      }

      return cec6.ToSelection().CreateTemporary(output);
    }

    private static void PrintHelp()
    {
      Console.WriteLine("./ces \"Lemma (tree_tagger)\" \"Haus\" 0.1 10 output.log corpus1.cec6 corpus2.cec6 corpus3.cec6");
    }

    private static int[] GetRandomIndices(int count, int max)
    {
      var res = new int[max];
      for (var i = 0; i < max && count > 0; i++)
        res[i] = _rnd.Next(0, count--);
      return res;
    }
  }
}
