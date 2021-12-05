using System;
using System.Linq;

namespace IDS.KorAP.Cec6Toolbox.Corpuslist2Index
{
  class Program
  {
    static void Main(string[] args)
    {
      var argList = args.ToList();
      var list = args.First();
      argList.RemoveAt(0);

      new CorpuslistIndex(list, argList);
    }
  }
}
