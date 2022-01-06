using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace IDS.KorAP.Cec6Toolbox.SimpleCounter.Sum
{
  class Program
  {
    static void Main(string[] args)
    {
      var path = args[0];
      var files = Directory.GetFiles(path, "*.sum");
      var outputPathTotal = args[0] + ".total";
      var outputPathD2I = args[0] + ".d2i";

      var dates = GetSortedDates(files, out var sortedArray);
      var index = GetIndex(sortedArray);

      File.WriteAllLines(outputPathD2I, index.Select(x => $"{x.Key:yyyy-MM-dd}\t{x.Value}"));

      using (var fs = new FileStream(outputPathTotal, FileMode.Create, FileAccess.Write))
      using (var buffer = new BufferedStream(fs))
        foreach (var x in dates)
        {
          var b = new List<byte>();
          b.AddRange(BitConverter.GetBytes(x.Key.Ticks));
          b.AddRange(BitConverter.GetBytes(dates[x.Key]));
          b.Add((byte)'\n');

          var arr = b.ToArray();
          buffer.Write(arr, 0, arr.Length);
        }
    }

    private static Dictionary<DateTime, int> GetIndex(KeyValuePair<DateTime, float>[] sortedArray)
    {
      var index = new Dictionary<DateTime, int>();
      foreach (var pair in sortedArray)
        index.Add(pair.Key, index.Count);
      return index;
    }

    private static Dictionary<DateTime, float> GetSortedDates(string[] files, out KeyValuePair<DateTime, float>[] sortedArray)
    {
      var tmp = new Dictionary<DateTime, float>();
      foreach (var file in files)
      {
        var val = float.Parse(File.ReadAllText(file));
        if (val < 0)
          Console.WriteLine($"overflow in {file} - val: {val}");

        var date = DateTime.ParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", CultureInfo.CurrentCulture);
        tmp.Add(date, val);
      }

      sortedArray = tmp.OrderBy(x => x.Key).ToArray();
      return tmp;
    }
  }
}
