using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class S_RETURN_TO_LOBBY : ParsedMessage
    {
        internal S_RETURN_TO_LOBBY(TeraMessageReader reader) : base(reader)
        {
        }
    }
}