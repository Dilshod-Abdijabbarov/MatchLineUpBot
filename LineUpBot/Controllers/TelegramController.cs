using LineUpBot.Service.IServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace LineUpBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TelegramController : ControllerBase
    {
        private readonly ITelegramUpdateHandler _updateHandler;

        public TelegramController(ITelegramUpdateHandler updateHandler)
        {
            _updateHandler = updateHandler;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Update update)
        {
            await _updateHandler.HandleAsync(update);
            return Ok();
        }
    }
}
