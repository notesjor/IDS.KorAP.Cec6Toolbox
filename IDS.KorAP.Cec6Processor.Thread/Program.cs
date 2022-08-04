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
      var dir = "/home/ruediger/TOOLBOX/PROCESSOR/error/"; // siehe IDS.KorAP.Cec6Processor > Program.cs
      var debug = File.Exists(Path.Combine(dir, "debug.flag"));
      CorpusExplorerEcosystem.InitializeMinimal();

      try
      {
        var importer = new ImporterKorap
        {
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
          File.AppendAllLines(Path.Combine(dir, Path.GetFileName(args[1])), importer.DebugLog.Select(x => $"{x.Message}\r\n{x.StackTrace}\r\n---\r\n"), Encoding.UTF8);

        importer.Dispose();
      }
      catch (Exception ex)
      {
        try
        {
          File.AppendAllText(Path.Combine(dir, Path.GetFileName(args[1])), "###MASSIVER FEHLER######");
          RecursiveWriteExecptionLog(dir, Path.GetFileName(args[1]), ex);
        }
        catch
        {
          // ignore
        }
      }
    }

    private static void RecursiveWriteExecptionLog(string dir, string fn, Exception ex)
    {
      File.AppendAllText(Path.Combine(dir, fn), $"\r\n{ex.Message}\r\n---\r\n{ex.StackTrace}\r\n");
      if(ex.InnerException != null)
        RecursiveWriteExecptionLog(dir, fn, ex.InnerException);
    }
  }
}
