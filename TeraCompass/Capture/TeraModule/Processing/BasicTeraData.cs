using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Processing
{
    public class BasicTeraData
    {
        private static BasicTeraData _instance;
        private BasicTeraData()
        {
            ResourceDirectory = @"resources";

            Servers = new ServerDatabase(Path.Combine(ResourceDirectory, "data"));
        }


        public static BasicTeraData Instance => _instance ?? (_instance = new BasicTeraData());
        public SkillDatabase SkillDatabase { get; set; }
        public NpcDatabase MonsterDatabase { get; set; }

        public string ResourceDirectory { get; }
        public ServerDatabase Servers { get; }
        public IconsDatabase Icons { get; set; }
        public MapData MapData { get; set; }

        private static IEnumerable<Server> GetServers(string filename)
        {
            return File.ReadAllLines(filename).Where(s => !s.StartsWith("#") && !string.IsNullOrWhiteSpace(s)).Select(s => s.Split(new[] {' '}, 3))
                .Select(parts => new Server(parts[2], parts[1], parts[0]));
        }
    }
}