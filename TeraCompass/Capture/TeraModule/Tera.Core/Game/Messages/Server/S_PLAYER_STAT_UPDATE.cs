using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Tera.Core.Game.Messages.Server
{
    public class S_PLAYER_STAT_UPDATE : ParsedMessage
    {
        internal S_PLAYER_STAT_UPDATE(TeraMessageReader reader) : base(reader)
        {
            HpRemaining = reader.ReadInt64();
            MpRemaining = reader.ReadInt32();
            reader.Skip(8);
            TotalHp = reader.ReadInt64();
            TotalMp = reader.ReadInt32();
        }

        public bool Slaying => TotalHp > HpRemaining*2 && HpRemaining > 0;
        public int BaseAttack { get; private set; }
        public int BaseAttack2 { get; private set; }
        public short BaseAttackSpeed { get; private set; }
        public int BaseBalance { get; private set; }
        public int BaseBalanceFactor { get; private set; }
        public float BaseCritPower { get; private set; }
        public float BaseCritRate { get; private set; }
        public float BaseCritResist { get; private set; }
        public int BaseDefence { get; private set; }
        public int BaseEndurance { get; private set; }
        public int BaseImpactFactor { get; private set; }
        public int BaseImpcat { get; private set; }
        public short BaseMovementSpeed { get; private set; }
        public int BasePower { get; private set; }
        public float BaseResistPeriodic { get; private set; }
        public float BaseResistStun { get; private set; }
        public float BaseResistWeakening { get; private set; }
        public int BonusAttack { get; private set; }
        public int BonusAttack2 { get; private set; }
        public short BonusAttackSpeed { get; private set; }
        public int BonusBalance { get; private set; }
        public int BonusBalanceFactor { get; private set; }
        public float BonusCritPower { get; private set; }
        public float BonusCritRate { get; private set; }
        public float BonusCritResist { get; private set; }
        public int BonusDefence { get; private set; }
        public int BonusEndurance { get; private set; }
        public int BonusHp { get; private set; }
        public int BonusImpactFactor { get; private set; }
        public int BonusImpcat { get; private set; }
        public short BonusMovementSpeed { get; private set; }
        public int BonusMp { get; private set; }
        public int BonusPower { get; private set; }
        public float BonusResistPeriodic { get; private set; }
        public float BonusResistStun { get; private set; }
        public float BonusResistWeakening { get; private set; }
        public long HpRemaining { get; }
        public int ItemLevel { get; private set; }
        public int ItemLevelInventory { get; private set; }
        public int Level { get; private set; }
        public int MpRemaining { get; private set; }
        public int ReRemaining { get; private set; }
        public int Stamina { get; private set; }
        public byte Status { get; private set; }
        public long TotalHp { get; }
        public int TotalMp { get; private set; }
        public int TotalRe { get; private set; }
        public int BonusRe { get; private set; }
        public int TotalStamina { get; private set; }
        public int Vitality { get; private set; }
        public int Edge { get; private set; }
        public float FlightEnergy { get; private set; }
        public int FireEdge { get; private set; }
        public int IceEdge { get; private set; }
        public int LightningEdge { get; private set; }
    }
}