using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Services;

namespace Capture.TeraModule.Processing.Packets
{
    public class S_CHANGE_RELATION : ParsedMessage
    {
        public EntityId EntityId { get; set; }
        public int Relation { get; set; }

        public S_CHANGE_RELATION(TeraMessageReader reader) : base(reader)
        {
            EntityId = reader.ReadEntityId();
            Relation = reader.ReadInt32();
        }
    }
}
