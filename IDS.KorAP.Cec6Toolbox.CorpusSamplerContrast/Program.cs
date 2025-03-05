using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;

namespace IDS.KorAP.Cec6Toolbox.CorpusSamplerContrast
{
  internal class Program
  {
    static Random _rng = new Random();

    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      if (args.Length != 4)
      {
        PrintHelp();
        return;
      }

      var input = args[0];
      var filter = args[1];
      var r = int.Parse(args[2]);
      var output = args[3];

      if (!Directory.Exists(output))
        Directory.CreateDirectory(output);

      var filesFilter = Directory.GetFiles(filter, "*.cec6");
      var filesInput = filesFilter.Select(f => Path.Combine(input, Path.GetFileName(f))).Where(File.Exists).ToArray();
      if (filesFilter.Length != filesInput.Length)
      {
        Console.WriteLine("Filter and input files do not match");
        return;
      }

      var reportLock = new object();

      Parallel.For(0, filesFilter.Length, new ParallelOptions { MaxDegreeOfParallelism = 20 }, i =>
      {
        var fFilter = filesFilter[i];
        var fInput = filesInput[i];

        if (Path.GetFileNameWithoutExtension(fFilter) != Path.GetFileNameWithoutExtension(fInput))
        {
          Console.WriteLine("Filter and input files do not match");
          throw new Exception("Filter and input files do not match");
        }

        var fOutput = Path.Combine(output, Path.GetFileName(fInput));

        var cFilter = CorpusAdapterWriteDirect.Create(fFilter);
        var lFilter = cFilter.GetLayers("Wort").First();
        var dFilter = cFilter.DocumentGuids.ToDictionary(g => g, g => lFilter[g].Sum(x => x.Length));
        cFilter.Dispose();

        GetMetrics(dFilter, out var min, out var max);
        var ranges = GenerateRanges(min, r, max);
        dFilter = ConvertToRange(dFilter, ranges);
        var rangeCount = CountRanges(ranges, dFilter);

        var cInput = CorpusAdapterWriteDirect.Create(fInput);
        var tmp = new HashSet<Guid>(cInput.DocumentGuids);
        foreach (var x in dFilter.Keys.ToArray())
          if (tmp.Contains(x))
            dFilter.Remove(x);
        var dInput = ConvertToRange(tmp.ToDictionary(g => g, g => lFilter[g].Sum(x => x.Length)), ranges).ToList();
        dInput = FisherYatesShuffel(dInput);

        lock (reportLock)
        {

        }
      });
    }

    private static List<KeyValuePair<Guid, int>> FisherYatesShuffel(List<KeyValuePair<Guid, int>> dInput)
    {
      for (var i = dInput.Count - 1; i > 0; i--)
      {
        var j = _rng.Next(i + 1);
        (dInput[i], dInput[j]) = (dInput[j], dInput[i]);
      }
      return dInput;
    }

    private static Dictionary<int, int> CountRanges(KeyValuePair<int, int>[] ranges, Dictionary<Guid, int> dInput)
    {
      // init
      var res = new Dictionary<int, int>();
      for (var i = 0; i < ranges.Length; i++)
        res[i] = 0;

      // count
      foreach (var key in dInput.Keys)
        res[dInput[key]]++;

      return res;
    }

    private static Dictionary<Guid, int> ConvertToRange(Dictionary<Guid, int> dInput, KeyValuePair<int, int>[] ranges)
    {
      foreach (var key in dInput.Keys.ToArray())
        for (var i = 0; i < ranges.Length; i++)
        {
          var r = ranges[i];
          if (dInput[key] <= r.Key || dInput[key] >= r.Value)
            continue;
          dInput[key] = i;
          break;
        }

      return dInput;
    }

    private static void GetMetrics(Dictionary<Guid, int> dInput, out int min, out int max)
    {
      min = dInput.Values.Min();
      if (min < 100)
        min = 100;
      max = dInput.Values.Max();
      if (max < min)
        max = min + 100;
    }

    private static KeyValuePair<int, int>[] GenerateRanges(int min, int r, int max)
    {
      var ranges = new List<KeyValuePair<int, int>>();
      var step = (max - min) / r;
      for (var j = 0; j < r; j++)
      {
        ranges.Add(new KeyValuePair<int, int>(min, min + step));
        min += step;
      }

      return ranges.ToArray();
    }

    private static void PrintHelp()
    {
      Console.WriteLine("Usage: CorpusSamplerContrast <input> <filter> <ranges> <output>");
      Console.WriteLine("  <input>   Input directory with CEC6 files");
      Console.WriteLine("  <filter>  Filter directory with CEC6 files");
      Console.WriteLine("  <ranges>  Number of ranges");
      Console.WriteLine("  <output>  Output directory");
    }
  }
}
