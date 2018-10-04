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
    public class S_DEAD_LOCATION : ParsedMessage
    {
        public EntityId EntityId { get; set; }
        public Vector3f Location { get; set; }

        public S_DEAD_LOCATION(TeraMessageReader reader) : base(reader)
        {
            EntityId = reader.ReadEntityId();
            Location = reader.ReadVector3f();
        }
    }
}
