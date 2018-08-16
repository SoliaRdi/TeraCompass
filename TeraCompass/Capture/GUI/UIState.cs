using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeraCompass.Tera.Core;
using TeraCompass.Tera.Core.Game;

namespace Capture.GUI
{

    public static class UIState
    {
        public static int OverlayCorner = 0;
        public static bool OverlayOpened= true;
        public static float DISTANCE = 10.0f;
        public static float Zoom = 4f;
        public static int PlayerSize = 5;
        public static bool SettingsOpened= false;
        public static bool StatisticsOpened = false;
        public static bool CaptureOnlyEnemy = false;
        public static string SelectedGuildName = Properties.Resources.SelectedGuildName;
        public static HashSet<RelationType> FriendlyTypes = new HashSet<RelationType>
        {
            RelationType.Casual,RelationType.GuildMember,RelationType.MyRaid,RelationType.RaidMyParty
        };
        public static Dictionary<RelationType, uint> RelationColors = new Dictionary<RelationType, uint>()
        {
            {RelationType.Unknown,Color.Red.ToARGB()},
            {RelationType.Casual,Color.White.ToARGB()},
            {RelationType.RaidMyParty,(uint)Color.FromArgb(255,30, 109, 255).ToARGB()},
            {RelationType.PK, Color.OrangeRed.ToARGB()},
            {RelationType.GuildMember,Color.FromArgb(255,21, 236, 25).ToARGB()},
            {RelationType.EnemyRaid,Color.Red.ToARGB()},
            {RelationType.GvG,Color.Red.ToARGB()},
            {RelationType.MyRaid,Color.FromArgb(33, 214, 33).ToARGB()},
        };
        public static float[] R = new float[RelationColors.Keys.Count];
        public static float[] G = new float[RelationColors.Keys.Count];
        public static float[] B = new float[RelationColors.Keys.Count];
        public static float[] A = new float[RelationColors.Keys.Count];
        public static bool FilterByClassess = false;
        public static bool ShowNicknames = false;
        public static bool ShowFPS = false;
        public static HashSet<PlayerClass> FilteredClasses = new HashSet<PlayerClass>();
    }
}
