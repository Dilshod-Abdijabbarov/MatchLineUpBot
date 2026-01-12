using LineUpBot.Enums;
using LineUpBot.IServices;
using LineUpBot.MatchDbContext;
using LineUpBot.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace LineUpBot.Services
{
    public class TelegramUpdateHandler : ITelegramUpdateHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly MatchLineUpDbContext _dbContext;
        private readonly BotMenuService _botMenuService;
        private readonly IUserService _userService;
        private readonly IGroupService _groupService;

        public TelegramUpdateHandler(ITelegramBotClient botClient,MatchLineUpDbContext dbContext, BotMenuService botMenuService, IUserService userSevice,IGroupService groupService)
        {
            _botClient = botClient;
            _dbContext = dbContext;
            _botMenuService = botMenuService;
            _userService = userSevice;
            _groupService = groupService;
        }
        public async Task HandleAsync(Telegram.Bot.Types.Update update)
        {
            if (update.CallbackQuery != null)
            {
                await HandleCallback(update.CallbackQuery);
                return;
            }

            if (update.Message?.Text == null)
                return;

            var chatId = update.Message.Chat.Id;

            switch (update.Message.Text)
            {
                case "/start":
                    await _botMenuService.SendMainMenu(chatId);
                    break;

                case "/createGroup":
                    await Group(update);
                    break;
            }
        }

        private async Task Group(Update update)
        {
            // 1️⃣ CallbackQuery har doim birinchi
            if (update.CallbackQuery != null)
            {
                await HandleCallback(update.CallbackQuery);
                return;
            }

            // 2️⃣ Message bo‘lmasa chiqib ket
            if (update.Message == null)
                return;

            var chatId = update.Message.Chat.Id;
            var userId = update.Message.From.Id;
            var text = update.Message.Text;

            // 3️⃣ /start
            if (text == "/start")
            {
                await _botMenuService.SendMainMenu(chatId);
                return;
            }

            //// 4️⃣ State tekshirish (GROUP CREATE)
            //var state = _userStateService.GetState(chatId);

            //if (state == UserState.WaitingForGroupName)
            //{
            //    await _groupService.CreateGroup(text);

            //    _userStateService.Clear(userId);

            //    await _botClient.SendMessage(
            //        chatId,
            //        "✅ Guruh muvaffaqiyatli yaratildi"
            //    );
            //    return;
            //}

            // 5️⃣ Boshqa textlar (ixtiyoriy)
            await _botClient.SendMessage(
                chatId,
                "Buyruqni tushunmadim"
            );
        }

        //private async Task HandleCallback(CallbackQuery callback)
        //{
        //    var chatId = callback.Message.Chat.Id;
        //    var userId = callback.From.Id;

        //    if (callback.Data == "CREATE_GROUP")
        //    {
        //        var state = _userStateService.GetState(userId);

        //        // ⛔ allaqachon kutyapmiz — qayta yuborma
        //        if (state == UserState.WaitingForGroupName)
        //            return;

        //        _userStateService.SetState(
        //           userId,
        //           UserState.WaitingForGroupName
        //       );

        //        await _botClient.SendMessage(
        //            chatId,
        //            "✍️ Guruh nomini yuboring:"
        //        );
        //    }


        //}

        private async Task HandleCallback(CallbackQuery callback)
        {
            try
            {
                if (callback.Message == null || string.IsNullOrEmpty(callback.Data))
                {
                    await _botClient.AnswerCallbackQuery(callback.Id);
                    return;
                }

                var chatId = callback.Message.Chat.Id;
                var userId = callback.From.Id;

                var parts = callback.Data.Split(':');
                var action = parts[0];

                await _botClient.AnswerCallbackQuery(
                    callback.Id,
                    text: "⏳",
                    showAlert: false
                );

                int? surveyId = null;
                if (parts.Length > 1 && int.TryParse(parts[1], out var id))
                    surveyId = id;

                await _botClient.AnswerCallbackQuery(callback.Id);

                switch (action)
                {
                    //case "CREATE_GROUP":
                    //    await HandleCreateGroup(chatId, userId);
                    //    break;

                    case "CREATE_POLL":
                        await HandleCreatePoll(chatId);
                        break;

                    case "POLL_YES":
                        if (surveyId == null) return;
                        await HandlePollVote(callback, surveyId.Value, true);
                        break;

                    case "POLL_NO":
                        if (surveyId == null) return;
                        await HandlePollVote(callback, surveyId.Value, false);
                        break;
                }
            }
            catch (Exception ex)
            {
                // 🔥 XATOLIKNI KO‘RISH UCHUN
                Console.WriteLine(ex);

                // Telegramni ham yopib qo‘yamiz
                await _botClient.AnswerCallbackQuery(
                    callback.Id,
                    "❌ Xatolik yuz berdi",
                    showAlert: true
                );
            }
        }

        private async Task HandleCreateGroup(long chatId, long userId)
        {
            var state = _userService.GetState(userId);

            if (state == UserState.WaitingForGroupName)
                return;

            _userService.SetState(
                userId,
                UserState.WaitingForGroupName
            );

            await _botClient.SendMessage(
                chatId,
                "✍️ Guruh nomini kiriting:"
            );
        }

        private async Task HandleCreatePoll(long chatId)
        {
            var survey = new Survey
            {
                Question = "⚽ Ertaga futbolga kim boradi?",
            };

            await _dbContext.Surveys.AddAsync(survey);
            await _dbContext.SaveChangesAsync();

            var message = await _botClient.SendMessage(
                chatId,
                BuildPollText(new List<BotUser>()),
                parseMode: ParseMode.Markdown,
                replyMarkup: new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            "✅ Boraman",
                            $"POLL_YES:{survey.Id}"
                        ),
                        InlineKeyboardButton.WithCallbackData(
                            "❌ Bormayman",
                            $"POLL_NO:{survey.Id}"
                        )
                    }
                })
            );
            survey.MessageId = message.MessageId;
            _dbContext.Surveys.Update(survey);
            await _dbContext.SaveChangesAsync();
        }

        private async Task HandlePollVote( CallbackQuery callback, int surveyId,  bool isGoing)
        {
            try
            {
                var chatId = callback.Message.Chat.Id;
                var survey = await _dbContext.Surveys.FindAsync(surveyId);

                if (survey == null) return;

                var user = await _userService.GetOrCreateOrUpdateAsync(callback.From);
                var groupId = await _groupService.CreateGroup(surveyId);
                await _groupService.AddUserToGroup(user.ChatId, groupId, isGoing);
                var users = await _userService.GetUsersByGroupIdAsync(groupId);

                // HTML formatda text tayyorlash
                var pollText = BuildPollTextHtml(users);

                await _botClient.EditMessageText(
                    chatId: chatId,
                    messageId: survey.MessageId ?? 0,
                    text: pollText,
                    parseMode: ParseMode.Html, // HTML format ishlatamiz
                    replyMarkup: callback.Message.ReplyMarkup
                );
            }
            catch (ApiRequestException ex)
            {
                if (!ex.Message.Contains("too old") && !ex.Message.Contains("not modified"))
                {
                    throw;
                }
                // Eskirgan yoki o'zgartirilmagan xabarlar uchun ignore
            }

        }

        private string BuildPollTextHtml(List<BotUser> goingUsers)
        {
            var sb = new StringBuilder();

            // Sarlavha
            sb.AppendLine("<b>⚽ Ertaga futbolga kim boradi?</b>");
            sb.AppendLine(); // Bo'sh qator

            // Ro'yxat sarlavhasi
            sb.AppendLine("<b>📋 Hozirgi ro'yxat:</b>");
            sb.AppendLine(); // Bo'sh qator

            if (!goingUsers?.Any() ?? true)
            {
                sb.AppendLine("— Hozircha hech kim yo'q");
            }
            else
            {
                int i = 1;
                foreach (var user in goingUsers)
                {
                    // Foydalanuvchi nomini formatlash
                    var name = FormatUserNameHtml(user);

                    // Raqam va nomni qo'shamiz
                    sb.AppendLine($"{i}. {name}");
                    i++;
                }

                sb.AppendLine($"<b> Jami: <u>{i-1}</u> kishi</b>");
            }

            return sb.ToString();
        }

        private string FormatUserNameHtml(BotUser user)
        {
            if (user == null)
                return "Noma'lum";

            // Username bo'lsa link qilamiz
            if (!string.IsNullOrEmpty(user.UserName))
            {
                return $"<a>{user.FirstName}  (@{user.UserName})</a>";
            }
            else if (!string.IsNullOrEmpty(user.FirstName))
            {
                // Faqat ism
                return System.Net.WebUtility.HtmlEncode(user.FirstName);
            }

            return "Noma'lum";
        }

        private string BuildPollText(List<BotUser> goingUsers)
        {
            var sb = new StringBuilder();

            sb.AppendLine("⚽ *Ertaga futbolga kim boradi?*\n");
            sb.AppendLine("📋 *Hozirgi ro‘yxat:*\n");

            if (!goingUsers.Any())
            {
                sb.AppendLine("— Hozircha hech kim yo‘q");
            }
            else
            {
                int i = 1;
                foreach (var user in goingUsers)
                {
                    var name = !string.IsNullOrEmpty(user.UserName)
                        ? $"@{user.UserName}"
                        : user.FirstName;

                    sb.AppendLine($"{i}. {name}");
                    i++;
                }
            }

            return sb.ToString();
        }

    }
}
