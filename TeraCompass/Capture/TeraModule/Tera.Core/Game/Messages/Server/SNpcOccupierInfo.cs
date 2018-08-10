using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class SNpcOccupierInfo : ParsedMessage

    {
        internal SNpcOccupierInfo(TeraMessageReader reader) : base(reader)
        {
            NPC = reader.ReadEntityId();
            Engager = reader.ReadEntityId();
            Target = reader.ReadEntityId();
        }
        public bool HasReset => Target == EntityId.Empty;
        public EntityId NPC { get; }
        public EntityId Engager { get; }
        public EntityId Target { get; }
    }
}