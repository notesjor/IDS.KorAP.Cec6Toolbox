using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP;
using CorpusExplorer.Sdk.Extern.Xml.Ids.KorAP.LoadStrategy;

namespace IDS.KorAP.Cec6Processor.Thread
{
  class Program
  {
    static void Main(string[] args)
    {
      var dir = "/home/ruediger/PROCESSOR/error/"; // siehe IDS.KorAP.Cec6Processor > Program.cs
      var debug = File.Exists(Path.Combine(dir, "debug.flag"));
      CorpusExplorerEcosystem.InitializeMinimal();

      try
      {
        var importer = new ImporterKorap
        {
          Strategy = args[0].EndsWith(".zip") ? new LoadStrategyMemory() : new LoadStrategyExtractedDirectory(), 
          Debug = debug,
          ImportCoreNlp = false,
          ImportMalt = false,
          ImportMarmot = false,
          ImportOpenNlp = false,
          ImportTreeTagger = true
        };
        var corpus = importer.Execute(new[] { args[0] }).FirstOrDefault();
        corpus?.Save(args[1], true);
        if (corpus is IDisposable c)
          c.Dispose();

        if (debug && importer.DebugLog.Any())
          File.WriteAllLines(Path.Combine(dir, Path.GetFileName(args[1])), importer.DebugLog.Select(x => $"{x.Message}\r\n{x.StackTrace}\r\n---\r\n"), Encoding.UTF8);

        importer.Dispose();
      }
      catch (Exception ex)
      {
        try
        {
          File.WriteAllText(Path.Combine(dir, Path.GetFileName(args[1])), $"###MASSIVER FEHLER###\r\n{ex.Message}\r\n---\r\n{ex.StackTrace}\r\n###");
        }
        catch
        {
          // ignore
        }
      }
    }
  }
}
