using LineUpBot.Context.MatchDbContext;
using LineUpBot.Domain.Enums;
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

            if (update.Message?.Text == null) return;

            if (update.Message?.From == null) return;

            var chatId = update.Message.Chat?.Id ?? 0;

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
                    if(user?.UserRole == UserRole.Admin || user?.UserRole == UserRole.SuperAdmin)
                        await GetUsersList(chatId, 0);

                    break;

                case "/team":
                    if (user?.UserRole == UserRole.Admin || user?.UserRole == UserRole.SuperAdmin)
                    {
                        user = user ?? await _dbContext.BotUsers
                               .FirstOrDefaultAsync(x => x.TelegramUserChatId == chatId);

                        user?.NextCommand = "CREATE_TEAM";
                        await _dbContext.SaveChangesAsync();

                        await _botClient.SendMessage(
                            chatId,
                            "Jamoalar sonini kiriting:"
                        );
                    }

                    break;

                case string s when s.StartsWith("/addadmin"):
                    if (user?.UserRole == UserRole.SuperAdmin)
                        await AddAdminAsync(chatId, user, update.Message.Text);
                    
                    break;

                case string s when s.StartsWith("/setup_"):
                    if(user?.UserRole == UserRole.SuperAdmin)
                        await SetInitialSuperAdmin(chatId, user, s);
                    
                    break;

                default:
                    // Faqat shaxsiy chat bo'lsa, xato haqida xabar chiqarsin
                    if (update.Message.Chat.Type == ChatType.Private)
                    {
                        await _botClient.SendMessage(
                            chatId,
                            "Bunday buyruq mavjud emas."
                        );
                    }
                    // Guruhda bo'lsa, hech narsa yubormasdan shunchaki break bo'ladi
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

                // (guruh yoki shaxsiy) ID-si
                long telegramGroupChatId = callback.Message.Chat.Id;

                // Tugmani bosgan foydalanuvchi ID-si
                long userChatId = callback.From.Id;

                if(userChatId != 0)
                     await _userService.CreateUserAsync(callback.From,telegramGroupChatId);

                var user = await _userService.GetByUserChatId(userChatId);

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
                        if (parts.Length > 2 && (user?.UserRole == UserRole.Admin || user?.UserRole == UserRole.SuperAdmin))
                        {
                            int currentPage = int.Parse(parts[2]);
                            await UpdateUserScoreAndRefreshList(callback, long.Parse(parts[1]), 2, currentPage);
                        }
                        
                        break;

                    case "SCORE_MINUS":
                        if (parts.Length > 2 && (user.UserRole == UserRole.Admin || user.UserRole == UserRole.SuperAdmin))
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
                        if (parts.Length > 3 && (user.UserRole == UserRole.Admin || user.UserRole == UserRole.SuperAdmin))
                        {
                            long targetId = long.Parse(parts[1]);
                            int delta = int.Parse(parts[2]);
                            int page = int.Parse(parts[3]);
                            await UpdateUserScoreAndRefreshList(callback, targetId, delta, page);
                        }
                       

                        break;
                }
            }
            catch (Exception ex)
            {
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
                    parseMode: ParseMode.Html,
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

            sb.AppendLine("<b>⚽ FUTBOL ⚽</b>");
            sb.AppendLine(" ━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine(
                $"<b>📅 Juma, {GetFridayDate()}</b>\n" +
                $"<b>⏰ Soat: 20:00</b>\n"
            );

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
                return System.Net.WebUtility.HtmlEncode(user.FirstName);
            }

            return "Noma'lum";
        }

        private async Task GetUsersList(long chatId, int page = 0, int? messageId = null)
        {
            int pageSize = 10;
            if (page < 0) page = 0;

            var groupUsers = await _dbContext.TelegramGroups
                .Where(x => x.TelegramGroupChatId == chatId && x.Active)
                .Include(x=>x.BotUsers)
                .Select(x=>x.BotUsers)
                .FirstOrDefaultAsync();

            var totalUsers = groupUsers != null ? groupUsers.Count() : 0;    

             var users = groupUsers?
                .OrderByDescending(x => x.Score)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToList();
            
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
                await _botClient.SendMessage(chatId, text,
                    parseMode: ParseMode.Html, replyMarkup: keyboard);
            }
        }


        private InlineKeyboardMarkup BuildUsersKeyboard(List<BotUser> users, int page, int total, int pageSize)
        {
            var rows = new List<List<InlineKeyboardButton>>();

            if (users == null || users?.Count == 0)
            {
                return new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("❌ Foydalanuvchilar mavjud emas", "NONE")
                    }
                });
            }

            for (int i = 0; i < users.Count; i++)
            {
                var u = users[i];
                var rank = (page * pageSize) + i + 1;

                rows.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData($"{rank}. {u.FirstName ?? u.LastName} {u.UserName ?? ""}", "NONE"),
                    InlineKeyboardButton.WithCallbackData($"⭐ {u.Score}", "NONE"),
                    InlineKeyboardButton.WithCallbackData("➕", $"SCORE:{u.TelegramUserChatId}:2:{page}"),
                    InlineKeyboardButton.WithCallbackData("➖", $"SCORE:{u.TelegramUserChatId}:-1:{page}")
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
            var user = await _dbContext.BotUsers.FirstOrDefaultAsync(x => x.TelegramUserChatId == targetUserChatId);

            if (user == null)
            {
                await _botClient.AnswerCallbackQuery(callback.Id, "❌ Foydalanuvchi topilmadi", showAlert: true);
                return;
            }

            user.Score += delta;
            if (user.Score <= 0)
                user.Score = 0;

            await _dbContext.SaveChangesAsync();

            long telegramGroupChatId = callback.Message.Chat.Id;

            var groupUsers = await _dbContext.TelegramGroups
                .Where(x => x.TelegramGroupChatId == telegramGroupChatId && x.Active)
                .Include(x => x.BotUsers)
                .FirstOrDefaultAsync();

            int pageSize = 10;
            var totalUsers = groupUsers != null ? groupUsers.BotUsers.Count() : 0;

            var users = groupUsers?.BotUsers
                .OrderByDescending(x => x.Score)
                .Skip(currentPage * pageSize)
                .Take(pageSize)
                .ToList();

            var keyboard = BuildUsersKeyboard(users, currentPage, totalUsers, pageSize);

            try
            {
                await _botClient.EditMessageText(
                    chatId: callback.Message.Chat.Id,
                    messageId: callback.Message.MessageId,
                    text: $"🏆 <b>Foydalanuvchilar reytingi</b> (Sahifa: {currentPage + 1})",
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard
                );
            }
            catch (Exception)
            {
                await _botClient.AnswerCallbackQuery(callback.Id);
            }
        }

        public async Task GenerateBalancedTeams(long chatId, int numberOfTeams)
        {
            var currentWeek = await _botMenuService.GetWeekNumber();

            var survey = await _dbContext.Surveys
                .Where(x => x.TelegramGroup.TelegramGroupChatId == chatId && x.IsActive && x.CurrentWeek == currentWeek)
                .FirstOrDefaultAsync();

            var users = await _dbContext.SurveyBotUsers
                .Where(x => x.SurveyId == survey.Id && x.Active)
                .Include(x => x.BotUser)
                .Select(x => x.BotUser)
                .OrderByDescending(x=>x.Score)
                .ThenBy(x=> Guid.NewGuid())
                .ToListAsync();

            int membersCount = users.Count / numberOfTeams;

            var teams = new List<List<BotUser>>();
            for (int i = 0; i < numberOfTeams; i++) teams.Add(new List<BotUser>());

          var usersGroup = users.GroupBy(x=>x.Score)
                .OrderByDescending(x=>x.Key)
                .ThenBy(x=>x.Sum(u=>u.Score))
                .ToList();

            while (usersGroup.Count > 0)
            {
                var currentGroup = usersGroup.First();
                var groupUsers = currentGroup.ToList();
                int groupUserCount = groupUsers.Count;
                for (int i = 0; i < groupUserCount;i++)
                {
                    var user = groupUsers.OrderBy(x=> Guid.NewGuid()).First();

                    var currentTeam = teams
                        .OrderBy(x => x.Count)
                        .ThenBy(x => x.Sum(x => x.Score))
                        .ThenBy(x=> Guid.NewGuid())
                        .First();

                    currentTeam.Add(user);
                    groupUsers.Remove(user);
                }
                usersGroup.Remove(currentGroup);
            }


            var sb = new StringBuilder();
            sb.AppendLine("<b>  Teng kuchli jamoalar:</b>\n");

            if(users.Count % numberOfTeams != 0)
                sb.AppendLine("<b>🎭 Agar jamoalarda o'yinchilar soni teng bo'lmasa,\n o'yinchilar navbat bilan almashib o'ynashlari kerak.</b>\n");


            for (int i = 0; i < teams.Count; i++)
            {
                int totalScore = teams[i].Sum(u => u.Score);

                sb.AppendLine($"<b>Jamoa #{i + 1} (Jami ball: {totalScore})</b>");
                int number = 1;
                foreach (var u in teams[i])
                {
                    sb.AppendLine($" {number}.{u.FirstName} {u.UserName} {u.Score})");
                    number++;
                }

                sb.AppendLine();
            }


            await _botClient.SendMessage(
                chatId: chatId,
                text: sb.ToString(),
                parseMode: ParseMode.Html
            );

        }


        private async Task AddAdminAsync(long chatId, BotUser currentUser, string messageText)
        {
            if (currentUser.UserRole != UserRole.SuperAdmin)
            {
                await _botClient.SendMessage(chatId, "Kechirasiz, ushbu buyruqdan foydalanish uchun sizda yetarli ruxsatnoma mavjud emas.");
                return;
            }

            var parts = messageText.Split('@', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await _botClient.SendMessage(chatId, "Format xato. Namuna: `/addadmin @username`", parseMode: ParseMode.Markdown);
                return;
            }

            var targetUsername = parts[1].Replace("@", "").Trim();
            var targetUser = await _dbContext.BotUsers
                .FirstOrDefaultAsync(u => u.UserName != null && u.UserName == targetUsername);

            if (targetUser == null)
            {
                await _botClient.SendMessage(chatId, $"❌ @{targetUsername} foydalanuvchisi bazadan topilmadi.");
                return;
            }

            targetUser.UserRole = UserRole.Admin;
            await _dbContext.SaveChangesAsync();

            await _botClient.SendMessage(chatId, $"✅ @{targetUsername} muvaffaqiyatli **Admin** qilindi!", parseMode: ParseMode.Markdown);

            try
            {
                await _botClient.SendMessage(targetUser.TelegramUserChatId, "Tabriklaymiz! Siz botda **Admin** huquqini oldingiz.");
            }
            catch { /* Foydalanuvchi botni bloklagan bo'lishi mumkin */ }
        }

        private async Task SetInitialSuperAdmin(long chatId, BotUser user, string text)
        {
            string secretKey = "LineUpBotUz$Dilshod1898$";

            if (text == $"/setup_{secretKey}")
            {
                user.UserRole = UserRole.SuperAdmin;
                await _dbContext.SaveChangesAsync();
                await _botClient.SendMessage(chatId, "✅ Tizim aniqlandi. Siz endi **SuperAdmin**siz!", parseMode: ParseMode.Markdown);
            }
        }
    }
}
