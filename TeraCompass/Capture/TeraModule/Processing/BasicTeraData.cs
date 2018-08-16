using System;
using System.Globalization;
using System.IO;
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

        public string ResourceDirectory { get; }
        public ServerDatabase Servers { get; }
    }
}