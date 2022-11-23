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

      var mode = args[1];
      var queries = new HashSet<Guid>(File.ReadLines(args[2]).Select(Guid.Parse));
      var output = args[3];

      using (var fs = new FileStream(output, FileMode.Create, FileAccess.Write))
      using (var writer = new StreamWriter(fs, Encoding.UTF8))
        switch (mode)
        {
          case "corpus":
            SearchIndexCall(Path.Combine(input, "corpus"), queries, writer);
            break;
          case "sigle":
            SearchIndexCall(Path.Combine(input, "sigle"), queries, writer);
            break;
          case "meta":
            SearchIndexCall(Path.Combine(input, "meta"), queries, writer);
            break;
        }
    }

    private static void SearchIndexCall(string directory, HashSet<Guid> queries, StreamWriter writer)
    {
      var lock1 = new object();
      var lock2 = new object();

      Parallel.ForEach(Directory.GetFiles(directory), file =>
      {
        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
        using (var reader = new StreamReader(fs, Encoding.UTF8))
        {
          while (!reader.EndOfStream && queries.Count > 0)
          {
            try
            {
              var line = reader.ReadLine();
              var parts = line.Split('\t');
              var guid = Guid.Parse(parts[0]);

              lock (lock1)
              {
                if (!queries.Contains(guid))
                  continue;

                queries.Remove(guid);
              }
              lock (lock2)
                writer.WriteLine(line);
            }
            catch (Exception ex)
            {
              Console.WriteLine(ex.Message);
              Console.WriteLine(ex.StackTrace);
            }
          }
        }
      });

      if (queries.Count > 0)
        foreach(var q in queries)
          writer.WriteLine($"{q}\tNOT FOUND");
    }

    private static void BuildIndex(string[] args)
    {
      var input = args[0];
      var filter = args[1];
      var output = args[2];

      var sigleDir = Path.Combine(output, "sigle");
      var metasDir = Path.Combine(output, "meta");
      var corpuDir = Path.Combine(output, "corpus");

      if (!Directory.Exists(sigleDir))
        Directory.CreateDirectory(sigleDir);
      if (!Directory.Exists(metasDir))
        Directory.CreateDirectory(metasDir);
      if (!Directory.Exists(corpuDir))
        Directory.CreateDirectory(corpuDir);

      CorpusExplorerEcosystem.InitializeMinimal();
      var files = Directory.GetFiles(input, filter, SearchOption.TopDirectoryOnly);

      var max = files.Length;
      var cnt = 0;
      var locked = new object();

      Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 40 }, file =>
      {
        try
        {
          using (var fsC = new FileStream(Path.Combine(corpuDir, Path.GetFileName(file)), FileMode.Create, FileAccess.Write))
          using (var fsS = new FileStream(Path.Combine(sigleDir, Path.GetFileName(file)), FileMode.Create, FileAccess.Write))
          using (var fsM = new FileStream(Path.Combine(metasDir, Path.GetFileName(file)), FileMode.Create, FileAccess.Write))
          using (var wC = new StreamWriter(fsC, Encoding.UTF8))
          using (var wS = new StreamWriter(fsS, Encoding.UTF8))
          using (var wM = new StreamWriter(fsM, Encoding.UTF8))
          using (var corpus = CorpusAdapterWriteIndirect.Create(file))
            foreach (var meta in corpus.DocumentMetadata)
            {
              var guid = meta.Key;
              wC.WriteLine($"{guid}\t{Path.GetFileName(file)}");

              if (meta.Value == null)
              {
                var se = $"{guid}\tNONE";
                wS.WriteLine(se);
                wM.WriteLine(se);
              }
              else
              {
                wS.WriteLine(meta.Value.ContainsKey("ZipPath")
                ? $"{guid}\t{meta.Value["ZipPath"]}"
                : $"{guid}\tNONE");

                var m = meta.Value;
                if (m.ContainsKey("Taxonomy"))
                  m.Remove("Taxonomy");
                wM.WriteLine($"{guid}\t{JsonConvert.SerializeObject(m).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Replace("  ", " ").Replace("  ", " ").Replace("  ", " ")}");
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
