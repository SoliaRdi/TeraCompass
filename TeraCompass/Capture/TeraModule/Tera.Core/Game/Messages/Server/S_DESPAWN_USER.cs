using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class SDespawnUser : ParsedMessage
    {
        internal SDespawnUser(TeraMessageReader reader) : base(reader)
        {
            User = reader.ReadEntityId();
        }

        public EntityId User { get; }
    }
}