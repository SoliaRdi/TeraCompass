using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using Capture.Hook;
using TeraCompass.Tera.Core.Game;
using TeraCompass.Tera.Core.Game.Services;

namespace TeraCompass.Processing
{
    public class BasicTeraData
    {
        public List<ImageElement> Icons;
        public BasicTeraData(string folder)
        {
            Icons = new List<ImageElement>();
            ResourceDirectory = Path.Combine(folder,@"resources");
            Servers = new ServerDatabase(Path.Combine(ResourceDirectory, "data"));
            //icons part
            DirectoryInfo d = new DirectoryInfo(Path.Combine(ResourceDirectory, "icons"));
            FileInfo[] Files = d.GetFiles("*.png");
            foreach (var image in Files)
            {
                Icons.Add(new ImageElement(image.FullName));
            }
        }
        public static BasicTeraData Instance { get; set; }

        public string ResourceDirectory { get; }
        public ServerDatabase Servers { get; }
    }
}