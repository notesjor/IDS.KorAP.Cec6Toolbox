using CorpusExplorer.Sdk.Blocks;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Ecosystem.Model;
using CorpusExplorer.Sdk.Model;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Cache;
using CorpusExplorer.Sdk.Model.Extension;
using CorpusExplorer.Sdk.Utils.CorpusManipulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IDS.KorAP.Cec6Toolbox.ContextEquivalenceSampler
{
  class Program
  {
    private static Random _rnd = new Random();
    private static CorpusAdapterWriteDirect _corpus;
    private static Selection _all;

    static void Main(string[] args)
    {
      if (args == null || args.Length < 7)
      {
        PrintHelp();
        return;
      }

      var layer = args[0]; // Layer (alle Korpora müssen diesen Layer beinahlten)
      var min = double.Parse(args[1]); // Minimale Anpassung (wenn unterschritten > Abbruch)
      var max = int.Parse(args[2]); // Maximale Anzahl an Runden (wenn überschritten > Abbruch)
      var log = args[3]; // Ausgabedatei

      // Initialisiere den CorpusExplorer
      CorpusExplorerEcosystem.InitializeMinimal(new CacheStrategyDisableCaching());
      Configuration.MinimumFrequency = 10;
      Configuration.MinimumSignificance = 1.0;

      var files = new List<string>(args.Skip(4));

      PrintInfo(files, layer, min, max, log);

      // Preparation
      var selections = PrepareCorpusIndex(files, layer);

      var corporaCount = selections.Count();
      var sizes = selections.Select(x => x.Sum(y => (long)y.Value)).ToArray();
      // Berechne das Kookkurrenzfeld für die Basis-Korpora
      var baseField = selections.Select(x => GetBaseCooccurrences(layer, x.Keys)).ToArray();
      var targets = baseField.Select(x => x.Sum(x => x.Value.Length)).ToArray();

      // Ratios - in der ersten Runde immer 1.0
      // Danach werden die Ratios angepasst.
      var ratios = new double[corporaCount];
      for (var i = 0; i < ratios.Length; i++)
        ratios[i] = 1.0;

      List<Guid> last = null;
      for (var i = 1; i <= max; i++)
      {
        last = SampleCorpus(ref ratios, ref sizes, ref selections);
        // Berechne, die Kookkurrenzen für das Sample
        var cooccurrences = GetCooccurrences(layer, last);
        // Zähle, pro Korpus wieviele Kookkurrenzen mit den Gesamt-Kookkurrenzen übereinstimmen
        var matches = GetMatches(cooccurrences, baseField);
        // Berechne eine neue Ratio
        var ratiosNew = Resolve(matches, targets, min, ratios);

        LogResults(log, i, max, files, ratios, matches, targets, ratiosNew);

        // Abbruch, wenn Schwelle unterschritten
        // Abbruch, wenn ein Korpus überhaupt nicht mehr gebraucht wird.
        if (CalcChangeDiff(ratios, ratiosNew) < min || ratiosNew.Any(x => x == 0) || ratiosNew.All(x => (int)x == 1))
        {
          Save(last);
          return; // Beende Programm
        }

        ratios = ratiosNew;
      }

      Save(last);
    }

    private static List<Guid> SampleCorpus(ref double[] ratios, ref long[] sizes, ref List<Dictionary<Guid, int>> corpora)
    {
      var res = new List<Guid>();

      for (int i = 0; i < corpora.Count; i++)
      {
        var ratio = ratios[i];

        if (ratio >= 1.0)
        {
          res.AddRange(corpora[i].Keys);
          continue;
        }

        var c = corpora[i];
        long size = (long)(sizes[i] / ratio);
        if (size < 1000)
          size = 1000;
        long cnt = 0;

        var input = c.Keys.ToList();
        var output = new List<Guid>();

        while (cnt < size && input.Count > 0)
        {
          var rnds = GetRandomIndices(input.Count, 1000);
          var breaks = false;

          foreach (var id in rnds)
          {
            var guid = input[id];
            output.Add(guid);
            input.RemoveAt(id);

            cnt += c[guid];
            if (cnt > size)
            {
              breaks = true;
              break;
            }
          }
          if (breaks)
            break;
        }

        res.AddRange(output);
      }

      return res;
    }

    private static List<Dictionary<Guid, int>> PrepareCorpusIndex(List<string> files, string layer)
    {
      var res = new List<Dictionary<Guid, int>>();

      var merger = new CorpusMerger();
      foreach (var file in files)
      {
        var corpus = CorpusAdapterWriteDirect.Create(file);
        foreach (var l in corpus.LayerUniqueDisplaynames.Where(x => x != layer))
          corpus.LayerDelete(l);
        merger.Input(corpus);

        var sLayer = corpus.GetLayers(layer).First();
        res.Add(sLayer.DocumentGuids.AsParallel().ToDictionary(x => x, x => sLayer[x].Sum(y => y.Length)));

        corpus.Dispose();
      }

      merger.Execute();

      merger.Output.TryDequeue(out var c);
      _corpus = (CorpusAdapterWriteDirect)c;
      _all = _corpus.ToSelection();

      return res;
    }

    private static void Save(List<Guid> selection)
    {
      if (selection != null)
        _all.CreateTemporary(selection).ToCorpus().Save("output.cec6", false);
    }

    private static void PrintInfo(List<string> files, string layer, double min, int max, string log)
    {
      Console.WriteLine("ContextEquivalenceSampler - (c) 2022 by Jan Oliver Rüdiger");
      Console.WriteLine($"{files.Count} files - Layer: {layer}");
      Console.WriteLine($"min. change: {min} - max. rounds: {max}");
      Console.WriteLine($"output: {log}");
    }

    private static double CalcChangeDiff(double[] r, double[] n) => r.Select((t, i) => Math.Abs(t - n[i])).Max();

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

    private static double[] Resolve(int[] matches, int[] targets, double min, double[] ratios)
    {
      var factor = new double[matches.Length];
      for (var i = 0; i < factor.Length; i++)
      {
        factor[i] = matches[i] <= 0 ? 0.001 : matches[i] / (double)targets[i];

        if (1.0 - factor[i] < min) // Wenn Abstand kleiner min, dann übernehme alten Wert
          factor[i] = ratios[i];
        else
          factor[i] = ratios[i] + (1.0 - factor[i]);
      }

      // ReSharper disable once InvertIf
      if (factor.Any(x => x > 1.0))
      {
        var max = factor.Max() + min;
        for (var i = 0; i < factor.Length; i++)
          factor[i] /= max;
      }

      return factor;
    }

    private static int[] GetMatches(Dictionary<int, HashSet<int>> cooccurrences, Dictionary<int, int[]>[] baseField)
    {
      var res = new int[baseField.Length];
      for (var i = 0; i < baseField.Length; i++)
      {
        var count = 0;
        foreach (var x in baseField[i])
          if (cooccurrences.ContainsKey(x.Key))
            count += x.Value.Count(y => cooccurrences[x.Key].Contains(y));
        res[i] = count;
      }
      return res;
    }

    private static Dictionary<int, int[]> GetBaseCooccurrences(string layer, IEnumerable<Guid> selections)
    {
      var select = _all.CreateTemporary(selections);
      var block = select.CreateBlock<CooccurrenceIndexOnlyBlock>();
      block.LayerDisplayname = layer;
      block.Calculate();
      return block.CooccurrenceSignificance.ToDictionary(x => x.Key, x => x.Value.Keys.ToArray());
    }

    private static Dictionary<int, HashSet<int>> GetCooccurrences(string layer, IEnumerable<Guid> selections)
    {
      var select = _all.CreateTemporary(selections);
      var block = select.CreateBlock<CooccurrenceIndexOnlyBlock>();
      block.LayerDisplayname = layer;
      block.Calculate();
      return block.CooccurrenceSignificance.ToDictionary(x => x.Key, x => new HashSet<int>(x.Value.Keys));
    }

    private static void PrintHelp()
    {
      Console.WriteLine("./ces \"Lemma (tree_tagger)\" 0.1 10 output.log corpus1.cec6 corpus2.cec6 corpus3.cec6");
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
