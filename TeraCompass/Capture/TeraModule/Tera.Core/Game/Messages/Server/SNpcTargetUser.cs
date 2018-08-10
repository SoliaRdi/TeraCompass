using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class SNpcTargetUser : ParsedMessage

    {
        internal SNpcTargetUser(TeraMessageReader reader) : base(reader)
        {
            NPC = reader.ReadEntityId();
        }

        public EntityId NPC { get; private set; }
    }
}