using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class S_START_COOLTIME_SKILL : ParsedMessage
    {
        internal S_START_COOLTIME_SKILL(TeraMessageReader reader) : base(reader)
        {
            //PrintRaw();
            SkillId = new SkillId(reader).Id;
            Cooldown = reader.ReadInt32();
            //Trace.WriteLine("cooldown: SkillId = "+SkillId+"; Cooldown:"+Cooldown+"; hasResetted:"+HasResetted);
        }

        public int SkillId { get; private set; }
        public int Cooldown { get; private set; }

        public bool HasResetted => Cooldown == 0;
    }
}