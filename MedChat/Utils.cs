using ProtoBuf.Meta;
using System.Net;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace MedChat
{
    public static class Utils
    {
        public static bool IsConversationEnded(string turn)
        {
            string lastTurn = turn.ToLower();
            if (lastTurn.Contains(" bye."))
            {
                return true;
            }

            return false;
        }
        public static Dictionary<string, int> FindRepeatingSubstrings(string input)
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            for (int length = 1; length <= input.Length / 2; length++)
            {
                for (int start = 0; start <= input.Length - 2 * length; start++)
                {
                    string substring = input.Substring(start, length);
                    string nextSubstring = input.Substring(start + length, length);
                    if (substring == nextSubstring)
                    {
                        int count = 2;
                        while (start + count * length + length <= input.Length && input.Substring(start + count * length, length) == substring)
                        {
                            count++;
                        }
                        if (!result.ContainsKey(substring) || count > result[substring])
                        {
                            result[substring] = count;
                        }
                    }
                }
            }
            return result;
        }

        public static List<string> SplitTurns(string transcript)
        {
            string[] words = transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            List<string> turns = new List<string>();
            List<string> wordsInCurrentTurn = new List<string>();
            foreach (string word in words)
            {
                if (String.IsNullOrEmpty(word))
                {
                    continue;
                }

                if (word.StartsWith("__") == true)
                {
                    //It starts note part, so we ignore the rest of the text.
                    break;
                }

                if (word == Settings.DoctorTag || word == Settings.PatientTag)
                {
                    if (wordsInCurrentTurn.Count > 0)
                    {
                        turns.Add(String.Join(" ", wordsInCurrentTurn).Trim());
                        wordsInCurrentTurn.Clear();
                    }
                }

                wordsInCurrentTurn.Add(word);
            }

            if (wordsInCurrentTurn.Count > 0)
            {
                turns.Add(String.Join(" ", wordsInCurrentTurn).Trim());
                wordsInCurrentTurn.Clear();
            }

            return turns;
        }

        public static bool NeedHotfix(string text)
        {
            if (Settings.Hotfix != null)
            {
                foreach (var item in Settings.Hotfix)
                {
                    if (text.Contains(item, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;

        }

        public static List<string> NormText(List<string> turns, bool aiDoctor)
        {
            List<string> newTurns = new List<string>();

            foreach (string turn in turns)
            {
                if (aiDoctor)
                {
                    if (turn.Contains(Settings.PatientTag))
                    {
                        newTurns.Add(turn);
                        continue;
                    }
                }
                else
                {
                    if (turn.Contains(Settings.DoctorTag))
                    {
                        newTurns.Add(turn);
                        continue;
                    }
                }

                string text = turn.Trim();
                // Apply hotfix
                if (Settings.Hotfix != null)
                {
                    foreach (var item in Settings.Hotfix)
                    {
                        if (turn.Contains(item, StringComparison.OrdinalIgnoreCase))
                        {
                            if (aiDoctor)
                            {
                                text = $"{Settings.DoctorTag} Mm-hmm.";
                            }
                            else
                            {
                                text = $"{Settings.PatientTag} Mm-hmm.";
                            }
                            break;
                        }
                    }
                }

                // Fix casing problem
                string[] words = text.Split(' ');
                List<string> newWords = new List<string>();
                foreach (var word in words)
                {
                    if ((word.Length == 2 || (word.Length == 3 && (word[2] == '.' || word[2] == '?' || word[2] == ','))) && word == word.ToUpper())
                    {
                        newWords.Add(word.ToLower());
                    }
                    else
                    {
                        newWords.Add(word);
                    }
                }

                text = String.Join(" ", newWords);

                newTurns.Add(text);
            }

            return newTurns;
        }

        public static List<string> AddHtmlTags(List<string> lines, bool isMale)
        {
            string gender = (isMale == true) ? "male" : "female";

            List<string> newLines = new List<string>();
            foreach (string line in lines)
            {
                string newLine = "";
                if (line.Contains(Settings.DoctorTag))
                {
                    newLine = line.Replace(Settings.DoctorTag, $"<div class=\"ai-message\"><img src=\"/images/doctor.jpg\" width=\"50\">");
                }
                else
                {
                    newLine = line.Replace(Settings.PatientTag, $"<div class=\"user-message\">") + $"<img src=\"/images/patient_{gender}.jpg\" width=\"50\">";
                }

                newLine = newLine.Replace("\n", "\n<br>");
                newLine = newLine.Replace("__lf__", " <br>");
                newLines.Add(newLine);
            }

            return newLines;
        }

        public static string RemoveHtmlTags(string input)
        {
            if (String.IsNullOrEmpty(input))
            {
                return "";
            }

            input = input.Replace($"<div class=\"user-message\">", Settings.PatientTag).Replace($"<div class=\"ai-message\">", Settings.DoctorTag);
            return Regex.Replace(input, "<.*?>", String.Empty);
        }
    }
}
