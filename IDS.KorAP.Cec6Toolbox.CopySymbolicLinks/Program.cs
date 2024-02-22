using System.Diagnostics;

namespace IDS.KorAP.Cec6Toolbox.CopySymbolicLinks
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var input = args[0];
      var filter = args[1];
      var output = args[2];

      Directory.SetCurrentDirectory(output);

      if(filter.StartsWith("FILE:"))
        Mode_File(input, filter, output);
      else
        Mode_Direct(input, filter, output);
    }

    private static void Mode_File(string input, string filter, string output)
    {
      var lines = Mode_File_Preprocess(filter);
      foreach (var x in lines)
      {
        try
        {
          var file = Path.Combine(input, x.EndsWith(".cec6") ? x : x + ".cec6");
          if (!File.Exists(file))
            continue;

          var link = Path.Combine(output, Path.GetFileName(file));
          if(File.Exists(link))
            continue;
          if(Directory.Exists(link))
            continue;

          Console.WriteLine($"{file} -> {link}");

          var process = new Process
          {
            StartInfo = new ProcessStartInfo
            {
              FileName = "ln",
              Arguments = $"-s {file} {link}",
              UseShellExecute = true,
              CreateNoWindow = true,
              WindowStyle = ProcessWindowStyle.Hidden
            }
          };
          process.Start();
          process.WaitForExit();
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
        }
      }
    }

    private static string[] Mode_File_Preprocess(string filter)
    {
      var lines = File.ReadAllLines(filter.Replace("FILE:", ""));
      var res = new List<string>();
      foreach (var x in lines)
      {
        if(x.Contains("{")){
          var start = x.IndexOf("{");
          var end = x.IndexOf("}");
          var range = x.Substring(start + 1, end - start - 1);
          var parts = range.Split('-');
          var startNum = int.Parse(parts[0]);
          var endNum = int.Parse(parts[1]);
          var prefix = x.Substring(0, start).ToLower();
          for (var i = startNum; i <= endNum; i++)
            res.Add(prefix + i);
        }
        else
          res.Add(x.ToLower());
      }
      return res.ToArray();;
    }

    private static void Mode_Direct(string input, string filter, string output)
    {
      foreach (var file in Directory.GetFiles(input, filter, SearchOption.TopDirectoryOnly))
      {
        try
        {
          var link = Path.Combine(output, Path.GetFileName(file));

          if (File.Exists(link))
            continue;
          if (Directory.Exists(link))
            continue;

          var process = new Process
          {
            StartInfo = new ProcessStartInfo
            {
              FileName = "ln",
              Arguments = $"-s {file} {link}",
              UseShellExecute = true,
              CreateNoWindow = true,
              WindowStyle = ProcessWindowStyle.Hidden
            }
          };
          process.Start();
          process.WaitForExit();
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.Message);
        }
      }
    }
  }
}