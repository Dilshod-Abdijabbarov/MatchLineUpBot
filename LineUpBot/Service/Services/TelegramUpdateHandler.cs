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

            var user = await _userService.GetByUserChatId(update.Message.From.Id);

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
                    if(user.UserRole == Domain.Enums.UserRole.Admin)
                        await GetUsersList(chatId, 0);
                    else
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "Sizda ushbu buyruqdan foydalanish huquqi yo‘q."
                        );
                    }

                    break;

                case "/team":
                    user = user ?? await _dbContext.BotUsers
                              .FirstOrDefaultAsync(x => x.TelegramUserChatId == chatId);

                    user.NextCommand = "CREATE_TEAM";
                    await _dbContext.SaveChangesAsync();

                    await _botClient.SendMessage(
                        chatId,
                        "Bitta jamoada nechta o'yinchi bo'lishini kiriting."
                    );
                    break;

                default:
                    await _botClient.SendMessage(
                        chatId,
                        "Bunday buyruq mavjud emas."
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

                await GenerateBalancedTeams(update.Message.Chat.Id, teamSize);
            }
            else
            {
                await _botClient.SendMessage(
                    update.Message.Chat.Id,
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

                // Bu o'sha xabarni ichida turgan chat (guruh yoki shaxsiy) ID-si
                long telegramGroupChatId = callback.Message.Chat.Id;

                // Tugmani bosgan foydalanuvchi ID-si
                long userChatId = callback.From.Id;

                if(userChatId != 0)
                     await _userService.CreateUserAsync(callback.From,telegramGroupChatId);

                var parts = callback.Data.Split(':');
                var action = parts[0];

                int? surveyId = null;
                if (parts.Length > 1 && int.TryParse(parts[1], out var id))
                    surveyId = id;

                switch (action)
                {
                    case "CREATE_POLL":
                        await HandleCreatePoll(telegramGroupChatId, callback);
                        break;

                    case "POLL_YES":
                        if (surveyId == null) return;
                        await HandlePollVote(callback, surveyId.Value, true);
                        break;

                    case "POLL_NO":
                        if (surveyId == null) return;
                        await HandlePollVote(callback, surveyId.Value, false);
                        break;

                    case "SCORE_PLUS":
                        // parts[1] - UserId, parts[2] - Joriy sahifa raqami
                        if (parts.Length > 2)
                        {
                            int currentPage = int.Parse(parts[2]);
                            await UpdateUserScoreAndRefreshList(callback, long.Parse(parts[1]), 1, currentPage);
                        }
                        break;

                    case "SCORE_MINUS":
                        if (parts.Length > 2)
                        {
                            int currentPage = int.Parse(parts[2]);
                            await UpdateUserScoreAndRefreshList(callback, long.Parse(parts[1]), -1, currentPage);
                        }
                        break;

                    case "PAGE":
                        if (parts.Length > 1 && int.TryParse(parts[1], out var newPage))
                        {
                            await GetUsersList(telegramGroupChatId, newPage, callback.Message.MessageId);
                        }
                        await _botClient.AnswerCallbackQuery(callback.Id);
                        break;

                    case "SCORE":
                        if (parts.Length > 3)
                        {
                            long targetId = long.Parse(parts[1]);
                            int delta = int.Parse(parts[2]);
                            int page = int.Parse(parts[3]); // Sahifa raqami
                            await UpdateUserScoreAndRefreshList(callback, targetId, delta, page);
                        }
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

        private async Task HandleCreatePoll(long telegramGroupChatId, CallbackQuery callback)
        {
            var groupId = await _groupService.CreateTelegramGroup(telegramGroupChatId);

            var currentWeek = GetWeekNumber();
            var survey =  await _dbContext.Surveys.FirstOrDefaultAsync(s => s.CurrentWeek == currentWeek && s.TelegramGroupId == groupId && s.IsActive);
            if (survey == null)
            {
                survey = new Survey
                {
                    CurrentWeek = currentWeek,
                    TelegramGroupId = groupId,
                    IsActive = true,
                    Question = $"<b>⚽ ⚽ ⚽ FUTBOL ⚽ ⚽ ⚽\nJuma({GetFridayDate()}) kuni soat 19:00 da futbolga kimlar boradi?</b>",
                    CreatedDate = DateTime.UtcNow
                };

                await _dbContext.Surveys.AddAsync(survey);
                await _dbContext.SaveChangesAsync();
            }

            var users = await _dbContext.SurveyBotUsers.Where(x => x.SurveyId == survey.Id && x.Active).Include(x => x.BotUser).Select(x => x.BotUser).ToListAsync();

            var message = await _botClient.SendMessage(
                chatId: telegramGroupChatId,
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
                var survey = await _dbContext.Surveys.FindAsync(surveyId);

                long telegramGroupChatId = callback.Message.Chat.Id;

                if (survey == null) return;

                var groupId = await _groupService.CreateTelegramGroup(telegramGroupChatId);

                var user = await _userService.GetByUserChatId(callback.From.Id);

                await _userService.AddUserToSursey(user.Id, surveyId, isGoing);

                var users = await _userService.GetUsersBySurveyIdAsync(surveyId);

                // HTML formatda text tayyorlash
                var pollText = BuildPollTextHtml(users);

                await _botClient.EditMessageText(
                    chatId: telegramGroupChatId,
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

        private async Task GetUsersList(long chatId, int page = 0, int? messageId = null)
        {
            int pageSize = 10;
            if (page < 0) page = 0;

            // Jami foydalanuvchilar sonini aniqlash (Pagination tugmalari uchun)
            var totalUsers = await _dbContext.BotUsers.CountAsync();

            var users = await _dbContext.BotUsers
                .OrderByDescending(x => x.Score)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var keyboard = BuildUsersKeyboard(users, page, totalUsers, pageSize);
            var text = "🏆 <b>Foydalanuvchilar reytingi</b>";

            if (messageId.HasValue)
            {
                // Tugma bosilganda mavjud xabarni tahrirlash
                await _botClient.EditMessageText(chatId, messageId.Value, text,
                    parseMode: ParseMode.Html, replyMarkup: keyboard);
            }
            else
            {
                // /users yozilganda yangi xabar yuborish
                await _botClient.SendMessage(chatId, text,
                    parseMode: ParseMode.Html, replyMarkup: keyboard);
            }
        }


        private InlineKeyboardMarkup BuildUsersKeyboard(List<BotUser> users, int page, int total, int pageSize)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            // Foydalanuvchilar qatorlari
            for (int i = 0; i < users.Count; i++)
            {
                var u = users[i];
                var rank = (page * pageSize) + i + 1;

                rows.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData(rank.ToString(), "NONE"),
            InlineKeyboardButton.WithCallbackData(u.FirstName ?? "User", "NONE"),
            InlineKeyboardButton.WithCallbackData($"⭐ {u.Score}", "NONE"),
            // Sahifani eslab qolish uchun page raqamini ham callbackga qo'shamiz
            InlineKeyboardButton.WithCallbackData("🟢", $"SCORE:{u.TelegramUserChatId}:1:{page}"),
            InlineKeyboardButton.WithCallbackData("🔴", $"SCORE:{u.TelegramUserChatId}:-1:{page}")
        });
            }

            // Navigatsiya tugmalari (⬅️ 📄 ➡️)
            var navRow = new List<InlineKeyboardButton>();

            // Orqaga tugmasi
            if (page > 0)
                navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"PAGE:{page - 1}"));

            // Joriy sahifa raqami
            navRow.Add(InlineKeyboardButton.WithCallbackData($"📄 {page + 1}", "NONE"));

            // Oldinga tugmasi
            if ((page + 1) * pageSize < total)
                navRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"PAGE:{page + 1}"));

            if (navRow.Count > 1 || (navRow.Count == 1 && navRow[0].Text != $"📄 {page + 1}"))
                rows.Add(navRow);

            return new InlineKeyboardMarkup(rows);
        }


        private async Task UpdateUserScoreAndRefreshList(CallbackQuery callback, long targetUserChatId, int delta, int currentPage)
        {
            // 1. Balli o'zgarayotgan foydalanuvchini topish
            var user = await _dbContext.BotUsers.FirstOrDefaultAsync(x => x.TelegramUserChatId == targetUserChatId);

            if (user == null)
            {
                await _botClient.AnswerCallbackQuery(callback.Id, "❌ Foydalanuvchi topilmadi", showAlert: true);
                return;
            }

            // 2. Ballni yangilash va saqlash
            user.Score += delta;
            await _dbContext.SaveChangesAsync();

            // 3. Pagination uchun ma'lumotlarni qayta hisoblash
            int pageSize = 10;
            var totalUsers = await _dbContext.BotUsers.CountAsync();

            var users = await _dbContext.BotUsers
                .OrderByDescending(x => x.Score) // Ball bo'yicha saralash muhim!
                .Skip(currentPage * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 4. Yangi keyboard yasash (joriy sahifa bilan)
            var keyboard = BuildUsersKeyboard(users, currentPage, totalUsers, pageSize);

            // 5. Xabarni tahrirlash (EditMessage)
            try
            {
                await _botClient.EditMessageText(
                    chatId: callback.Message.Chat.Id,
                    messageId: callback.Message.MessageId,
                    text: $"🏆 <b>Foydalanuvchilar reytingi</b> (Sahifa: {currentPage + 1})",
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard
                );

                // Tugma ostida kichik bildirishnoma chiqarish
               // await _botClient.AnswerCallbackQuery(callback.Id, $"✅ {user.FirstName}: {user.Score} ball");
            }
            catch (Exception)
            {
                // "Message is not modified" xatosini oldini olish
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
