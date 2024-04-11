using MedChat.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using AdvUtils;
using TensorSharp.CUDA.DeviceCode;
using System.Net;
using System.Security.Policy;

namespace MedChat.Controllers
{
    [Serializable]
    public class BackendResult
    {
        public string? Output { get; set; }
        public bool? isend { get; set; }
    }

    public static class SessionExtensions
    {
        public static void SetObject(this ISession session, string key, object value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T GetObject<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }
    }

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            bool acceptAgreement = HttpContext.Session.GetObject<bool>("acceptAgreement");
            if (acceptAgreement)
            {
                ViewData["acceptAgreement"] = true;
            }
            else
            {
                ViewData["acceptAgreement"] = false;
            }


            HttpContext.Session.SetObject("maskedDict", null);
            HttpContext.Session.SetObject("notMaskList", null);
            HttpContext.Session.SetObject("translateCache", null);


            //!!!!!!!!!!!!!!!!!!DEBUG!!!!!!!
            ViewData["acceptAgreement"] = true;
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!


            return View();
        }

        public IActionResult Agreement(string checkAgreement)
        {
            if (String.IsNullOrEmpty(checkAgreement) == false)
            {
                //Users has signed the agreement
                HttpContext.Session.SetObject("acceptAgreement", true);
            }

            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }



        [HttpPost]
        public IActionResult RegenerateTurn(string transcript, bool aiDoctor, bool isMale)
        {
            transcript = Utils.RemoveHtmlTags(transcript);
            List<string> turnText = Utils.SplitTurns(transcript);

            if (turnText.Count == 1)
            {
                BackendResult tr2 = new BackendResult
                {
                    Output = String.Join("</div> ", Utils.AddHtmlTags(turnText, isMale)),
                    isend = true
                };

                return new JsonResult(tr2);
            }

               turnText = Translate(turnText, Settings.Language, "en");
            //    turnText = UnmaskEntity(turnText);

            turnText.RemoveAt(turnText.Count - 1);
            if (aiDoctor)
            {
                turnText.Add($"{Settings.DoctorTag} ");
            }
            else
            {
                turnText.Add($"{Settings.PatientTag} ");
            }


            (var turnTexts, var bEOS) = CallBackend(turnText, 0.1f, aiDoctor, 1, forceUseOpenAI: true); // For turn re-generation, we force to use OpenAI model.

            //Logging
            var remoteIpAddress = Request.HttpContext.Connection.RemoteIpAddress;
            string rawOutput = String.Join(" ", turnTexts[0]);
            string logLine = $"Client '{remoteIpAddress.ToString()}' Regenerate Turn: '{rawOutput}'";
            Logger.WriteLine(logLine);
            if (bEOS)
            {
                Settings.BlobLogs.WriteLine(logLine);        
            }
            turnText = NormText(turnTexts[0], aiDoctor, "en", Settings.Language);

            BackendResult tr = new BackendResult
            {
                Output = String.Join("</div> ", Utils.AddHtmlTags(turnText, isMale)),
                isend = bEOS
            };

            if (turnTexts.Count > 1)
            {
                List<string> nBestLastTurn = new List<string>();
                for (int i = 1; i < turnTexts.Count; i++)
                {
                    string text = turnTexts[i][turnTexts[i].Count - 1].Replace(Settings.DoctorTag, "").Replace(Settings.PatientTag, "").Trim();
                    nBestLastTurn.Add(text);
                }

                nBestLastTurn = NormText(nBestLastTurn, aiDoctor, "en", Settings.Language);
                tr.Output = tr.Output + "\t" + String.Join("\t", nBestLastTurn);
            }

            return new JsonResult(tr);
        }

        [HttpPost]
        public void SubmitFeedback(string transcript, int feedBackType)
        {
            transcript = Utils.RemoveHtmlTags(transcript);
            List<string> turnText = Utils.SplitTurns(transcript);
            turnText = Translate(turnText, Settings.Language, "en");
            transcript = String.Join(" ", turnText);

            string logLine = "";
            if (feedBackType == 1)
            {
                //Thumb Up
                logLine = $"ThumbUp: Transcript = '{transcript}'";
            }
            else
            {
                //Thumb Down
                logLine = $"ThumbDown: Transcript = '{transcript}'";
            }


            Settings.BlobLogs.WriteLine(logLine);
        }

        [HttpPost]
        public IActionResult SendTurn(string transcript, string inputTurn, bool aiDoctor, bool contiGen, bool isMale)
        {
            if (contiGen == false && String.IsNullOrEmpty(inputTurn))
            {
                BackendResult tr2 = new BackendResult
                {
                    Output = transcript,
                    isend= true,
                };

                return new JsonResult(tr2);
            }

            transcript = Utils.RemoveHtmlTags(transcript);
            List<string> turnText = Utils.SplitTurns(transcript);
            if (contiGen == false)
            {
                if (aiDoctor)
                {
                    turnText.Add($"{Settings.PatientTag} " + inputTurn);
                    turnText.Add($"{Settings.DoctorTag} ");
                }
                else
                {
                    turnText.Add($"{Settings.DoctorTag} " + inputTurn);
                    turnText.Add($"{Settings.PatientTag} ");
                }
            }

            turnText = Translate(turnText, Settings.Language, "en");
       //     turnText = UnmaskEntity(turnText);

            // Call model to generate outputs
            List<List<string>> turnTexts;
            bool turnEnded = false;
            if (Utils.IsConversationEnded(turnText[^2]))
            {
                turnText.RemoveAt(turnText.Count - 1);
                turnTexts = new List<List<string>>();
                turnTexts.Add(turnText);
                turnEnded = true;
            }
            else
            {
                (turnTexts, turnEnded) = CallBackend(turnText, 0.0f, aiDoctor, Settings.nBest);

            }

            //Logging
            var remoteIpAddress = Request.HttpContext.Connection.RemoteIpAddress;
            string rawOutput = String.Join(" ", turnTexts[0]);
            string logLine = $"Client '{remoteIpAddress.ToString()}' New Turn: '{rawOutput}'";
            Logger.WriteLine(logLine);
            if (turnEnded)
            {
                Settings.BlobLogs.WriteLine(logLine);
            }
            turnText = NormText(turnTexts[0], aiDoctor, "en", Settings.Language);

            BackendResult tr = new BackendResult
            {
                Output = String.Join("</div> ", Utils.AddHtmlTags(turnText, isMale)),
                isend = turnEnded
            };

            if (turnTexts.Count > 1)
            {
                List<string> nBestLastTurn = new List<string>();
                for (int i = 1; i < turnTexts.Count; i++)
                {
                    string text = turnTexts[i][turnTexts[i].Count - 1].Replace(Settings.DoctorTag, "").Replace(Settings.PatientTag, "").Trim();
                    nBestLastTurn.Add(text);
                }
                nBestLastTurn = NormText(nBestLastTurn, aiDoctor, "en", Settings.Language);
                tr.Output = tr.Output + "\t" + String.Join("\t", nBestLastTurn);
            }

            return new JsonResult(tr);
        }
     
        //private List<string> UnmaskEntity(List<string> sents)
        //{
        //    if (Settings.ChatModel == ChatModel.OpenAI)
        //    {
        //        // We don't unmask results from OpenAI model
        //        return sents;
        //    }

        //    Dictionary<string, string> maskedDict = HttpContext.Session.GetObject<Dictionary<string, string>>("maskedDict");
        //    if (maskedDict == null)
        //    {
        //        maskedDict = new Dictionary<string, string>();
        //    }

        //    List<string> newSents = new List<string>();
        //    foreach (string sent in sents)
        //    {
        //        List<string> newWords = new List<string>();
        //        string[] words = sent.Split(' ');
        //        foreach (string word in words)
        //        {
        //            if (maskedDict.ContainsKey(word))
        //            {
        //                newWords.Add(maskedDict[word]);
        //            }
        //            else
        //            {
        //                newWords.Add(word);
        //            }
        //        }

        //        newSents.Add(String.Join(" ", newWords));
        //    }

        //    return newSents;
        //}


        private bool ShouldMasked(string word, string preWord)
        {
            if (word.Length > 1 && word[0] >= 'A' && word[0] <= 'Z' && word.Contains("'") == false &&
                        (preWord.EndsWith(".") == false || preWord == "Dr.") && preWord.EndsWith("?") == false && preWord.EndsWith("]") == false)
                return true;

            return false;
        }

        private string MaskEntity(string sent, bool aiDoctor)
        {
            if (Settings.ChatModel == ChatModel.OpenAI)
            {
                // We don't mask results from OpenAI model
                return sent;
            }

            Dictionary<string, string> maskedDict = HttpContext.Session.GetObject<Dictionary<string, string>>("maskedDict");
            if (maskedDict == null)
            {
                maskedDict = new Dictionary<string, string>();
            }
            int maskIdx = maskedDict.Count;


            HashSet<string> setNotMask = HttpContext.Session.GetObject<HashSet<string>>("notMaskList");
            if (setNotMask == null)
            {
                setNotMask = new HashSet<string>();
            }

            string[] words = sent.Split(' ');

            if ((aiDoctor && sent.StartsWith(Settings.PatientTag)) || (aiDoctor == false && sent.StartsWith(Settings.DoctorTag)))
            {
                foreach (var word in words)
                {
                    setNotMask.Add(word);
                }
                return sent;
            }

            List<string> newWords = new List<string>();
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                if (i > 0 && setNotMask.Contains(word) == false && ShouldMasked(word, words[i - 1]) && (i > 1 && ShouldMasked(words[i - 1], words[i - 2]) || i < words.Length - 1 && ShouldMasked(words[i + 1], word)))
                {
                    string maskKey = "";
                    foreach (var pair in maskedDict)
                    {
                        if (word == pair.Value)
                        {
                            maskKey = pair.Key;
                        }
                    }

                    if (String.IsNullOrEmpty(maskKey))
                    {
                        maskKey = $"MASKED_{maskIdx}";
                        maskIdx++;
                        maskedDict.Add(maskKey, word);
                    }

                    newWords.Add(maskKey);
                }
                else
                {
                    newWords.Add(word);
                }
            }

            string newSent = String.Join(" ", newWords);

            HttpContext.Session.SetObject("maskedDict", maskedDict);
            HttpContext.Session.SetObject("notMaskList", setNotMask);

            return newSent;
        }

        private List<string> NormText(List<string> turns, bool aiDoctor, string from, string to)
        {
            var turnText = Utils.NormText(turns, aiDoctor);
            turnText = Translate(turnText, from, to);

            return turnText;
        }

        private List<string> Translate(List<string> turnText, string from, string to)
        {
            if (from == to)
            {
                return turnText;
            }

            Dictionary<string, string> translateCache = HttpContext.Session.GetObject<Dictionary<string, string>>("translateCache");
            if (translateCache == null)
            {
                translateCache = new Dictionary<string, string>();
            }

            List<string> results = new List<string>();

            foreach (var turn in turnText)
            {
                string[] words = turn.Split(' ');
                string role = words[0];

                string textToTranslate = turn;
                if (role == Settings.DoctorTag || role == Settings.PatientTag)
                {
                    textToTranslate = String.Join(" ", words, 1, words.Length - 1).Trim();
                }

                if (String.IsNullOrEmpty(textToTranslate))
                {
                    results.Add(turn);
                    continue;
                }

                string translatedText = "";
                if (translateCache.ContainsKey(textToTranslate))
                {
                    translatedText = translateCache[textToTranslate];
                }
                else
                {
                    translatedText = Translator.Translate(textToTranslate, from, to);
                    translateCache.Add(textToTranslate, translatedText);
                }

                if (translateCache.ContainsKey(translatedText) == false)
                {
                    translateCache.Add(translatedText, textToTranslate);
                }

                if (translateCache.ContainsKey(textToTranslate) == false)
                {
                    translateCache.Add(textToTranslate, translatedText);
                }

                if (role == Settings.DoctorTag || role == Settings.PatientTag)
                {
                    results.Add($"{role} {translatedText}");
                }
                else
                {
                    results.Add(translatedText);
                }
            }

            HttpContext.Session.SetObject("translateCache", translateCache);

            return results;
        }


        private bool HasTooQsFromDoctor(List<string> turns)
        {
            int num = 0;
            for (int i = turns.Count - 1; i >= 0; i--)
            {
                string turn = turns[i];
                if (turn.Contains(Settings.DoctorTag))
                {
                    string t = turn.Replace(Settings.DoctorTag, "").Trim();
                    if (string.IsNullOrEmpty(t))
                    {
                        continue;
                    }
                    if (turn.EndsWith("?"))
                    {
                        num++;
                        if (num >= 15)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }           
            }

            return false;
        }

        /// <summary>
        /// Call backend generative models. It supports in-house model and OpenAI models for now.
        /// </summary>
        /// <param name="turnText"></param>
        /// <param name="topP"></param>
        /// <param name="aiDoctor"></param>
        /// <param name="nbest"></param>
        /// <returns></returns>
        private (List<List<string>>, bool) CallBackend(List<string> turnText, float topP, bool aiDoctor, int nbest = 1, bool forceUseOpenAI = false)
        {
            bool tooManyQuestion = HasTooQsFromDoctor(turnText);
            if (Settings.ChatModel == ChatModel.OpenAI || forceUseOpenAI || tooManyQuestion)
            {
                try
                {
                    Logger.WriteLine(Logger.Level.info, ConsoleColor.Green, "Calling OpenAI model to generate the next turn.");
                    return CallOpenAI(turnText, topP, aiDoctor, nbest, noMoreQuestion: tooManyQuestion);
                }
                catch(Exception e) 
                {
                    Logger.WriteLine($"Failed to call OpenAI model due to '{e.Message}', so we fall back to the in-house model. Call stack = '{e.StackTrace}'");
                    return CallInHouseModel(turnText, topP, aiDoctor, nbest);
                }
            }
            else
            {
                (var results, bool turnEnded) = CallInHouseModel(turnText, topP, aiDoctor, nbest);
                try
                {
                    return RewriteLastTurn(turnText, results, turnEnded, topP, aiDoctor, nbest);
                }
                catch (Exception e)
                {
                    Logger.WriteLine($"Failed to rewrite in-house model result by OpenAI model. Error = '{e.Message}' Call stack = '{e.StackTrace}'");
                    return (results, turnEnded);
                }
            }
        }

        private bool HasLongDigits(string text)
        {
            var words = text.Split(' ');
            foreach (var word in words)
            {
                bool isPunctOrDigit = true;
                int dightNum = 0;
                foreach (var ch in word)
                {
                    if (char.IsPunctuation(ch) == false && char.IsDigit(ch) == false)
                    {
                        isPunctOrDigit = false;
                        break;
                    }

                    if (char.IsDigit(ch))
                    {
                        dightNum++;
                    }
                }

                if (isPunctOrDigit && dightNum >= 3)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsRepeatTurn(string turn)
        {
            var results = Utils.FindRepeatingSubstrings(turn);

            foreach (var pair in results)
            {
                if (pair.Key.Length >= 2 && pair.Value >= 5)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if the generated turn needs to be rewritten by OpenAI model.
        /// If the generated turns has one of any case in below, it will be rewritten:
        /// 1. Too short (less then 3 words)
        /// 2. Has information needs to be masked
        /// 3. Need to be hotfixed.
        /// 4. Including long digits
        /// 5. The turn has already been in the previous turns.
        /// </summary>
        /// <param name="inputTurns"></param>
        /// <param name="resultBatch"></param>
        /// <param name="topP"></param>
        /// <param name="aiDoctor"></param>
        /// <param name="nbest"></param>
        /// <returns></returns>
        private (List<List<string>>, bool) RewriteLastTurn(List<string> inputTurns, List<List<string>> resultBatch, bool turnEnded, float topP, bool aiDoctor, int nbest = 1)
        {
            for (int i = 0; i < resultBatch.Count; i++)
            {
                var result = resultBatch[i];
            //    var words = result[^1].Split(' ');

                // Check if the last turn is duplicated.
                HashSet<string> setTurns = new HashSet<string>();
                for (int j = 0; j < result.Count - 1; j++)
                {
                    setTurns.Add(result[j].ToLower().Trim());
                }

                var maskedTurn = MaskEntity(result[^1], aiDoctor);
                var isRepeatTurn = IsRepeatTurn(result[^1]);

                if (maskedTurn != result[^1] || Utils.NeedHotfix(result[^1]) || HasLongDigits(result[^1]) || setTurns.Contains(result[^1].ToLower().Trim()) || isRepeatTurn)
                {
                    Logger.WriteLine(Logger.Level.info, ConsoleColor.Green, $"This turn needs to be rewritten by OpenAI model. Turn = '{result[^1]}'");
                    return CallOpenAI(inputTurns, topP, aiDoctor, nbest);
                }
            }

            return (resultBatch, turnEnded);
        }

        /// <summary>
        /// Call OpenAI chat model
        /// </summary>
        /// <param name="turnText"></param>
        /// <param name="topP"></param>
        /// <param name="aiDoctor"></param>
        /// <param name="nbest"></param>
        /// <returns></returns>
        private (List<List<string>>, bool) CallOpenAI(List<string> turnText, float topP, bool aiDoctor, int nbest = 1, bool noMoreQuestion = false)
        {
            List<List<string>> results = new List<List<string>>(); // [nbest, turns]

            string lastTurn = OpenAI.Call(turnText, aiDoctor, noMoreQuestion: noMoreQuestion);
            lastTurn = lastTurn.Replace("\n\n", " ").Replace("\n", " ").Trim();

            if ((aiDoctor && lastTurn.StartsWith(Settings.DoctorTag) == false) || 
                (!aiDoctor && lastTurn.StartsWith(Settings.PatientTag) == false))
            {
                throw new Exception($"OpenAI model generate incorrect result.");
            }
 
            turnText[turnText.Count - 1] = lastTurn;
            for (int i = 0; i < nbest; i++)
            {
                results.Add(turnText);
            }

            return (results, true);
        }

        /// <summary>
        /// Call in-house chat model
        /// </summary>
        /// <param name="turnText"></param>
        /// <param name="topP"></param>
        /// <param name="aiDoctor"></param>
        /// <param name="nbest"></param>
        /// <returns></returns>
        private (List<List<string>>, bool) CallInHouseModel(List<string> turnText, float topP, bool aiDoctor, int nbest = 1)
        {
            string inputText = String.Join(" ", turnText);
            List<List<string>> results = new List<List<string>>(); // [nbest, turns]
            DateTime startTime = DateTime.Now;
            bool turnEnded = false;

            while (results.Count < nbest)
            {
                if (results.Count > 0)
                {
                    topP = 0.1f;
                }

                // Ask model to generate a completed turn
                string outputText = Seq2SeqInstance.Call(inputText, inputText, 8, topP, 1.0f, Settings.PenaltyScore);
                var newTurns = Utils.SplitTurns(outputText);

                if (newTurns.Count > turnText.Count)
                {
                    turnEnded = true;
                }
                else if (newTurns.Count == turnText.Count && newTurns[^1] == turnText[^1])
                {
                    turnEnded = true;
                }
                else if (newTurns[^1].Length >= 256)
                {
                    int periodIdx = newTurns[^1].LastIndexOf("?");
                    if (periodIdx < 0)
                    {
                        periodIdx = newTurns[^1].LastIndexOf(".");
                    }

                    if (periodIdx >= 0)
                    {
                        newTurns[^1] = newTurns[^1].Substring(0, periodIdx + 1);
                    }

                    turnEnded = true;
                }

                newTurns = newTurns.GetRange(0, turnText.Count);

                results.Add(newTurns);
            }

            return (results, turnEnded);
        }

        [HttpPost]
        public IActionResult GenerateNote(string transcript, string tag)
        {
            List<string> turnText = Utils.SplitTurns(transcript);
            string inputText = String.Join(" ", turnText);
            tag = $"__{tag}__";


            inputText += (" " + tag);
            int maxLength = 300;
            int outputLengthSoFar = 0;

            string outputText = Seq2SeqInstance.Call(inputText, inputText, 30, 0.0f, 1.0f, 2.0f);
            outputLengthSoFar = 30;
            while (outputText.EndsWith("EOS") == false && outputLengthSoFar < maxLength)
            {
                // We need to call the model again to generate more contents.
                outputText = Seq2SeqInstance.Call(outputText, outputText, 30, 0.0f, 1.0f, 2.0f);
                outputLengthSoFar += 30;
            }

            var remoteIpAddress = Request.HttpContext.Connection.RemoteIpAddress;
            string logLine = $"Client '{remoteIpAddress.ToString()}' Turns and drafted note: '{outputText}'";
            Logger.WriteLine(logLine);
            Settings.BlobLogs.WriteLine(logLine);

            int idx = outputText.IndexOf(tag);
            string note = outputText.Substring(idx + tag.Length);

            note = note.Replace("__lf__", "<br/>").Replace("__lf1__", "<br/>").Replace("__lf2__", "<br/><br/>").Replace("__lf3__", "<br/><br/>");
            if (note.EndsWith("EOS"))
            {
                note = note.Substring(0, note.Length - 4);
            }

            BackendResult tr = new BackendResult
            {
                Output = note
            };

            return new JsonResult(tr);
        }       
    }
}