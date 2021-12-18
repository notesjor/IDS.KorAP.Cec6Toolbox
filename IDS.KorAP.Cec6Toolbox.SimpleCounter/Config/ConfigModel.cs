namespace IDS.KorAP.Cec6Toolbox.SimpleCounter.Config
{
  public class ConfigModel
  {
    public int Parallel { get; set; } = 20;
    public string Cec6InputDir { get; set; } = "/home/ruediger/CEC6";
    public string PathCorpuslist { get; set; } = "/home/ruediger/PROJECTS/CORPUSLIST/corpus_cec6_and_cec6time.lst";
    public string RDBWordlist { get; set; } = "/home/ruediger/PROJECTS/WORDLIST/Step02-MakeIndex/w2i";
    public string PathOutput { get; set; } = "/home/ruediger/PROJECTS/WORDLIST/Step03-SimpleCount";
  }
}
