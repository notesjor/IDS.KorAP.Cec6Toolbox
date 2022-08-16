using System.Diagnostics;

namespace IDS.KorAP.Cec6Toolbox.RecursiveSubDirZip
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var input = args[0];
      var level = args[1];
      var output = args[2];

      var dirs = Directory.GetDirectories(input);
      Parallel.ForEach(dirs, new ParallelOptions{ MaxDegreeOfParallelism = 20 }, dir =>
      {
        var process = Process.Start(new ProcessStartInfo
        {
          FileName = "zip",
          Arguments = $"{level} -q -r {Path.Combine(output, dir.Replace(input, "").Replace("/", ""))}.zip {dir}",
          CreateNoWindow = true,
          WindowStyle = ProcessWindowStyle.Hidden
        });
        process.WaitForExit();
      });
    }
  }
}