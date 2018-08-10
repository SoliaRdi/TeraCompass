using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class S_LEAVE_PARTY : ParsedMessage
    {
        internal S_LEAVE_PARTY(TeraMessageReader reader) : base(reader)
        {
        }
    }
}