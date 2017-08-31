using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using SpielInsights;
using System.Collections.Generic;
using System.Text;

namespace Inaugurals
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputList = args[0];
            string apiKey = args[1];
            string outputFileName = args[2];

            var client = new HttpClient();

            //Output file
            System.IO.StreamWriter outputFile = new System.IO.StreamWriter(outputFileName);

            Task.Run(async () =>
            {
                using (StreamReader reader = new StreamReader(inputList))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Spiel spiel = new Spiel();

                        string[] components = line.Split(";");

                        spiel.Speaker = components[0];
                        spiel.SourceURI = components[1];
                        spiel.Date = System.Convert.ToDateTime(components[2]);

                        Console.WriteLine("Processing Inaugural: " + components[1]);
                        var response = await client.GetAsync(components[1]);
                        var content = await response.Content.ReadAsStringAsync();

                        HtmlDocument htmlDocument = new HtmlDocument();
                        htmlDocument.LoadHtml(content);

                        foreach (HtmlNode node in htmlDocument.DocumentNode.SelectNodes("//span[@class='displaytext']"))
                        {
                            HtmlDocument innerHtmlDocument = new HtmlDocument();
                            innerHtmlDocument.LoadHtml(node.InnerHtml);

                            foreach (HtmlNode pnode in innerHtmlDocument.DocumentNode.SelectNodes("//text()"))
                            {
                                string paragraphText = pnode.InnerText.Trim();
                                spiel.Paragraphs.Add(paragraphText);                  
                            }

                            SpielAnalytics analytics = SpielInsights.SpielInsights.AnalyzeSpiel(spiel, apiKey);


                            //Build semicolon separated output records
                            StringBuilder osb = new StringBuilder();
                            osb.Append(components[0] + ";"); //Speaker
                            osb.Append(components[2] + ";"); //Date
                            osb.Append(analytics.SummaryAnalytics.Sentiment);

                            outputFile.WriteLine(osb.ToString());
                            Console.WriteLine(osb.ToString());
                        }
                    }
                }
            }).GetAwaiter().GetResult();

            outputFile.Close();
        }
    }
}
