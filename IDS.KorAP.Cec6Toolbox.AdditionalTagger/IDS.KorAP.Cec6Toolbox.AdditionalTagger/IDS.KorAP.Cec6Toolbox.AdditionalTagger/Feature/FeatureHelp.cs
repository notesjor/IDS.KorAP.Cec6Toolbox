using System;
using IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature.Abstract;

namespace IDS.KorAP.Cec6Toolbox.AdditionalTagger.Feature
{
  public class FeatureHelp : AbstractFeature
  {
    public override void Execute(string option, string basePath, string[] files)
    {
      Console.WriteLine("Arguments: [mode] [mode-option] [directory]");
      Console.WriteLine();
      Console.WriteLine("[mode] = app OR korap");
      Console.WriteLine();
      Console.WriteLine("[mode] = app -> use tagger for additional annotations");
      Console.WriteLine("\tmode-options:");
      Console.WriteLine("\t\trtf = RTFTagger (path: /home/ruediger/TOOLBOX/TAGGER/RFTagger/)");
      Console.WriteLine("\t\ttt = TreeTagger (path: /home/ruediger/TOOLBOX/TAGGER/TreeTagger/)");
      Console.WriteLine();
      Console.WriteLine("[mode] = korap -> use KorAP files for additional annoations");
      Console.WriteLine("\tmode-options: corenlp, malt, marmot, opennlp, tree_tagger");
      Console.WriteLine();
    }
  }
}
