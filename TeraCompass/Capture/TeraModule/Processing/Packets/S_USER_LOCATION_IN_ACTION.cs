using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Services;

namespace Capture.TeraModule.Processing.Packets
{
    public class S_USER_LOCATION_IN_ACTION : ParsedMessage
    {
        public Vector3f Position { get; set; }
        public EntityId EntityId { get; set; }
        internal S_USER_LOCATION_IN_ACTION(TeraMessageReader reader) : base(reader)
        {
            EntityId = reader.ReadEntityId();
            Position = reader.ReadVector3f();
        }
    }
}
