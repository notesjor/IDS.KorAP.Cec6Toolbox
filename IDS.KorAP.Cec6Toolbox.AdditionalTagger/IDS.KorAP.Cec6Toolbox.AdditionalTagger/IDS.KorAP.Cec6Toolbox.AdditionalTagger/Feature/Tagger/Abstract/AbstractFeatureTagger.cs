using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Helper;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature.Abstract;

namespace IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature.Tagger.Abstract
{
  public abstract class AbstractFeatureTagger : AbstractFeature
  {
    protected string _basePathTagger = "/home/ruediger/TOOLBOX/TAGGER";

    public override void Execute(string option, string basePath, string[] files)
    {
      foreach (var file in files)
      {
        var tmpPath = CreateTempPath(basePath);

        try
        {
          var corpus = CorpusAdapterWriteDirect.Create(file);
          Parallel.ForEach(corpus.DocumentGuids, (dsel) =>
          {
            File.WriteAllText(Path.Combine(tmpPath, $"{dsel:N}.txt"), corpus.GetReadableDocument(dsel, "Wort").ReduceDocumentToText(tokenSeparator: "\r\n"));
          });

          ProcessCorpus(basePath, file, tmpPath, Directory.GetFiles(tmpPath, "*.txt"));
        }
        catch
        {
          // ignore
        }

        DeleteTempPath(tmpPath);
      }
    }

    protected abstract void ProcessCorpus(string basePath, string baseFile, string tmpPath, string[] tmpFiles);

    private string CreateTempPath(string basePath)
    {
      var res = Path.Combine(basePath, "ceTMP");
      DeleteTempPath(res);
      Directory.CreateDirectory(res);
      return res;
    }

    private void DeleteTempPath(string tmpPath)
    {
      if (Directory.Exists(tmpPath))
        Directory.Delete(tmpPath, true);
    }
  }
}
