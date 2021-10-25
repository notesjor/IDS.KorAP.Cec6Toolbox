using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CorpusExplorer.Sdk.Helper;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature.Abstract;
using IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature.Tagger.Abstract;

namespace IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature
{
  public class FeatureAddAnnotationByApp : AbstractFeatureTagger
  {
    protected override void ProcessCorpus(string basePath, string baseFile, string tmpPath, string[] tmpFiles)
    {
      throw new NotImplementedException();
    }
  }
}
