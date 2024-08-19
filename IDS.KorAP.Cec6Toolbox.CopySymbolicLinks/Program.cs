using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IDS.KorAP.Cec6Toolbox.CopySymbolicLinks
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var mode = args[0].ToLower();
      args = args.Skip(1).ToArray();

      switch (mode)
      {
        case "q":
          Mode_Direct(args);
          return;
        case "sigle":
          Mode_Sigle(args);
          return;
        case "file":
          Mode_File(args);
          return;
        case "date":
          Mode_Date(args);
          return;
        case "regex":
          Mode_Regex(args);
          return;
        case "compare":
          Mode_Compare(args);
          return;
        default:
          PrintHelp();
          return;
      }
    }

    private static void Mode_Regex(string[] args)
    {
      var input = args[0];
      var sigle = new Regex(args[1]);
      var output = args[2];

      var files = Directory.GetFiles(input, "*.*", SearchOption.TopDirectoryOnly);
      foreach (var f in files)
      {
        var name = Path.GetFileName(f);

        if (!sigle.IsMatch(name))
          continue;

        var link = Path.Combine(output, name);
        if (File.Exists(link))
          continue;
        if (Directory.Exists(link))
          continue;

        var process = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "ln",
            Arguments = $"-s {f} {link}",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
          }
        };
        process.Start();
        process.WaitForExit();
      }
    }

    private static void Mode_Compare(string[] args)
    {
      var input = args[0];
      var output = args[1];

      var filesIn = Directory.GetFiles(input, "*.*", SearchOption.TopDirectoryOnly);
      var filesOut = new HashSet<string>(Directory.GetFiles(output, "*.*", SearchOption.TopDirectoryOnly).Select(x => Path.GetFileNameWithoutExtension(x)));

      foreach (var f in filesIn)
      {
        var name = Path.GetFileNameWithoutExtension(f);
        if (filesOut.Contains(name))
          continue;

        Console.WriteLine(f);
      }
    }

    private static void Mode_Sigle(string[] args)
    {
      var input = args[0];
      var sigle = args[1];
      var output = args[2];
      Directory.SetCurrentDirectory(output);

      if (!Directory.Exists(output))
        Directory.CreateDirectory(output);


      var process = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "find",
          Arguments = $"{input} -regextype posix-extended -regex '.*{sigle}[0-9]{{2}}\\.cec6' -exec ln -s {{}} \\;",
          UseShellExecute = true,
          CreateNoWindow = true,
          WindowStyle = ProcessWindowStyle.Hidden,
          WorkingDirectory = output
        }
      };
      process.Start();
      process.WaitForExit();
    }

    private static void PrintHelp()
    {
      Console.WriteLine("Mode: q / file / data / sigle");
      Console.WriteLine("q: Direct copy");
      Console.WriteLine("file: Copy files based on a list");
      Console.WriteLine("date: Copy files based on a date range");
      Console.WriteLine("sigle: Copy files based on a sigle");
      Console.WriteLine("regex: Copy files based on a regex");
      Console.WriteLine("compare: Compare files in two directories");

      Console.WriteLine("q: input filter output");
      Console.WriteLine("file: input file output");
      Console.WriteLine("date: input start end output");
      Console.WriteLine("sigle: input sigle output");
      Console.WriteLine("regex: input regex output");
      Console.WriteLine("compare: input output");
    }

    private static void Mode_Date(string[] args)
    {
      var input = args[0];
      var start = DateTime.ParseExact(args[1], "yyyy-MM-dd", CultureInfo.InvariantCulture);
      var end = DateTime.ParseExact(args[2], "yyyy-MM-dd", CultureInfo.InvariantCulture);
      var output = args[3];
      Directory.SetCurrentDirectory(output);

      if (!Directory.Exists(output))
        Directory.CreateDirectory(output);

      var files = new HashSet<string>(Directory.GetFiles(input).Select(x => Path.GetFileNameWithoutExtension(x)));
      for (var date = start; date <= end; date = date.AddDays(1))
      {
        var file = date.ToString("yyyy-MM-dd");
        if (!files.Contains(file))
          continue;
        file += ".cec6";

        var link = Path.Combine(output, file);
        if (File.Exists(link))
          continue;
        if (Directory.Exists(link))
          continue;

        var process = new Process
        {
          StartInfo = new ProcessStartInfo
          {
            FileName = "ln",
            Arguments = $"-s {Path.Combine(input, file)} {link}",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
          }
        };
        process.Start();
        process.WaitForExit();
      }
    }

    private static void Mode_File(string[] args)
    {
      var input = args[0];
      var filter = args[1];
      var output = args[2];
      Directory.SetCurrentDirectory(output);

      var lines = Mode_File_Preprocess(filter);
      foreach (var x in lines)
      {
        try
        {
          var file = Path.Combine(input, x.EndsWith(".cec6") ? x : x + ".cec6");
          if (!File.Exists(file))
            continue;

          var link = Path.Combine(output, Path.GetFileName(file));
          if (File.Exists(link))
            continue;
          if (Directory.Exists(link))
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
        if (x.Contains("{"))
        {
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
      return res.ToArray(); ;
    }

    private static void Mode_Direct(string[] args)
    {
      var input = args[0];
      var filter = args[1];
      var output = args[2];
      Directory.SetCurrentDirectory(output);

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