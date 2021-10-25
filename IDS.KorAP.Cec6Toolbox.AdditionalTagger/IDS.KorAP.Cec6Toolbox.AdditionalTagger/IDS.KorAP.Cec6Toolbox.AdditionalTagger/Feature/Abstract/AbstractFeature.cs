using System.IO;

namespace IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature.Abstract
{
  public abstract class AbstractFeature
  {
    public abstract void Execute(string option, string basePath, string[] files);
  }
}
