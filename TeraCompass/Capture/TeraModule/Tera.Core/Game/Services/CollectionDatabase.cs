using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using TeraCompass.Processing;
using TeraCompass.Tera.Core.Game;

namespace Capture.TeraModule.Tera.Core.Game.Services
{
    public class CollectionDatabase
    {
        public LiteDatabase LiteDatabase { get; set; }
        public LiteCollection<CollectionEntity> Collection { get; set; }

        public CollectionDatabase()
        {
            // открывает базу данных, если ее нет - то создает
            LiteDatabase = new LiteDatabase(Path.Combine(BasicTeraData.Instance.ResourceDirectory, "CollectionsData.db"));
            Collection = LiteDatabase.GetCollection<CollectionEntity>("collections");
            BsonMapper.Global.RegisterType(
                vector => new BsonArray(new BsonValue[] { vector.X, vector.Y, vector.Z }),
                value => new Vector3f((float)value.AsArray[0].AsDouble, (float)value.AsArray[1].AsDouble, (float)value.AsArray[2].AsDouble)
            );
            Collection.EnsureIndex(x => x.Position, "$.Position[*]");
            Collection.EnsureIndex(x => x.ZoneId);
        }
    }
}
