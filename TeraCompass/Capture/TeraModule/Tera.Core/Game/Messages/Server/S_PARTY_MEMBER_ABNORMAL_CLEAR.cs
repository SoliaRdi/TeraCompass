using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class SPartyMemberAbnormalClear : ParsedMessage
    {
        internal SPartyMemberAbnormalClear(TeraMessageReader reader) : base(reader)
        {
            //  PrintRaw();
        }
    }
}