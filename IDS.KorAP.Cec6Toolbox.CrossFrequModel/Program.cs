using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Blocks;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using CorpusExplorer.Sdk.Model.Extension;
using CorpusExplorer.Sdk.Utils.CorpusManipulation;
using CorpusExplorer.Sdk.Utils.DocumentProcessing.Importer.CorpusExplorerV6;

namespace IDS.KorAP.Cec6Toolbox.CrossFrequModel
{
  class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var lexicon = GetLexicon(args[0]);
      var pathInput = args[1];
      var parallelMax = int.Parse(args[2]);
      var pathOutput = args[3];

      var dirs = Directory.GetDirectories(pathInput);
      Parallel.ForEach(dirs, new ParallelOptions { MaxDegreeOfParallelism = parallelMax }, dir =>
      {
        var name = dir.Replace(pathInput, "").Replace("/", "");

        var files = Directory.GetFiles(dir, "*.cec6.gz");
        using (var corpus = CorpusMerger.Merge(files, new ImporterCec6()) as CorpusAdapterWriteDirect)
        {
          var select = corpus.ToSelection();
          var block = select.CreateBlock<CrossFrequencyBlock>();
          block.ProtectMemoryOverflow = true;
          block.LayerDisplayname = "Lemma";
          block.Calculate();


        }
      });
    }

    private static Dictionary<string, int> GetLexicon(string path)
    {
      var res = new Dictionary<string, int>();
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
      using (var buffer = new BufferedStream(fs, 4096))
      using (var reader = new StreamReader(buffer, Encoding.UTF8))
      {
        while (!reader.EndOfStream)
        {
          var line = reader.ReadLine();
          if (line.StartsWith("#"))
            continue;

          var split = line.Split(' ');
          if (split.Length < 2)
            continue;

          var query = split[0];
          if (query.Contains("("))
          {
            split = query.Split(new[] { "(", ",", ")" }, StringSplitOptions.RemoveEmptyEntries);
            AddSafe(ref res, split[0]);
            for (var i = 1; i < split.Length; i++)
            {
              AddSafe(ref res, split[0] + split[i]);
            }
          }
          else if (query.Contains(","))
          {

          }
          else
            AddSafe(ref res, query);
        }
      }
    }

    private static void AddSafe(ref Dictionary<string, int> dict, string token)
    {
      if (dict.ContainsKey(token))
        return;
      dict.Add(token, dict.Count);
    }
  }
}
