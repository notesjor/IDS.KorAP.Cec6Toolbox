using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using System.Text;
using Newtonsoft.Json;

namespace IDS.KorAP.Cec6Toolbox.GuidIndex
{
  internal class Program
  {
    static void Main(string[] args)
    {
      if (args.Length == 3) // input (CEC6.gz), filter (*.cec6) and output (idx-File-Dir)
        BuildIndex(args);
      else if (args.Length == 4) // 1: input (idx-File-Dir), 2: mode (corpus/sigle/meta), 3: query-file (guids) and 4: output-folder
        SearchIndex(args);
    }

    private static void SearchIndex(string[] args)
    {
      var input = args[0];
      var sigleFile = Path.Combine(input, "sigle.idx");
      var metasFile = Path.Combine(input, "meta.idx");
      var corpuFile = Path.Combine(input, "corpus.idx");

      if (!File.Exists(sigleFile) || !File.Exists(metasFile))
        return;

      var mode = args[1];
      var queries = new HashSet<Guid>(File.ReadLines(args[2]).Select(Guid.Parse));
      var output = args[3];

      using (var fs = new FileStream(output, FileMode.Create, FileAccess.Write))
      using (var writer = new StreamWriter(fs, Encoding.UTF8))
        switch (mode)
        {
          case "corpus":
            SearchIndexCall(corpuFile, queries, writer);
            break;
          case "sigle":
            SearchIndexCall(sigleFile, queries, writer);
            break;
          case "meta":
            SearchIndexCall(metasFile, queries, writer);
            break;
        }
    }

    private static void SearchIndexCall(string indexFile, HashSet<Guid> queries, StreamWriter writer)
    {
      using (var fs = new FileStream(indexFile, FileMode.Open, FileAccess.Read))
      using (var reader = new StreamReader(fs, Encoding.UTF8))
      {
        while (!reader.EndOfStream && queries.Count > 0)
        {
          try
          {
            var line = reader.ReadLine();
            var parts = line.Split('\t');
            var guid = Guid.Parse(parts[0]);

            if (!queries.Contains(guid))
              continue;

            queries.Remove(guid);
            writer.WriteLine(line); // Schreibt komplette LINE (SIGLE oder META)
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
          }
        }
      }
    }

    private static void BuildIndex(string[] args)
    {
      var input = args[0];
      var filter = args[1];
      var output = args[2];

      var sigleFile = Path.Combine(output, "sigle.idx");
      var metasFile = Path.Combine(output, "meta.idx");
      var corpusFile = Path.Combine(output, "corpus.idx");

      CorpusExplorerEcosystem.InitializeMinimal();
      var files = Directory.GetFiles(input, filter, SearchOption.TopDirectoryOnly);

      var max = files.Length;
      var cnt = 0;
      var locked = new object();

      var l1 = new object();
      var l2 = new object();
      var l3 = new object();

      using (var fs1 = new FileStream(corpusFile, FileMode.Create, FileAccess.Write))
      using (var fs2 = new FileStream(sigleFile, FileMode.Create, FileAccess.Write))
      using (var fs3 = new FileStream(metasFile, FileMode.Create, FileAccess.Write))
      using (var w1 = new StreamWriter(fs1, Encoding.UTF8))
      using (var w2 = new StreamWriter(fs2, Encoding.UTF8))
      using (var w3 = new StreamWriter(fs3, Encoding.UTF8))
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 20 }, file =>
        {
          try
          {
            using (var corpus = CorpusAdapterWriteDirect.Create(file))
              foreach (var meta in corpus.DocumentMetadata)
              {
                var guid = meta.Key;
                var s1 = $"{guid}\t{Path.GetFileName(file)}";
                lock (l1)
                  w1.WriteLine(s1);

                if (meta.Value == null)
                {
                  var se = $"{guid}\tNONE-SEE-CORPUS";
                  lock (l2)
                    w2.WriteLine(se);
                  lock (l3)
                    w3.WriteLine(se);
                }
                else
                {
                  var s2 = meta.Value.ContainsKey("ZipPath")
                    ? $"{guid}\t{meta.Value["ZipPath"]}"
                    : $"{guid}\tNONE-SEE-CORPUS";
                  lock (l2)
                    w2.WriteLine(s2);

                  var s3 = $"{guid}\t{JsonConvert.SerializeObject(meta.Value).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Replace("  ", " ").Replace("  ", " ").Replace("  ", " ")}";
                  lock (l3)
                    w3.WriteLine(s3);
                }
              }
            lock (locked)
            {
              cnt++;
              Console.WriteLine($"{cnt}/{max}");
            }
          }
          catch (Exception ex)
          {
            lock (locked)
            {
              Console.WriteLine(ex.Message);
              Console.WriteLine(ex.StackTrace);
            }
          }
        });
    }
  }
}
