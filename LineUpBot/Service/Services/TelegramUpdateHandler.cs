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

            var user = await _dbContext.BotUsers
                        .FirstOrDefaultAsync(x => x.ChatId == chatId);

            if (user?.NextCommand == "CREATE_TEAM")
            {
                await CreateTeam(update, user);
                return;
            }

            switch (update.Message.Text)
            {
                case "/start":
                    await _botMenuService.SendMainMenu(chatId);
                    break;

                case "/users":
                    await SendUsersList(chatId);
                    break;

                case "/team":
                    user = user ?? await _dbContext.BotUsers
                              .FirstOrDefaultAsync(x => x.ChatId == chatId);

                    user.NextCommand = "CREATE_TEAM";
                    await _dbContext.SaveChangesAsync();

                    await _botClient.SendMessage(
                        chatId,
                        "Bitta jamoada nechta o'yinchi bo'lishini kiriting."
                    );
                    break;             
            }
        }


        private async Task CreateTeam(Update update,BotUser user)
        {
            if (int.TryParse(update.Message.Text, out int teamSize))
            {
                user.NextCommand = null;
                await _dbContext.SaveChangesAsync();

                await GenerateBalancedTeams(user.ChatId, teamSize);
            }
            else
            {
                await _botClient.SendMessage(
                    user.ChatId,
                    "Iltimos faqat raqam kiriting. Masalan: 5"
                );
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
                .ToListAsync();

            var keyboard = BuildUsersKeyboard(users);

            await _botClient.SendMessage(
                chatId: chatId,
                text: "🏆 <b>Foydalanuvchilar reytingi</b>",
                parseMode: ParseMode.Html,
                replyMarkup: BuildUsersKeyboard(users)
            );
        }

        private InlineKeyboardMarkup BuildUsersKeyboard(List<BotUser> users)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            var sorted = users
                .OrderByDescending(x => x.Score)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                var u = sorted[i];

                var rank = $"{i + 1}";

                rows.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(rank, "NONE"),
                    InlineKeyboardButton.WithCallbackData(u.FirstName, "NONE"),
                    InlineKeyboardButton.WithCallbackData($"⭐ {u.Score}", "NONE"),
                    InlineKeyboardButton.WithCallbackData("🟢", $"SCORE:{u.ChatId}:1"),
                    InlineKeyboardButton.WithCallbackData("🔴", $"SCORE:{u.ChatId}:-1")
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

            var users = await _dbContext.BotUsers
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

                //await _botClient.AnswerCallbackQuery(callback.Id, $"✅ {user.FirstName}: {user.Score}");
            }
            catch (Exception ex)
            {
                // Agar foydalanuvchi bir xil tugmani ko'p bossa va ball o'zgarmasa, 
                // Telegram "Message is not modified" xatosini beradi. Shuni oldini olamiz.
                await _botClient.AnswerCallbackQuery(callback.Id);
            }
        }

        public async Task GenerateBalancedTeams(long chatId, int usersPerTeam)
        {
            var rnd = new Random();

            // 1️⃣ Userlarni olish
            var users = await _dbContext.BotUsers.ToListAsync();

            // 2️⃣ Random aralashtirish
            users = users.OrderBy(x => Guid.NewGuid()).ToList();

            // 3️⃣ Score bo'yicha saralash (balans uchun)
            users = users.OrderByDescending(u => u.Score).ToList();

            int numberOfTeams = users.Count / usersPerTeam;
            int usersToUse = numberOfTeams * usersPerTeam;

            // 4️⃣ Eng kuchli userlarni ishlatamiz
            var selectedUsers = users.Take(usersToUse).ToList();
            var remainingUsers = users.Skip(usersToUse)
                                      .OrderBy(u => u.Score)
                                      .ToList();

            // 5️⃣ Jamoalar
            var teams = new List<List<BotUser>>();
            var teamScores = new int[numberOfTeams];

            for (int i = 0; i < numberOfTeams; i++)
                teams.Add(new List<BotUser>());

            // 6️⃣ Balanslash algoritmi
            foreach (var user in selectedUsers)
            {
                int bestTeam = -1;
                int minScore = int.MaxValue;

                for (int i = 0; i < numberOfTeams; i++)
                {
                    if (teams[i].Count >= usersPerTeam)
                        continue;

                    if (teamScores[i] < minScore)
                    {
                        minScore = teamScores[i];
                        bestTeam = i;
                    }
                }

                teams[bestTeam].Add(user);
                teamScores[bestTeam] += user.Score;
            }

            // 7️⃣ Natija chiqarish
            var sb = new StringBuilder();
            sb.AppendLine("<b>🎭 Teng kuchli jamoalar:</b>\n");

            for (int i = 0; i < teams.Count; i++)
            {
                sb.AppendLine($"<b>Jamoa #{i + 1} (Jami ball: {teamScores[i]})</b>");

                foreach (var u in teams[i])
                    sb.AppendLine($" └ {u.FirstName} ({u.Score})");

                sb.AppendLine();
            }

            // 8️⃣ Ortiqcha userlar (eng past score)
            if (remainingUsers.Any())
            {
                sb.AppendLine("<b>❌ Bu safar tushmaganlar:</b>");

                foreach (var u in remainingUsers)
                    sb.AppendLine($" └ {u.FirstName} ({u.Score})");
            }

            await _botClient.SendMessage(
                chatId: chatId,
                text: sb.ToString(),
                parseMode: ParseMode.Html
            );
        }
    }
}
