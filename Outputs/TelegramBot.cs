using ServiceStack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace LNDInsights.Outputs
{
   

    public static class TelegramBot
    {
        static ITelegramBotClient BotClient;
        static Chat ChatId = null;
        static string SavedChatIdFilename = "chatid.json";

        public static bool IsReady { get; set; }

        public static async Task Start(string apiKey)
        {
            BotClient = new TelegramBotClient(apiKey);
            var me = BotClient.GetMeAsync().Result;

            BotClient.OnMessage += Bot_OnMessage; ;
            BotClient.StartReceiving();

            if (System.IO.File.Exists(SavedChatIdFilename))
            {
                ChatId = System.IO.File.ReadAllText(SavedChatIdFilename).FromJson<Chat>();
            }

            //Wait until we have a chatId
            while (ChatId == null)
            {
                await Task.Delay(1000);
            }
            IsReady = true;
        }

        public static async Task Stop()
        {
           await BotClient.CloseAsync();
        }

        public static async Task<Message> SendTextMessage(string message)
        {
            return await BotClient.SendTextMessageAsync(
                chatId: ChatId,
                text: message
               );
            
        }
        static bool FirstInboundMessageCompleted;

        static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (!FirstInboundMessageCompleted) //just to avoid annoying myself
            {
                ChatId = e.Message.Chat;
                System.IO.File.WriteAllText(SavedChatIdFilename, ChatId.ToJson());
                await BotClient.SendTextMessageAsync(
                  chatId: e.Message.Chat,
                  text: $"Thanks, we now have you chatId saved so future messages can be sent without your interaction # {e.Message.Chat.Id}"
                );
                FirstInboundMessageCompleted = true;
            }
        }
    }
}
