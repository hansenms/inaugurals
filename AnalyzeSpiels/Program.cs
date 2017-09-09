using System;
using System.IO;
using SpielInsights;

namespace AnalyzeSpiels
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputFolder = args[0];
            string outputFolder = args[1];
            string apiKey = args[2];

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string[] files = Directory.GetFiles(inputFolder, "*.json");

            foreach (string f in files)
            {
                string of = Path.Combine(outputFolder, Path.GetFileName(f));
                Spiel spiel = SpielInsights.SpielInsights.FromJsonFile<Spiel>(f);

                SpielAnalytics analytics = SpielInsights.SpielInsights.AnalyzeSpiel(spiel, apiKey);

                AnalyzedSpiel aSpiel = new AnalyzedSpiel();
                aSpiel.Spiel = spiel;
                aSpiel.SpielAnalytics = analytics;

                SpielInsights.SpielInsights.ToJson(aSpiel, of);

                Console.WriteLine("Processing file: " + f + " (" + spiel.Speaker + ")");
            }

        }
    }
}
