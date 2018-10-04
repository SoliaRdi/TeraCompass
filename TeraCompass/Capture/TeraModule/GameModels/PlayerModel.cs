using System.Numerics;
using TeraCompass.Tera.Core;
using TeraCompass.Tera.Core.Game;

namespace Capture.TeraModule.GameModels
{
    public class PlayerModel
    {
        public EntityId EntityId { get; set; }

        public Vector3f Position { get; set; }
        public RelationType Relation { get; set; }
        public string Name { get; set; }
        public string GuildName { get; set; }
        public Vector2 ScreenPosition { get; set; }
        public PlayerClass PlayerClass { get; set; }
        public bool Dead { get; set; }
        public PlayerModel(UserEntity obj)
        {
            Relation = obj.Relation;
            EntityId = obj.Id;
            Position = obj.Position;
            Name = obj.Name;
            GuildName = obj.GuildName;
            PlayerClass = obj.RaceGenderClass.Class;
            Dead = obj.Dead;
        }
    }
}
