using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IDS.KorAP.Cec6Toolbox.Corpuslist2Index
{
  public class CorpuslistIndex
  {
    private Dictionary<string, int> _c2i = new Dictionary<string, int>();
    private Dictionary<int, string> _i2c = new Dictionary<int, string>();

    public CorpuslistIndex(string pathIndex)
    {
      Load(pathIndex);
    }

    public CorpuslistIndex(string pathIndex, IEnumerable<string> pathCorpora)
    {
      if (File.Exists(pathIndex))
        Load(pathIndex);
      if (Add(pathCorpora))
        Save(pathIndex);
    }

    public IEnumerable<string> Corpora => _c2i.Keys;
    public IEnumerable<string> FindCorpus(string match) => _c2i.Keys.Where(x => x.Contains(match));
    public IEnumerable<string> FindCorpus(Regex match) => _c2i.Keys.Where(x => match.IsMatch(x));

    public bool Contains(string corpus) => _c2i.ContainsKey(corpus);
    public bool Contains(int index) => _i2c.ContainsKey(index);

    public int this[string corpus] => _c2i.ContainsKey(corpus) ? _c2i[corpus] : -1;
    public string this[int index] => _i2c.ContainsKey(index) ? _i2c[index] : null;

    private void Save(string pathIndex)
    {
      using (var fs = new FileStream(pathIndex, FileMode.Create, FileAccess.Write))
      using (var buffer = new BufferedStream(fs, 4096))
      using (var writer = new StreamWriter(buffer, Encoding.UTF8))
      {
        foreach (var x in _i2c)
          writer.WriteLine($"{x.Key}\t{x.Value}");
      }
    }

    private bool Add(IEnumerable<string> pathCorpora)
    {
      var res = false;
      foreach (var dir in pathCorpora)
      {
        var files = Directory.GetFiles(dir, "*.cec6.gz", SearchOption.AllDirectories);
        foreach (var file in files)
        {
          if (_c2i.ContainsKey(file))
            continue;

          var idx = _c2i.Count;
          _c2i.Add(file, idx);
          _i2c.Add(idx, file);

          res = true;

          var parent = Path.GetDirectoryName(file);
          if (_c2i.ContainsKey(parent))
            continue;

          idx = _c2i.Count;
          _c2i.Add(parent, idx);
          _i2c.Add(idx, parent);
        }
      }

      return res;
    }

    private void Load(string pathIndex)
    {
      using (var fs = new FileStream(pathIndex, FileMode.Open, FileAccess.Read))
      using (var buffer = new BufferedStream(fs, 4096))
      using (var reader = new StreamReader(buffer, Encoding.UTF8))
      {
        while (!reader.EndOfStream)
        {
          var entry = reader.ReadLine().Split('\t', StringSplitOptions.RemoveEmptyEntries);
          if (entry.Length != 2)
            continue;

          var idx = int.Parse(entry[0]);
          var val = entry[1];

          _c2i.Add(val, idx);
          _i2c.Add(idx, val);
        }
      }
    }
  }
}
