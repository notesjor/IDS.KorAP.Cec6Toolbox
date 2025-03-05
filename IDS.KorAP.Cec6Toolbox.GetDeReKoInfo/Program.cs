using Newtonsoft.Json;
using System.Text;

namespace IDS.KorAP.Cec6Toolbox.GetDeReKoInfo
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var info = Download();
      using (var fs = new FileStream("dereko.tsv", FileMode.Create, FileAccess.Write))
      using (var writer = new StreamWriter(fs, Encoding.UTF8))
      {
        writer.WriteLine("Sigle\tName\tTokens\tvon\tbis\tlicence");
        foreach (var korpus in info)
        {
          writer.WriteLine($"{korpus.Sigle}\t{korpus.Name}\t{korpus.Tokens}\t{korpus.von}\t{korpus.bis}\t{korpus.license}");
        }
      }
    }

    private static Korpus[] Download()
    {
      using (var client = new HttpClient())
      {
        var req = client.GetStringAsync("https://www.ids-mannheim.de/fileadmin/kl/misc/data/basename.json");
        req.Wait();
        return JsonConvert.DeserializeObject<Korpus[]>(req.Result);
      }
    }

    private class Korpus
    {
      public string Name { get; set; }
      public string Sigle { get; set; }
      public string Tokens { get; set; }
      public string von { get; set; }
      public string bis { get; set; }
      public string license { get; set; }
    }
  }
}
