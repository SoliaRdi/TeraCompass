using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Services;

namespace Capture.TeraModule.Tera.Core.Game.Messages.Server
{
    public class S_DESPAWN_COLLECTION : ParsedMessage
    {
        public EntityId EntityId { get; set; }
        internal S_DESPAWN_COLLECTION(TeraMessageReader reader) : base(reader)
        {
            EntityId = reader.ReadEntityId();
        }
    }
}