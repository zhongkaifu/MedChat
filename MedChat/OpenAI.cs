using OpenAI_API.Models;

namespace MedChat
{
    public static class OpenAI
    {
        public static string OpenAIKeyString = "";
        public static string Call(List<string> turns, bool aiDoctor, string language = "en", bool noMoreQuestion = false)
        {
            var api = new OpenAI_API.OpenAIAPI(OpenAIKeyString);
            var chat = api.Chat.CreateConversation();
            chat.Model = Model.ChatGPTTurbo;
            chat.RequestParameters.Temperature = 0;

            if (aiDoctor)
            {
                if (language == "zh")
                {
                    if (noMoreQuestion)
                    {
                        chat.AppendSystemMessage("您是一名医生，正在远程医疗就诊中与患者交谈。 请根据与病人的交谈信息，对于病人的主诉和医疗健康状况给出诊断，意见和建议。");
                    }
                    else
                    {
                        chat.AppendSystemMessage("您是一名医生，正在远程医疗就诊中与患者交谈。 如果你对病人有任何问题，请每次只询问病人一个问题。");
                    }
                }
                else
                {
                    if (noMoreQuestion)
                    {
                        chat.AppendSystemMessage("You are a doctor and having a conversation with the patient in telehealth visiting. According to the conversation between you and the patient, please give diagnosis, treatment, plan and suggestion to the patient for the patient's chief complaint and health conditions.");
                    }
                    else
                    {
                        chat.AppendSystemMessage("You are a doctor and having a conversation with the patient in telehealth visiting. If you have question to the patient, you should ask the patient once a time.");
                    }
                    
                }
            }
            else
            {
                if (language == "zh")
                {
                    chat.AppendSystemMessage("您是一名患者，正在远程医疗就诊中与医生交谈。");
                }
                else
                {
                    chat.AppendSystemMessage("You are a patient and having a conversation with the doctor in telehealth visiting.");
                }
            }

            foreach (var turn in turns)
            {
                if (turn.Contains("[doctor]"))
                {
                    string text = turn.Replace("[doctor]", "").Trim();
                    if (text.Length > 0)
                    {
                        if (aiDoctor)
                        {
                            chat.AppendExampleChatbotOutput(turn);
                        }
                        else
                        {
                            chat.AppendUserInput(turn);
                        }
                    }
                }
                else
                {
                    string text = turn.Replace("[patient]", "").Trim();
                    if (text.Length > 0)
                    {
                        if (aiDoctor)
                        {
                            chat.AppendUserInput(turn);
                        }
                        else
                        {
                            chat.AppendExampleChatbotOutput(turn);
                        }
                    }
                }
            }

            // and get the response
            string response = chat.GetResponseFromChatbotAsync().Result.ToString();
            return response;
        }

        
    }
}
