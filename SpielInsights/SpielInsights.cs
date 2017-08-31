using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO;
using System.Text;
using Microsoft.ProjectOxford.Text.Sentiment;
using Microsoft.ProjectOxford.Text.KeyPhrase;


namespace SpielInsights
{
    public class SpielParagraphAnalytics
    {
        public SpielParagraphAnalytics()
        {
            KeyPhrases = new HashSet<string>();
            Sentiment = 0;
            Words = 0;
            Characters = 0;
        }

        public long Words { get; set; }

        public long Characters { get; set; }

        public double Sentiment { get; set; }

        public HashSet<string> KeyPhrases { get; set; }
    }

    public class SpielAnalytics
    {
        public SpielAnalytics()
        {
            SummaryAnalytics = new SpielParagraphAnalytics();
            ParaGraphAnalytics = new List<SpielParagraphAnalytics>();
        }

        public SpielParagraphAnalytics SummaryAnalytics { get; set; }
        public List<SpielParagraphAnalytics> ParaGraphAnalytics { get; set; }
    }

    public class Spiel
    {
        public Spiel()
        {
            Paragraphs = new List<string>();
        }

        public string SourceURI { get; set; }
        public string Speaker { get; set; }

        public string Category { get; set; }

        public DateTime Date { get; set; }

        public List<string> Paragraphs { get; set; }
    }

    public class SpielInsights
    {
        static public string SpielToJson(Spiel s)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;

            StringBuilder sb = new StringBuilder();
            using (TextWriter tw = new StringWriter(sb))
            using (JsonWriter writer = new JsonTextWriter(tw))
            {
                serializer.Serialize(writer, s);
            }

            return sb.ToString();
        }

        static public SpielAnalytics AnalyzeSpiel(Spiel spiel, string apiKey)
        {
            SpielAnalytics analytics = new SpielAnalytics();

            var sentimentRequest = new SentimentRequest();
            var keyPhraseRequest = new KeyPhraseRequest();

            //This is currently a hard limit in the Text Analytics API
            const int apiMaxCharacters = 5120;

            int chunkId = 0;
            int paragraphNum = 0;

            //chunkMap will map each of the text chunks submitted to the Analytics API to a given paragrah. 
            //Each chunk has an ID (string), but belongs to a given paragraph number (int) with a certain weight (double)
            var chunkMap = new Dictionary<string, Tuple<int, double>>();

            foreach (string p in spiel.Paragraphs)
            {
                SpielParagraphAnalytics a = new SpielParagraphAnalytics();

                string[] words = p.Split(' ');

                a.Characters = p.Length;
                a.Words = words.Length;

                //We need to divide this document into chunks
                if (p.Length > apiMaxCharacters)
                {
                    int chunks = (int)Math.Ceiling((double)p.Length / apiMaxCharacters);
                    int charsPerChunk = (int)Math.Ceiling((double)p.Length / chunks);

                    //Let's create a number of chunks that fit in the limits of the TextAnalytics API
                    int wordNum = 0;
                    List<string> chunkList = new List<string>();
                    int chunkCharacterSum = 0;
                    for (int i = 0; i < chunks; i++)
                    {
                        StringBuilder sb = new StringBuilder();
            
                        while (sb.Length < charsPerChunk && wordNum < words.Length)
                        {
                            sb.Append(words[wordNum++] + " ");
                        }

                        string chunk = sb.ToString();

                        if (chunk.Length > 0)
                        {
                            chunkList.Add(chunk);
                            chunkCharacterSum += chunk.Length;
                        }
                    }

                    //Based on the chunks and the total number of characters in the chunks, assign IDs and weights. 
                    foreach (string c in chunkList)
                    {
                        keyPhraseRequest.Documents.Add(new KeyPhraseDocument { Id = chunkId.ToString(), Language = "en", Text = c });
                        sentimentRequest.Documents.Add(new SentimentDocument { Id = chunkId.ToString(), Language = "en", Text = c });
                        chunkMap[chunkId.ToString()] = Tuple.Create(paragraphNum, (double)c.Length / (double)chunkCharacterSum);
                        chunkId++;

                    }
                } else
                {
                    if (p.Length > 0)
                    {
                        keyPhraseRequest.Documents.Add(new KeyPhraseDocument { Id = chunkId.ToString(), Language = "en", Text = p });
                        sentimentRequest.Documents.Add(new SentimentDocument { Id = chunkId.ToString(), Language = "en", Text = p });
                        chunkMap[chunkId.ToString()] = Tuple.Create(paragraphNum, 1.0);
                        chunkId++;
                    }
                }
                paragraphNum++;

                analytics.SummaryAnalytics.Words += a.Words;
                analytics.SummaryAnalytics.Characters += a.Characters;

                analytics.ParaGraphAnalytics.Add(a);
            }


            //Let's get the sentiment scores for each paragraph/chunk.
            var sentimentClient = new SentimentClient(apiKey);
            var sentimentResponse = sentimentClient.GetSentiment(sentimentRequest);

            //Print any errors if we have any
            foreach (Microsoft.ProjectOxford.Text.Core.DocumentError e in sentimentResponse.Errors)
            {
                Console.WriteLine("Errors from sentiment API: " + e.Message);
            }

            foreach (SentimentDocumentResult r in sentimentResponse.Documents)
            {
                int p = chunkMap[r.Id].Item1;
                double weightedScore = r.Score * chunkMap[r.Id].Item2;
                analytics.ParaGraphAnalytics[p].Sentiment += weightedScore;
            }


            //Weighted sum of the analytics
            foreach (SpielParagraphAnalytics pa in analytics.ParaGraphAnalytics)
            {
                analytics.SummaryAnalytics.Sentiment += pa.Sentiment * ((double)pa.Characters / (double)analytics.SummaryAnalytics.Characters);
            }

            //Now we will follow the same procedure to get the key phrases in the paragraphs
            var keyPhraseClient = new KeyPhraseClient(apiKey);
            var keyPhraseResponse = keyPhraseClient.GetKeyPhrases(keyPhraseRequest);

            //Print any errors if we have any
            foreach (Microsoft.ProjectOxford.Text.Core.DocumentError e in keyPhraseResponse.Errors)
            {
                Console.WriteLine("Errors from key phrase API: " + e.Message);
            }

            foreach (KeyPhraseDocumentResult r in keyPhraseResponse.Documents)
            {
                int p = chunkMap[r.Id].Item1;

                foreach (string kp in r.KeyPhrases)
                {
                    analytics.ParaGraphAnalytics[p].KeyPhrases.Add(kp);
                    analytics.SummaryAnalytics.KeyPhrases.Add(kp);
                }
            }


            return analytics;
        }
    }
}
