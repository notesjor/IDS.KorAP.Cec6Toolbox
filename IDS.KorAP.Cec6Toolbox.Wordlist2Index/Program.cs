using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CorpusExplorer.Sdk.Db.RocksDb;

namespace IDS.KorAP.Cec6Toolbox.Wordlist2Index
{
  class Program
  {
    static void Main(string[] args)
    {
      var w2i = new EasyRocksDb("w2i", true);
      var i2w = new EasyRocksDb("i2w", true);

      var idx = 0;

      var t2i = new Dictionary<string, string>();
      var i2t = new Dictionary<string, string>();

      var max = 10000;

      using (var fs = new FileStream(args[0], FileMode.Open, FileAccess.Read))
      using (var buffer = new BufferedStream(fs, 4096))
      using (var reader = new StreamReader(buffer, Encoding.UTF8))
      {
        while (!reader.EndOfStream)
        {
          var token = reader.ReadLine();
          t2i.Add(token, idx.ToString());
          i2t.Add(idx.ToString(), token);
          idx++;

          if (t2i.Count < max) 
            continue;

          w2i.PutBatch(t2i);
          i2w.PutBatch(i2t);

          t2i.Clear();
          i2t.Clear();
        }
      }

      if (t2i.Count > 0)
      {
        w2i.PutBatch(t2i);
        i2w.PutBatch(i2t);

        t2i.Clear();
        i2t.Clear();
      }

      w2i.Dispose();
      i2w.Dispose();
    }
  }
}
