using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Processing.Packets
{
    public class C_PLAYER_FLYING_LOCATION : ParsedMessage
    {
        public Vector3f Position { get; set; }
        public Vector3f Destination { get; set; }
        public int Type { get; set; }
        public Angle Heading { get; set; }
        internal C_PLAYER_FLYING_LOCATION(TeraMessageReader reader) : base(reader)
        {
            Type = reader.ReadInt32();
            Position = reader.ReadVector3f();
            Destination = reader.ReadVector3f();
        }
    }
}
