using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Services;

namespace Capture.TeraModule.Tera.Core.Game.Messages.Server
{
    public class S_SPAWN_COLLECTION : ParsedMessage
    {
        public EntityId EntityId { get; set; }
        public int CollectionId { get; set; }
        public int Amount { get; set; }
        public Vector3f Position { get; set; }
        public Angle Angle { get; set; }
        public bool Extractor { get; set; }
        public bool ExtractorDisabled { get; set; }
        internal S_SPAWN_COLLECTION(TeraMessageReader reader) : base(reader)
        {
            EntityId = reader.ReadEntityId();
            CollectionId = reader.ReadInt32();
            Amount = reader.ReadInt32();
            Position = reader.ReadVector3f();
            Angle = reader.ReadAngle();
            Extractor = reader.ReadBoolean();
            ExtractorDisabled = reader.ReadBoolean();
        }
    }
}
