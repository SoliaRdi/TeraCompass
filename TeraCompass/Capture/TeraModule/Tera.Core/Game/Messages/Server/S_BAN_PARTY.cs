using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class S_BAN_PARTY : ParsedMessage
    {
        internal S_BAN_PARTY(TeraMessageReader reader) : base(reader)
        {
        }
    }
}