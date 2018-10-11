﻿using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class S_CHANGE_DESTPOS_PROJECTILE : ParsedMessage
    {
        internal S_CHANGE_DESTPOS_PROJECTILE(TeraMessageReader reader)
            : base(reader)
        {
            Id = reader.ReadEntityId();
            Finish = reader.ReadVector3f();
            //Trace.WriteLine($"{Time.Ticks} {BitConverter.ToString(BitConverter.GetBytes(Id.Id))} - > {Finish}");
        }

        public Vector3f Finish { get; set; }
        public EntityId Id { get; private set; }
    }
}