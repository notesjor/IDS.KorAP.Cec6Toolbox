namespace IDS.KorAP.Cec6Toolbox.CreateDirForEveryFile
{
  internal class Program
  {
    static void Main(string[] args)
    {
      var dir = args[0];
      var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);

      foreach (var file in files)
      {
        var fileName = Path.GetFileNameWithoutExtension(file);
        var newDir = Path.Combine(dir, fileName);
        Directory.CreateDirectory(newDir);
        File.Move(file, Path.Combine(newDir, Path.GetFileName(file)));
      } 
    }
  }
}