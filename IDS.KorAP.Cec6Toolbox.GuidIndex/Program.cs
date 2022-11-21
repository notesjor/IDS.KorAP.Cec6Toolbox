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
      if (args.Length == 2) // input (CEC6.gz) and output (idx-File-Dir)
        BuildIndex(args);
      else if (args.Length == 4) // 1: input (idx-File-Dir), 2: mode (corpus/sigle/meta), 3: query-file (guids) and 4: output-folder
        SearchIndex(args);
    }

    private static void SearchIndex(string[] args)
    {
      var input = args[0];
      var sigleFile = Path.Combine(input, "sigle.idx");
      var metasFile = Path.Combine(input, "meta.idx");

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
            SearchCorpus(sigleFile, queries, writer);
            break;
          case "sigle":
            SearchSigle(sigleFile, queries, writer); // Schreibt SIGLE
            break;
          case "meta":
            SearchSigle(metasFile, queries, writer); // Schreibt META
            break;
        }
    }

    private static void SearchCorpus(string sigleFile, HashSet<Guid> queries, StreamWriter writer)
    {
      using (var fs = new FileStream(sigleFile, FileMode.Open, FileAccess.Read))
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
            var idx = parts[1].IndexOf("/");
            writer.WriteLine($"{guid}\t{parts[1].Substring(0, idx).Replace("/", "").ToLower()}.cec6.gz");
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
          }
        }
      }
    }

    private static void SearchSigle(string sigleFile, HashSet<Guid> queries, StreamWriter writer)
    {
      using (var fs = new FileStream(sigleFile, FileMode.Open, FileAccess.Read))
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
      var output = args[1];

      var sigleFile = Path.Combine(output, "sigle.idx");
      var metasFile = Path.Combine(output, "meta.idx");

      CorpusExplorerEcosystem.InitializeMinimal();
      var files = Directory.GetFiles(input, "*.cec6.gz", SearchOption.TopDirectoryOnly);

      var max = files.Length;
      var cnt = 0;
      var locked = new object();

      using (var fs1 = new FileStream(sigleFile, FileMode.Create, FileAccess.Write))
      using (var fs2 = new FileStream(metasFile, FileMode.Create, FileAccess.Write))
      using (var w1 = new StreamWriter(fs1, Encoding.UTF8))
      using (var w2 = new StreamWriter(fs2, Encoding.UTF8))
        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 20 }, file =>
        {
          try
          {
            using (var corpus = CorpusAdapterWriteDirect.Create(file))
              foreach (var meta in corpus.DocumentMetadata)
              {
                var guid = meta.Key;
                var sigle = meta.Value["Sigle"].ToString();

                w1.WriteLine($"{guid}\t{sigle}");
                w2.WriteLine($"{guid}\t{JsonConvert.SerializeObject(meta.Value).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Replace("  ", " ").Replace("  ", " ").Replace("  ", " ")}");
              }
            lock (locked)
            {
              cnt++;
              Console.WriteLine($"{cnt}/{max}");
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
          }
        });
    }
  }
}
