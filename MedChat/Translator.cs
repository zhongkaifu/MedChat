using Newtonsoft.Json;
using System.Text;

namespace MedChat
{
    /// <summary>
    /// The C# classes that represents the JSON returned by the Translator Text API.
    /// </summary>
    public class TranslationResult
    {
        public DetectedLanguage DetectedLanguage { get; set; }
        public TextResult SourceText { get; set; }
        public Translation[] Translations { get; set; }
    }

    public class DetectedLanguage
    {
        public string Language { get; set; }
        public float Score { get; set; }
    }

    public class TextResult
    {
        public string Text { get; set; }
        public string Script { get; set; }
    }

    public class Translation
    {
        public string Text { get; set; }
        public TextResult Transliteration { get; set; }
        public string To { get; set; }
        public Alignment Alignment { get; set; }
        public SentenceLength SentLen { get; set; }
    }
    public class Alignment
    {
        public string Proj { get; set; }
    }

    public class SentenceLength
    {
        public int[] SrcSentLen { get; set; }
        public int[] TransSentLen { get; set; }
    }

    public class Translator
    {
        private static string m_key = "";
        private static string m_endpoint = "https://api.cognitive.microsofttranslator.com";
        private static string m_location = "";


        public static void Initialize(string key, string location)
        {
            if (String.IsNullOrEmpty(key) == false)
            {
                m_key = key;
            }

            if (String.IsNullOrEmpty(location) == false)
            {
                m_location = location;
            }
        }

        public static string Translate(string textToTranslate, string from, string to)
        {
            if (String.IsNullOrEmpty(m_key) || String.IsNullOrEmpty(m_endpoint))
            {
                return textToTranslate;
            }

            // Input and output languages are defined as parameters.
            string route = $"/translate?api-version=3.0&from={from}&to={to}";
      //      string textToTranslate = "I would really like to drive your car around the block a few times!";
            object[] body = new object[] { new { Text = textToTranslate } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(m_endpoint + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", m_key);
                // location required if you're using a multi-service or regional (not global) resource.
                request.Headers.Add("Ocp-Apim-Subscription-Region", m_location);

                // Send the request and get response.
                HttpResponseMessage response = client.Send(request);
                // Read response as a string.
                string rawResult = response.Content.ReadAsStringAsync().Result;

                TranslationResult[] tr = JsonConvert.DeserializeObject<TranslationResult[]>(rawResult);
                return tr[0].Translations[0].Text;
            }
        }
    }
}
