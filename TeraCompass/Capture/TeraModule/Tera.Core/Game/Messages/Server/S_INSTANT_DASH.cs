﻿using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class S_INSTANT_DASH : ParsedMessage
    {
        internal S_INSTANT_DASH(TeraMessageReader reader) : base(reader)
        {
            Entity = reader.ReadEntityId();
            reader.Skip(12);//0?
            Position = reader.ReadVector3f();
            Heading = reader.ReadAngle();
//            Trace.WriteLine($"{Time.Ticks} {BitConverter.ToString(BitConverter.GetBytes(Entity.Id))}: {Finish} {Heading}");
        }

        public EntityId Entity { get; }
        public Vector3f Position { get; private set; }
        public Angle Heading { get; private set; }
    }
}