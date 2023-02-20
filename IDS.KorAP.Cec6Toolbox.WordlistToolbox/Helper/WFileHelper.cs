using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDS.KorAP.Cec6Toolbox.WordlistToolbox.Helper
{
  public static class WFileHelper
  {
    public static Dictionary<string, int> ReadDictionary(string path)
    {
      Console.WriteLine("Load Dictionary...");
      var ts = DateTime.Now;

      var size = int.Parse(File.ReadAllText(path + ".idx"));
      var res = new Dictionary<string, int>(size);
      var idx = 0;

      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
      using (var buffer = new BufferedStream(fs, 100000000))
      using (var reader = new StreamReader(buffer, new UTF8Encoding(false)))
        while (!reader.EndOfStream)
          res.Add(reader.ReadLine(), idx++);

      Console.WriteLine($"Load Dictionary [{(DateTime.Now - ts).TotalSeconds}]... [{res.Count}/{size}]");
      return res;
    }

    public static Dictionary<string, int> ReadDictionary(string path, IEnumerable<string> filter)
    {
      var hash = new HashSet<string>(filter);

      Console.WriteLine("Load Dictionary...");
      var ts = DateTime.Now;

      var res = new Dictionary<string, int>(hash.Count);
      var idx = 0;

      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
      using (var buffer = new BufferedStream(fs, 100000000))
      using (var reader = new StreamReader(buffer, new UTF8Encoding(false)))
        while (!reader.EndOfStream)
        {
          var line = reader.ReadLine();
          if (hash.Contains(line))
            res.Add(line, idx++);
        }

      Console.WriteLine($"Load Dictionary [{(DateTime.Now - ts).TotalSeconds}]... [{res.Count}/{hash.Count}]");
      return res;
    }
  }
}
