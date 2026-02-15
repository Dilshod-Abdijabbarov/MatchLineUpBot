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
                case "/users":
                    await SendUsersList(chatId);
                    break;

                case "/team":
                    await GenerateBalancedTeams(chatId);
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

                int? surveyId = null;
                if (parts.Length > 1 && int.TryParse(parts[1], out var id))
                    surveyId = id;

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

                    // yoki bitta formatda ishlatsang:
                    case "SCORE":
                        if (parts.Length < 3) return;
                        await UpdateUserScoreAndRefreshList(callback, long.Parse(parts[1]), int.Parse(parts[2]));
                        break;

                    case "SCORE_PLUS":
                        await UpdateUserScoreAndRefreshList(callback, long.Parse(parts[1]), +1);
                        break;

                    case "SCORE_MINUS":
                        await UpdateUserScoreAndRefreshList(callback, long.Parse(parts[1]), -1);
                        break;

                    case "NONE":
                        // Hech qanday amal bajarmaymiz, shunchaki callbackni yopamiz
                        await _botClient.AnswerCallbackQuery(callback.Id);
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
            var survey =  await _dbContext.Surveys.FirstOrDefaultAsync(s => s.IsActive && s.CurrentWeek == currentWeek);
            if (survey == null)
            {
                survey = new Survey
                {
                    Question = $"<b>⚽ ⚽ ⚽ FUTBOL ⚽ ⚽ ⚽\nJuma({GetFridayDate()}) kuni soat 19:00 da futbolga kimlar boradi?</b>",
                    CurrentWeek = currentWeek,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
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
                $"<b>📅 Juma, {GetFridayDate()}</b>\n" +
                $"<b>⏰ Soat: 19:00</b>\n"
            );

            // Ro'yxat sarlavhasi
            sb.AppendLine("<b>❓ Futbolga kim boradi?</b>");
            sb.AppendLine(" ━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine("<b>📋 Boradiganlar ro‘yxati:</b>");

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

                sb.AppendLine(" ━━━━━━━━━━━━━━━━━━━━━");
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

        private async Task SendUsersList(long chatId)
        {
            var users = await _dbContext.BotUsers
                .OrderByDescending(x => x.Score)
                .ToListAsync();

            // Text yubormaymiz, faqat keyboard
            var keyboard = BuildUsersKeyboard(users);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "<b>Foydalanuvchilar ro'yxati:</b>",
                parseMode: ParseMode.Html,
                replyMarkup: keyboard
            );
        }

        private InlineKeyboardMarkup BuildUsersKeyboard(List<BotUser> users)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            foreach (var u in users)
            {
                // 1. Ma'lumot tugmasi (Bosilganda hech narsa qilmaydi)
                string infoText = $"👤 {u.FirstName} (@{u.UserName}) | ⭐ {u.Score}";

                rows.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(infoText, "NONE")
                });
               
                        // 2. Boshqaruv tugmalari (Pastki qatorda yonma-yon)
                        rows.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("➕ 1 ball", $"SCORE:{u.ChatId}:1"),
                    InlineKeyboardButton.WithCallbackData("➖ 1 ball", $"SCORE:{u.ChatId}:-1")
                });
            }

            return new InlineKeyboardMarkup(rows);
        }

        private async Task UpdateUserScoreAndRefreshList(CallbackQuery callback, long chatId, int delta)
        {
            var user = await _dbContext.BotUsers.FirstOrDefaultAsync(x => x.ChatId == chatId);
            
            if (user == null)
            {
                await _botClient.AnswerCallbackQuery(callback.Id, "❌ User topilmadi", true);
                return;
            }

            // 2. Ballni yangilaymiz
            user.Score += delta;

            await _dbContext.SaveChangesAsync();

            // 3. Yangilangan ro'yxatni ballar bo'yicha saralab olamiz
            var users = await _dbContext.BotUsers
                .OrderByDescending(x => x.Score)
                .ToListAsync();

            // 4. Yangi keyboard yasaymiz (ichida yangi ballar bilan)
            var keyboard = BuildUsersKeyboard(users);

            // 5. Xabarni yangilaymiz
            try
            {
                await _botClient.EditMessageText(
                    chatId: callback.Message.Chat.Id,
                    messageId: callback.Message.MessageId,
                    text: "<b>Foydalanuvchilar reytingi</b>",
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard
                );

                await _botClient.AnswerCallbackQuery(callback.Id, $"✅ {user.FirstName}: {user.Score}");
            }
            catch (Exception ex)
            {
                // Agar foydalanuvchi bir xil tugmani ko'p bossa va ball o'zgarmasa, 
                // Telegram "Message is not modified" xatosini beradi. Shuni oldini olamiz.
                await _botClient.AnswerCallbackQuery(callback.Id);
            }
        }

        public async Task GenerateBalancedTeams(long chatId)
        {
            // 1. Barcha userlarni ballari bo'yicha kamayish tartibida olamiz
            var users = await _dbContext.BotUsers
                .OrderByDescending(u => u.Score)
                .ToListAsync();

            //if (users.Count < 4)
            //    return "⚠️ Jamoa tuzish uchun kamida 4 ta foydalanuvchi kerak!";

            int usersPerTeam = 4;
            int numberOfTeams = users.Count / usersPerTeam; // Masalan: 13 user / 4 = 3 ta jamoa

            // Jamoalar ro'yxatini yaratamiz
            var teams = new List<List<BotUser>>();
            for (int i = 0; i < numberOfTeams; i++)
            {
                teams.Add(new List<BotUser>());
            }

            // 2. Snake Draft (Ilon izi) taqsimoti
            int currentTeam = 0;
            bool movingForward = true;

            for (int i = 0; i < users.Count; i++)
            {
                // Agar jamoalar to'lgan bo'lsa va userlar ortib qolsa (masalan 13-user), 
                // uni oxirgi jamoaga qo'shib qo'yamiz
                if (i >= numberOfTeams * usersPerTeam)
                {
                    teams[numberOfTeams - 1].Add(users[i]);
                    continue;
                }

                teams[currentTeam].Add(users[i]);

                if (movingForward)
                {
                    if (currentTeam == numberOfTeams - 1) movingForward = false;
                    else currentTeam++;
                }
                else
                {
                    if (currentTeam == 0) movingForward = true;
                    else currentTeam--;
                }
            }

            // 3. Natijani chiroyli formatda chiqarish
            var sb = new StringBuilder();
            sb.AppendLine("<b>🎭 Teng kuchli jamoalar tuzildi:</b>\n");

            for (int i = 0; i < teams.Count; i++)
            {
                int totalScore = teams[i].Sum(u => u.Score);
                sb.AppendLine($"<b>Jamoa #{i + 1} (Jami ball: {totalScore/4})</b>");

                foreach (var u in teams[i])
                {
                    sb.AppendLine($" └ {u.FirstName}");
                }
                sb.AppendLine(); // bo'sh qator
            }

            await _botClient.SendMessage(
                     chatId: chatId,
                     text: sb.ToString(),     // 👈 faqat text
                     parseMode: ParseMode.Html
            );
        }
    }
}
