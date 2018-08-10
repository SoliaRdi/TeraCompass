using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Client
{
    public class C_PLAYER_LOCATION : ParsedMessage
    {
        internal C_PLAYER_LOCATION(TeraMessageReader reader) : base(reader)
        {
            Position = reader.ReadVector3f();
            Heading = reader.ReadAngle();
           
        }
        public Vector3f Position { get; private set; }
        public Angle Heading { get; private set; }

    }
}