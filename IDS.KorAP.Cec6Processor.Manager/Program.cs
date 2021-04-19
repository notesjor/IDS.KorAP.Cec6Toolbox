using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace IDS.KorAP.Cec6Processor.Manager
{
  class Program
  {
    static void Main(string[] args)
    {
      var max = File.ReadAllLines("catalog.tsv").Count(x => !string.IsNullOrWhiteSpace(x));
      var stopFile = "/home/ruediger/cec6processor.stop";

      do
      {
        if (File.Exists(stopFile))
          File.Delete(stopFile);

        var process = Process.Start("./IDS.KorAP.Cec6Processor");
        Thread.Sleep(5 * 60 * 1000);
        File.Create(stopFile);

        process?.WaitForExit();
      } while (Directory.GetFiles("/home/ruediger/CEC6").Length < max);
    }
  }
}
