using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capture.GUI;
using Jot;
using Jot.DefaultInitializer;
using TeraCompass.Tera.Core;
using TeraCompass.Tera.Core.Game;

namespace Capture.TeraModule.Settings
{
    public static class Services
    {
        public static StateTracker Tracker = new StateTracker();

        public static CompassSettings CompassSettings = new CompassSettings();
        //public static AppSettings Settings = new AppSettings();
    }

    //[Serializable]
    //public class AppSettings
    //{
    //    public AppSettings()
    //    {
    //        CompassSettings = new CompassSettings();
    //    }

    //    [Trackable]
    //    public CompassSettings CompassSettings { get; set; }
    //}


    [Serializable]
    public class CompassSettings
    {
        public CompassSettings()
        {
            ResetSettings(); //for initialization
        }

        [Trackable]
        public int OverlayCorner { get; set; }

        public bool OverlayOpened = true;

        [Trackable]
        public float DISTANCE { get; set; }

        [Trackable]
        public float Zoom
        {
            get => _zoom;
            set => _zoom = value;
        }

        [Trackable]
        public int PlayerSize
        {
            get => _playerSize;
            set => _playerSize = value;
        }

        public bool SettingsOpened = false;
        public bool StatisticsOpened = false;
        public bool CaptureOnlyEnemy = false;
        public string SelectedGuildName = "Without Guild";

        [Trackable]
        public Dictionary<RelationType, uint> RelationColors { get; set; }

        public float[] R { get; set; }
        public float[] G { get; set; }
        public float[] B { get; set; }
        public float[] A { get; set; }

        public HashSet<RelationType> FriendlyTypes = new HashSet<RelationType>
        {
            RelationType.Casual, RelationType.GuildMember, RelationType.MyRaid, RelationType.RaidMyParty
        };

        public HashSet<PlayerClass> _filteredClasses;
        public bool _filterByClasses;
        public bool _showNicknames;
        public bool _showFps;
        public float _zoom;
        public int _playerSize;

        public void ResetSettings()
        {
            PlayerSize = 5;
            OverlayCorner = 0;
            DISTANCE = 10.0f;
            Zoom = 4f;
            RelationColors = new Dictionary<RelationType, uint>()
            {
                {RelationType.Unknown, Color.Red.ToARGB()},
                {RelationType.Casual, Color.White.ToARGB()},
                {RelationType.RaidMyParty,Color.FromArgb(255, 30, 109, 255).ToARGB()},
                {RelationType.PK, Color.OrangeRed.ToARGB()},
                {RelationType.GuildMember, Color.FromArgb(255, 21, 236, 25).ToARGB()},
                {RelationType.EnemyRaid, Color.Red.ToARGB()},
                {RelationType.GvG, Color.Red.ToARGB()},
                {RelationType.MyRaid, Color.FromArgb(33, 214, 33).ToARGB()},
                {RelationType.Dead, Color.FromArgb(75, 79, 79).ToARGB()},
            };
            R = new float[RelationColors.Keys.Count];
            G = new float[RelationColors.Keys.Count];
            B = new float[RelationColors.Keys.Count];
            A = new float[RelationColors.Keys.Count];
            FilterByClasses = false;
            ShowNicknames = false;
            ShowFPS = false;
            FilteredClasses = new HashSet<PlayerClass>();
        }

        [Trackable]
        public bool FilterByClasses
        {
            get => _filterByClasses;
            set => _filterByClasses = value;
        }

        [Trackable]
        public bool ShowNicknames
        {
            get => _showNicknames;
            set => _showNicknames = value;
        }

        [Trackable]
        public bool ShowFPS
        {
            get => _showFps;
            set => _showFps = value;
        }

        [Trackable]
        public HashSet<PlayerClass> FilteredClasses
        {
            get => _filteredClasses;
            set => _filteredClasses = value;
        }
    }
}