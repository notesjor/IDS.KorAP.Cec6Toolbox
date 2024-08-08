
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace IDS.KorAP.Cec6Toolbox.MakeIndex
{
  internal class Program
  {
    private static string _basePath = "/vol/corpora/DeReKo/current/KorAP/zip";
    private static string _cec6Path = "/home/ruediger/CEC6";
    private static string _tempPath = "/home/ruediger/CEC6-TMP";
    private static string[] _corpusFilter = new[] { "wdd", "wpd", "wrd", "wud", "twi" };

    private static string[] _foundries =
    {
      "corenlp",
      "malt",
      "marmot",
      "opennlp",
      "tree_tagger",
      "spacy",
      "i5",
      "cmc",
      "corpusexplorer"
    };

    static void Main(string[] args)
    {
      try
      {
        var command = args[0];
        switch (command)
        {
          case "foundry":
            ListAdditionalFoundries();
            break;
          case "tt":
            ListMissingTreeTagger();
            break;
          case "tt-sync":
            StartTreeTaggerForMissing();
            return;
          case "update":
            ListMissingUpdates();
            break;
          case "prepare-upgrade":
            PrepareUpgrade();
            return;
          case "upgrade":
            ProcessUpgrade();
            return;
        }
      }
      catch
      {
        PrintHelp();
      }
    }

    private static void ProcessUpgrade()
    {
      var outPath = "/home/ruediger/CEC6-NEW";
      if (!Directory.Exists(outPath))
        Directory.CreateDirectory(outPath);

      var files = Directory.GetFiles(_tempPath, "*.tree_tagger.zip");
      Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 10 }, file =>
      {
        try
        {
          var fn = Path.GetFileName(file).Replace(".tree_tagger.zip", "");
          var outputFile = $"{outPath}/{fn}.cec6";
          if (File.Exists(outputFile))
            return;

          var process = Process.Start(new ProcessStartInfo
          {
            FileName = "dotnet",
            Arguments = $"/home/ruediger/CorpusExplorer/cec.dll import#Korap2021#{_tempPath}/{fn}.zip convert Cec6#{outputFile}",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
          });
          process?.WaitForExit();
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
          Console.WriteLine(ex.StackTrace);
        }
      });
    }

    private static void PrepareUpgrade()
    {
      if (Directory.Exists(_tempPath))
        Directory.Delete(_tempPath, true);

      Directory.CreateDirectory(_tempPath);

      var missing = ListMissingUpdates();
      Parallel.ForEach(missing, new ParallelOptions { MaxDegreeOfParallelism = 10 }, entry =>
      {
        foreach (var f in _corpusFilter)
          if (entry.StartsWith(f))
            return;

        var optionalTreeTaggerFile = Path.Combine(_basePath, $"{entry}.tree_tagger.zip");
        var target = Path.Combine(_tempPath, $"{entry}.tree_tagger.zip");
        if (!File.Exists(target))
          if (File.Exists(optionalTreeTaggerFile))
            File.Copy(optionalTreeTaggerFile, target);

        target = Path.Combine(_tempPath, $"{entry}.zip");
        if (!File.Exists(target))
          File.Copy(Path.Combine(_basePath, $"{entry}.zip"), target);
      });
    }

    private static void StartTreeTaggerForMissing()
    {
      var missing = ListMissingTreeTagger();

      Parallel.ForEach(missing, new ParallelOptions { MaxDegreeOfParallelism = 10 }, m =>
      {
        var process = Process.Start(new ProcessStartInfo
        {
          FileName = "/usr/local/kl/korap/Ingestion/treetagger/korap2tt.pl",
          Arguments = $"-D . /vol/corpora/DeReKo/current/KorAP/zip/{m}.zip",
          UseShellExecute = true,
          CreateNoWindow = true,
          WindowStyle = ProcessWindowStyle.Hidden
        });
        process?.WaitForExit();
      });
    }

    private static void PrintHelp()
    {
      Console.WriteLine("MakeIndex - OWID-Index für KorAP - by J. O. Rüdiger (2024)");
      Console.WriteLine();
      Console.WriteLine("Usage:");
      Console.WriteLine("\tMakeIndex foundry - Überprüft, ob mittlerweile andere Foundries verfügbar sind. (Ausgabe sollte LEER sein).");
      Console.WriteLine("\tMakeIndex tt - Überprüft, ob es Korpora ohne Tree-Tagger Foundry gibt. Diese können manuell angestoßen werden");
      Console.WriteLine("\t\tManuelle TreeTagger-Annotation (Beispiel):");
      Console.WriteLine("\t\t/usr/local/kl/korap/Ingestion/treetagger/korap2tt.pl -D . /vol/corpora/DeReKo/incoming/KorAP/zip/KORPUS.zip");
      Console.WriteLine("\tMakeIndex tt-sync - Führt für alle Korpora ohne TreeTagger eine Annotation durch");
      Console.WriteLine("\tMakeIndex update - Überprüft ob der OWID-Index synchron zu DeReKo ist. Setzt voraus, dass alle DeReKo-Daten Tree-Tagger annotiert sind (siehe MakeIndex tt / tt-sync).");
      Console.WriteLine("\tMakeIndex prepate-upgrade - Bereitet Upgrade des OWID-Index durch. Setzt voraus, dass alle DeReKo-Daten Tree-Tagger annotiert sind (siehe MakeIndex tt / tt-sync). Kopiert die Daten in CEC6-TEMP. Ggf. müssen von tt-sync in CEC6-TEMP manuell kopiert werden.");
      Console.WriteLine("\tMakeIndex upgrade - Führt das Upgrade des OWID-Index durch.");
    }

    private static List<string> ListMissingUpdates()
    {
      var zips = Directory.GetFiles(_basePath, "*.zip").Select(x => Path.GetFileName(x));
      var cec6 = Directory.GetFiles(_cec6Path, "*.cec6").Select(x => Path.GetFileNameWithoutExtension(x));

      var res = new List<string>();
      foreach (var z in zips)
      {
        foreach (var f in _corpusFilter)
          if (z.StartsWith(f))
            continue;

        var split = z.Split('.');
        if (split.Length != 2)
          continue;
        var name = split[0];
        if (!cec6.Contains(name))
          res.Add(name);
      }

      foreach (var x in res)
        Console.WriteLine(x);

      return res;
    }

    private static List<string> ListMissingTreeTagger()
    {
      var files = Directory.GetFiles(_basePath, "*.zip").Select(x => Path.GetFileName(x));
      var tt = new HashSet<string>();
      var zf = new HashSet<string>();

      foreach (var file in files)
      {
        var split = file.Split(".", StringSplitOptions.RemoveEmptyEntries);
        if ((split.Length > 2) && (split[1] == "tree_tagger"))
          tt.Add(split[0]);

        foreach (var f in _corpusFilter)
          if (split[0].StartsWith(f))
            continue;

        if (split.Length == 2)
          zf.Add(split[0]);
      }

      var res = new List<string>();
      foreach (var x in zf)
        if (!tt.Contains(x))
          res.Add(x);

      foreach (var x in res)
        Console.WriteLine(x);

      return res;
    }

    private static void ListAdditionalFoundries()
    {
      var files = Directory.GetFiles(_basePath, "*.zip").Select(x => Path.GetFileName(x));

      var res = new HashSet<string>();
      foreach (var file in files)
      {
        var split = file.Split(".", StringSplitOptions.RemoveEmptyEntries);
        if (split.Length > 2)
          res.Add(split[1]);
      }

      foreach (var x in _foundries)
        res.Remove(x);

      foreach (var foundry in res)
        Console.WriteLine(foundry);
    }
  }
}
