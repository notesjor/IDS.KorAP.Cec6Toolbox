using CorpusExplorer.Sdk.Ecosystem;
using CorpusExplorer.Sdk.Model.Adapter.Corpus;
using MainContext;

namespace IDS.KorAP.Cec6Toolbox.DbIndex
{
  internal class Program
  {
    static void Main(string[] args)
    {
      CorpusExplorerEcosystem.InitializeMinimal();

      var files = Directory.GetFiles(args[0], "*.cec6", SearchOption.TopDirectoryOnly);

      using (var context = new MainDataContext("data source=data.db3;failifmissing=False"))
      {
        if (!context.DatabaseExists())
          context.CreateDatabase();

        var @lock = new object();

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = 5 }, file =>
        {
          try
          {

            var cname = Path.GetFileNameWithoutExtension(file);
            Corpus cref;
            lock (@lock)
            {
              if (context.Corpora.Any(c => c.Name == cname))
                return;

              cref = new Corpus { Name = cname };
              context.Corpora.InsertOnSubmit(cref);
              context.SubmitChanges();
            }

            var meta = GetMetadata(file);
            var docs = new List<Document>();
            foreach (var doc in meta)
            {
              docs.Add(new Document
              {
                Corpus = cref,
                Id = doc.Key,
                Year = doc.Value.Year,
                Month = doc.Value.Month,
                Day = doc.Value.Day
              });
            }

            lock (@lock)
            {
              context.Documents.InsertAllOnSubmit(docs);
              context.SubmitChanges();
            }
          }
          catch
          {
            // ignore
          }
        });
      }
    }

    private static Dictionary<Guid, DateTime> GetMetadata(string file)
    {
      var corpus = CorpusAdapterWriteDirect.Create(file);
      var res = new Dictionary<Guid, DateTime>();
      foreach (var doc in corpus.DocumentMetadata)
      {
        if (doc.Value.ContainsKey("Datum") && doc.Value["Datum"] is DateTime dt)
          res.Add(doc.Key, dt);
        else
          res.Add(doc.Key, DateTime.MinValue);
      }
      corpus.Dispose();
      return res;
    }
  }
}
