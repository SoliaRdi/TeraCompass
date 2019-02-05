using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capture.TeraModule.Tera.Core.Game.Messages.Server;
using LiteDB;
using TeraCompass.Tera.Core;
using TeraCompass.Tera.Core.Game;

namespace Capture.TeraModule.Tera.Core.Game
{
    public enum ColorType
    {
        Blue,Green,Yellow,Grey
    }
    public class CollectionEntity:IEntity
    {
        [BsonId(true)]
        public ObjectId DataId { get; set; }
        public int CollectionId { get; set; }
        public int ZoneId { get; set; }
        [BsonIgnore]
        public int Amount { get; set; }
        [BsonIgnore]
        public Angle Angle { get; set; }
        [BsonIgnore]
        public bool Extractor { get; set; }
        [BsonIgnore]
        public bool ExtractorDisabled { get; set; }
        [BsonIgnore]
        public ColorType ColorType
        {
            get
            {
                switch (CollectionId)
                {
                    case int n when (n<=6):
                        return ColorType.Green;
                    case int n when (n >= 201&&n<=205):
                        return ColorType.Blue;
                    case int n when (n >= 101 && n <= 106):
                        return ColorType.Yellow;
                    default:
                        return ColorType.Grey;
                }
            }
        }

        [BsonIgnore]
        public EntityId Id { get; set; }
        [BsonIgnore]
        public RelationType Relation { get; set; }
        [BsonIgnore]
        public bool Dead { get; set; }

        public Vector3f Position { get; set; }
        
        [BsonIgnore]
        public int Status { get; set; }
    }
}
