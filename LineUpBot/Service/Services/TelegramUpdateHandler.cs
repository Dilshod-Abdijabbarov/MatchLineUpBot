using LineUpBot.Context.MatchDbContext;
using LineUpBot.Domain.Models;
using LineUpBot.Service.IServices;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace LineUpBot.Service.Services
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
        public async Task HandleAsync(Update update)
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
            }
        }

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

                //await _botClient.AnswerCallbackQuery(
                //    callback.Id,
                //    text: "⏳",
                //    showAlert: false
                //);

                int? surveyId = null;
                if (parts.Length > 1 && int.TryParse(parts[1], out var id))
                    surveyId = id;

                //await _botClient.AnswerCallbackQuery(callback.Id);

                switch (action)
                {
                    case "CREATE_POLL":
                        await HandleCreatePoll(chatId,callback);
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
                // Telegramni yopib qo‘yamiz
                await _botClient.AnswerCallbackQuery(
                    callback.Id,
                    "❌ Xatolik yuz berdi",
                    showAlert: true
                );
            }
        }

        private async Task HandleCreatePoll(long chatId, CallbackQuery callback)
        {
            var currentWeek = GetWeekNumber();
            var survey = await _dbContext.Surveys.FirstOrDefaultAsync(s => s.IsActive && s.CurrentWeek == currentWeek);

            if (survey == null)
            {
                survey = new Survey
                {
                    Question = $"<b>⚽ ⚽ ⚽ FUTBOL ⚽ ⚽ ⚽\nJuma({GetFridayDate()}) kuni soat 19:00 da futbolga kimlar boradi?</b>",
                    CurrentWeek = currentWeek,
                };

                await _dbContext.Surveys.AddAsync(survey);
                await _dbContext.SaveChangesAsync();
            }

            var groupId = await _groupService.CreateGroup(survey.Id);
            var users = await _userService.GetUsersByGroupIdAsync(groupId) ?? new List<BotUser>();

            var message = await _botClient.SendMessage(
                chatId: chatId,
                text: BuildPollTextHtml(users),
                parseMode: ParseMode.Html,
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

        private string GetFridayDate()
        {
            var today = DateTime.UtcNow;
            int daysUntilFriday = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
            var fridayDate = today.AddDays(daysUntilFriday);
            return fridayDate.ToString("dd.MM.yyyy");
        }
        private int GetWeekNumber()
        {
            var calendar = CultureInfo.InvariantCulture.Calendar;

            return calendar.GetWeekOfYear(
                DateTime.UtcNow,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday
            );
        }
        private async Task HandlePollVote(CallbackQuery callback, int surveyId,  bool isGoing)
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
            }

        }

        private string BuildPollTextHtml(List<BotUser> goingUsers)
        {
            var sb = new StringBuilder();

            // Sarlavha
            sb.AppendLine("<b>⚽ FUTBOL ⚽</b>");
            sb.AppendLine(" ━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine(
                $"<b>    📅 Juma, {GetFridayDate()}</b>\n" +
                $"<b>    ⏰ Soat: 19:00</b>\n"
            );

            // Ro'yxat sarlavhasi
            sb.AppendLine("<b>❓ Futbolga kim boradi?</b>");
            sb.AppendLine(" ━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("<b>📋 Hozirgi ro‘yxat:</b>");

            if (!goingUsers?.Any() ?? true)
            {
                sb.AppendLine("— Hozircha hech kim yo'q");
            }
            else
            {
                int i = 1;
                foreach (var user in goingUsers)
                {
                    var name = FormatUserNameHtml(user);

                    sb.AppendLine($"{i}. {name}");
                    i++;
                }

                sb.AppendLine("━━━━━━━━━━━━━━━");
                sb.AppendLine($"👥 <b>Jami:</b> <u>{i - 1}</u> kishi");
            }

            return sb.ToString();
        }

        private string FormatUserNameHtml(BotUser user)
        {
            if (user == null)
                return "Noma'lum";

            // Username 
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
