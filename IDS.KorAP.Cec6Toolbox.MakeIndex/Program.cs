
namespace IDS.KorAP.Cec6Toolbox.MakeIndex
{
  internal class Program
  {
    private static string _basePath = "/vol/corpora/DeReKo/current/KorAP/zip";
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
      var command = args[0];
      switch (command)
      {
        case "foundry":
          ListAdditionalFoundries();
          break;
        case "tt":
          ListMissingTreeTagger();
          break;
      }
    }

    private static void ListMissingTreeTagger()
    {
      var files = Directory.GetFiles(_basePath, "*.zip").Select(x => Path.GetFileName(x));
      var tt = new HashSet<string>();
      var zf = new HashSet<string>();

      foreach (var file in files)
      {
        var split = file.Split(".", StringSplitOptions.RemoveEmptyEntries);
        if ((split.Length > 2) && (split[1] == "tree_tagger"))
          tt.Add(split[0]);
        if (split.Length == 2)
          zf.Add(split[0]);
      }

      var res = new List<string>();
      foreach (var x in zf)
        if (!tt.Contains(x))
          res.Add(x);

      foreach (var x in res)
        Console.WriteLine(x);
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
