using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class SCreatureChangeHp : ParsedMessage
    {
        internal SCreatureChangeHp(TeraMessageReader reader) : base(reader)
        {
            HpRemaining = reader.ReadInt64();
            TotalHp = reader.ReadInt64();
            HpChange = reader.ReadInt64();
            Type = reader.ReadInt32();
            TargetId = reader.ReadEntityId();
            SourceId = reader.ReadEntityId();
            Critical = reader.ReadByte();
            AbnormalId = reader.ReadInt32();
            //Trace.WriteLine("target = " + TargetId + ";Source:" + SourceId + ";Critical:" + Critical + ";Hp left:" + HpRemaining + ";Max HP:" + TotalHp+";HpLost/Gain:"+ HpChange + ";Type:"+ Type + ";dot:"+AbnormalId);
        }

        public int Unknow3 { get; }
        public long HpChange { get; }

        public int Type { get; }


        public long HpRemaining { get; }

        public long TotalHp { get; }

        public int Critical { get; }

        public int AbnormalId { get; }

        public EntityId TargetId { get; }
        public EntityId SourceId { get; }
        public bool Slaying => TotalHp > HpRemaining*2 && HpRemaining > 0;
    }
}