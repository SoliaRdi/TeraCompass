using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Capture.GUI;
using Capture.Hook;
using Capture.TeraModule.CameraFinder;
using Capture.TeraModule.GameModels;
using Capture.TeraModule.Processing;
using Capture.TeraModule.Settings;
using ImGuiNET;
using SharpDX.Direct3D9;
using TeraCompass.Tera.Core;
using TeraCompass.Tera.Core.Game;

namespace Capture.TeraModule.ViewModels
{
    public class CompassViewModel
    {
        public CameraScanner CameraScanner;
        public Dictionary<EntityId, PlayerModel> PlayerModels;

        public CompassViewModel()
        {
            PlayerModels = new Dictionary<EntityId, PlayerModel>();
            CurrentProcess = Process.GetProcessesByName("tera").Single();
            CameraScanner = new CameraScanner(CurrentProcess);
            Task.Factory.StartNew(() => { CameraScanner.FindCameraAddress(); });
            PacketProcessor.Instance.EntityTracker.EntityUpdated += EntityTracker_EntityUpdated;
            PacketProcessor.Instance.EntityTracker.EntityDeleted += EntityTracker_EntityDeleted;
            PacketProcessor.Instance.EntityTracker.EntitysCleared += EntityTracker_EntitysCleared;
        }

        private Process CurrentProcess { get; }

        private void EntityTracker_EntitysCleared(IEntity obj)
        {
            PlayerModels.Clear();
        }

        private void EntityTracker_EntityDeleted(IEntity obj)
        {
            PlayerModels.Remove(obj.Id);
        }

        private void EntityTracker_EntityUpdated(IEntity obj)
        {
            if (PacketProcessor.Instance.EntityTracker.CompassUser.Id != obj.Id)
            {
                var entity = (UserEntity) obj;
                var founded = PlayerModels.TryGetValue(obj.Id, out var model);
                if (!founded)
                {
                    model = new PlayerModel(entity);
                    PlayerModels[obj.Id] = model;
                }
                else
                {
                    model.Dead = obj.Dead;
                    model.Position = obj.Position;
                    model.Relation = obj.Relation;
                }
            }
        }

        public Vector2 Vector3ToVector2(Vector3f vector)
        {
            return new Vector2(vector.X, vector.Y);
        }

        public static Vector2 RotatePoint(Vector2 pointToRotate, Vector2 centerPoint, float angle,
            bool angleInRadians = false)
        {
            if (!angleInRadians)
                angle = (float) (angle * (Math.PI / 180f));
            var cosTheta = (float) Math.Cos(angle);
            var sinTheta = (float) Math.Sin(angle);
            var returnVec = new Vector2(
                cosTheta * (pointToRotate.X - centerPoint.X) - sinTheta * (pointToRotate.Y - centerPoint.Y),
                sinTheta * (pointToRotate.X - centerPoint.X) + cosTheta * (pointToRotate.Y - centerPoint.Y)
            );
            returnVec += centerPoint;
            return returnVec;
        }

        public Vector2 GetScreenPos(PlayerModel entity)
        {
            var myPos = Vector3ToVector2(PacketProcessor.Instance.EntityTracker.CompassUser.Position);
            var radarCenter = new Vector2(160f, 160f);
            var screenPos = Vector3ToVector2(entity.Position);
            screenPos = myPos - screenPos;
            var distance = screenPos.Length() * (0.02f * Services.CompassSettings.Zoom);
            distance = Math.Min(distance, 150f - Services.CompassSettings.PlayerSize);
            screenPos = Vector2.Normalize(screenPos);
            screenPos *= distance;
            screenPos += radarCenter;
            screenPos = RotatePoint(screenPos, radarCenter, 90f);
            return new Vector2(screenPos.X, screenPos.Y);
        }
        
        internal void Render(Sprite sprite)
        {
            if (Services.GameState != GameState.InGame) return;
            Vector2 CompassPosition=new Vector2();
            Vector2 CompassSize = new Vector2(320, 320);
            
            
            if (Services.CompassSettings.OverlayCorner != -1)
            {
                CompassPosition = new Vector2(Services.CompassSettings.DISTANCE,Services.CompassSettings.DISTANCE);
                ImGui.SetNextWindowPos(CompassPosition, Condition.Always);
            }

            ImGui.SetNextWindowSize(CompassSize, Condition.Always);
            ImGuiNative.igSetNextWindowContentSize(CompassSize);
            ImGui.PushStyleVar(StyleVar.WindowPadding, Vector2.Zero);
            if (ImGui.BeginWindow("Overlay", ref Services.CompassSettings.OverlayOpened, Vector2.Zero, 0,
                (Services.CompassSettings.OverlayCorner != -1 ? WindowFlags.NoMove : 0) | WindowFlags.NoTitleBar | WindowFlags.NoResize | WindowFlags.NoBringToFrontOnFocus | WindowFlags.NoFocusOnAppearing|WindowFlags.NoScrollbar))
            {
                //var draw_list = ImGui.GetOverlayDrawList();
                var draw_list = DrawList.GetForCurrentWindow();
                CompassPosition = ImGui.GetWindowPosition();
                ImGui.PopStyleVar();
                if (ImGuiNative.igBeginPopupContextWindow("Options", 1, true))
                {
                    if (ImGui.MenuItem("Custom position", null, Services.CompassSettings.OverlayCorner == -1, true))
                        Services.CompassSettings.OverlayCorner = -1;
                    if (ImGui.MenuItem("Top right", null, Services.CompassSettings.OverlayCorner == 0, true)) Services.CompassSettings.OverlayCorner = 0;
                    if (ImGui.MenuItem("Settings", null, Services.CompassSettings.SettingsOpened, true))
                        Services.CompassSettings.SettingsOpened = !Services.CompassSettings.SettingsOpened;

                    ImGuiNative.igEndPopup();
                }

                Vector2 WindowCenter = new Vector2(CompassPosition.X + CompassSize.X * 0.5f, CompassPosition.Y + CompassSize.Y * 0.5f);
                
                draw_list.AddCircleFilled(
                    WindowCenter,
                    CompassSize.X/2, Color.FromArgb(76, 15, 15, 15).ToDx9ARGB(), 128
                    );

                draw_list.AddLine(new Vector2(WindowCenter.X, CompassPosition.Y),
                    new Vector2(WindowCenter.X, CompassPosition.Y + CompassSize.Y),
                    Color.FromArgb(90, 70, 70, 255).ToDx9ARGB(), 1f);

                draw_list.AddLine(new Vector2(CompassPosition.X, WindowCenter.Y),
                    new Vector2(CompassPosition.X + CompassSize.X, WindowCenter.Y),
                    Color.FromArgb(89, 70, 70, 255).ToDx9ARGB(), 1f);


                var dot2 = new Vector2(CompassPosition.X+CompassSize.X - 30, WindowCenter.Y);

                var final = CameraScanner.CameraAddress != 0
                    ? RotatePoint(dot2, WindowCenter, new Angle(CameraScanner.Angle()).Gradus - 90)
                    : RotatePoint(dot2, WindowCenter,
                        PacketProcessor.Instance.EntityTracker.CompassUser.Heading.Gradus - 90);

                draw_list.AddLine(WindowCenter, final, Color.FromArgb(120, 255, 255, 255).ToDx9ARGB(), 1f);
                
                var values = PlayerModels.Values.ToArray();
                for (var i = 0; i < values.Length; i++)
                {
                    if (Services.CompassSettings.CaptureOnlyEnemy && Services.CompassSettings.FriendlyTypes.Contains(values[i].Relation)) continue;
                    if (Services.CompassSettings.FilterByClasses && Services.CompassSettings.FilteredClasses.Contains(values[i].PlayerClass))
                        continue;
                    uint color;
                    if (values[i].Dead)
                    {
                        Services.CompassSettings.RelationColors.TryGetValue(RelationType.Dead, out color);
                    }
                    else
                    {
                        if (!Services.CompassSettings.RelationColors.TryGetValue(values[i].Relation, out color))
                            Services.CompassSettings.RelationColors.TryGetValue(RelationType.Unknown, out color);
                    }
                    

                    var ScreenPosition = GetScreenPos(values[i]);

                    color = color.ToDx9ARGB();

                    draw_list.AddCircleFilled(
                        new Vector2(CompassPosition.X+ScreenPosition.X, CompassPosition.Y+ScreenPosition.Y),
                        Services.CompassSettings.PlayerSize, color, Services.CompassSettings.PlayerSize * 2);

                    if (Services.CompassSettings.ShowNicknames)
                        draw_list.AddText(
                            new Vector2(CompassPosition.X+ScreenPosition.X - values[i].Name.Length * 4 / 2f,
                                CompassPosition.Y+ScreenPosition.Y + Services.CompassSettings.PlayerSize), $"{values[i].Name}",
                            color);
                    
                }
            }

            ImGui.EndWindow();
            
            if (PacketProcessor.Instance.EntityTracker.CompassUser.Status==1&&DXHookD3D9._imageCache.TryGetValue("incombat.png", out var texture))
                sprite.Draw(texture,new SharpDX.Mathematics.Interop.RawColorBGRA(255,255,255,255),null,null,
                    new SharpDX.Mathematics.Interop.RawVector3(CompassPosition.X+CompassSize.X-32, CompassPosition.Y+CompassSize.Y-64,0));
            if (PlayerModels.Count > 0)
                if (Services.CompassSettings.StatisticsOpened)
                {
                    var GuldList = PlayerModels.Values
                        .ToArray()
                        .GroupBy(x => x.GuildName.Length == 0 ? "Without Guild" : x.GuildName,
                            (key, g) => new {GuildName = key, Players = g.ToList()})
                        .OrderByDescending(x => x.Players.Count).ToHashSet();

                    if (GuldList.Count > 0)
                    {
                        ImGui.SetNextWindowPos(new Vector2(CompassPosition.X, CompassPosition.Y + CompassSize.Y), Condition.Always);
                        if (ImGui.BeginWindow("Guilds", ref Services.CompassSettings.OverlayOpened, new Vector2(350, 200), 0.3f,
                            WindowFlags.NoTitleBar | WindowFlags.NoFocusOnAppearing))
                        {
                            ImGui.BeginChild("left pane", new Vector2(150, 0), true);

                            foreach (var i in GuldList)
                                if (ImGui.Selectable($"{i.GuildName} ({i.Players.Count})",
                                    Services.CompassSettings.SelectedGuildName == i.GuildName))
                                    Services.CompassSettings.SelectedGuildName = i.GuildName;

                            ImGui.EndChild();
                            ImGui.SameLine();
                            ImGuiNative.igBeginGroup();
                            ImGui.BeginChild("item view", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), true); // Leave room for 1 line below us

                            ImGui.TextUnformatted($"Guild name {Services.CompassSettings.SelectedGuildName}\n");
                            ImGui.Columns(3, null, true);

                            var players = GuldList.SingleOrDefault(x => x.GuildName == Services.CompassSettings.SelectedGuildName)
                                ?.Players?.GroupBy(x => x.PlayerClass,
                                    (key, g) => new {Class = key, Players = g.ToList()});

                            if (players != null)
                                foreach (var details in players)
                                {
                                    if (ImGui.GetColumnIndex() == 0)
                                        ImGui.Separator();
                                    ImGui.TextUnformatted($"{details.Class.ToString()} ({details.Players.Count})\n");

                                    if (details.Players?.Count > 0)
                                        foreach (var name in details.Players)
                                            ImGui.TextUnformatted($"{name.Name}\n");
                                    ImGui.NextColumn();
                                }

                            ImGui.Columns(1, null, true);

                            ImGui.Separator();
                            ImGui.EndChild();
                            ImGuiNative.igEndGroup();
                        }

                        ImGui.EndWindow();
                    }
                }

            if (Services.CompassSettings.SettingsOpened)
            {
                if (ImGui.BeginWindow("Settings", ref Services.CompassSettings.SettingsOpened, new Vector2(350, 400), 0.3f,
                    WindowFlags.NoFocusOnAppearing | WindowFlags.AlwaysAutoResize))
                {
                    ImGui.Checkbox("Guild statistic", ref Services.CompassSettings.StatisticsOpened);
                    ImGui.Checkbox("Show only enemy players", ref Services.CompassSettings.CaptureOnlyEnemy);
                    ImGui.Checkbox("Filter by classes", ref Services.CompassSettings._filterByClasses);
                    ImGui.Checkbox("Show nicknames", ref Services.CompassSettings._showNicknames);
                    ImGui.Checkbox("Show fps", ref Services.CompassSettings._showFps);
                    ImGui.Checkbox("Show render time", ref Services.CompassSettings._showRenderTime);
                    ImGui.SliderFloat("Zoom", ref Services.CompassSettings._zoom, 1, 20, $"Zoom={Services.CompassSettings.Zoom}", 2f);
                    if (ImGui.IsLastItemActive() || ImGui.IsItemHovered(HoveredFlags.Default))
                        ImGui.SetTooltip($"{Services.CompassSettings.Zoom:F2}");
                    ImGui.SliderInt("PlayerSize", ref Services.CompassSettings._playerSize, 1, 10, $"PlayerSize = {Services.CompassSettings.PlayerSize}");
                    if (ImGui.IsLastItemActive() || ImGui.IsItemHovered(HoveredFlags.Default))
                        ImGui.SetTooltip($"{Services.CompassSettings.PlayerSize}");
                    if (ImGui.CollapsingHeader("Settings for filter by class            ", TreeNodeFlags.CollapsingHeader | TreeNodeFlags.AllowItemOverlap))
                    {
                        ImGui.TextUnformatted("Common ignored");
                        ImGui.Columns(3, null, false);
                        foreach (PlayerClass i in Enum.GetValues(typeof(PlayerClass)))
                        {
                            var flag = Services.CompassSettings.FilteredClasses.Contains(i);
                            ImGui.Checkbox(i.ToString(), ref flag);
                            if (flag)
                                Services.CompassSettings.FilteredClasses.Add(i);
                            else if (Services.CompassSettings.FilteredClasses.Contains(i))
                                Services.CompassSettings.FilteredClasses.Remove(i);
                            ImGui.NextColumn();
                        }

                        ImGui.Columns(1, null, false);
                    }

                    if (ImGui.CollapsingHeader("Colors for player relation", TreeNodeFlags.CollapsingHeader))
                    {
                        var keys = Services.CompassSettings.RelationColors.Keys.ToArray();
                        for (var i = 0; i < keys.Length; i++)
                        {
                            Services.CompassSettings.RelationColors.TryGetValue(keys[i], out var color);
                            Services.CompassSettings.R[i] = ((color >> 16) & 255) / 255f;
                            Services.CompassSettings.G[i] = ((color >> 8) & 255) / 255f;
                            Services.CompassSettings.B[i] = ((color >> 0) & 255) / 255f;
                            Services.CompassSettings.A[i] = ((color >> 24) & 255) / 255f;
                            ImGui.TextUnformatted(keys[i].ToString());
                            ImGui.SameLine();
                            ImGui.ColorEdit4(keys[i].ToString(), ref Services.CompassSettings.R[i], ref Services.CompassSettings.G[i], ref Services.CompassSettings.B[i],
                                ref Services.CompassSettings.A[i],
                                ColorEditFlags.NoInputs | ColorEditFlags.RGB | ColorEditFlags.NoLabel);

                            uint mr = Services.CompassSettings.R[i] >= 1.0 ? 255 :
                                    Services.CompassSettings.R[i] <= 0.0 ? 0 : (uint) Math.Round(Services.CompassSettings.R[i] * 255f),
                                mg = Services.CompassSettings.G[i] >= 1.0 ? 255 :
                                    Services.CompassSettings.G[i] <= 0.0 ? 0 : (uint) Math.Round(Services.CompassSettings.G[i] * 255f),
                                mb = Services.CompassSettings.B[i] >= 1.0 ? 255 :
                                    Services.CompassSettings.B[i] <= 0.0 ? 0 : (uint) Math.Round(Services.CompassSettings.B[i] * 255f),
                                ma = Services.CompassSettings.A[i] >= 1.0 ? 255 :
                                    Services.CompassSettings.A[i] <= 0.0 ? 0 : (uint) Math.Round(Services.CompassSettings.A[i] * 255f);

                            Services.CompassSettings.RelationColors[keys[i]] = (ma << 24) | (mr << 16) | (mg << 8) | (mb << 0);
                        }
                    }

                    if (ImGui.Button("Save settings"))
                    {
                        Services.Tracker.RunAutoPersist();
                    }

                    ImGui.SameLine();
                    if (ImGui.Button("Reset to default"))
                    {
                        Services.CompassSettings.ResetSettings();
                        Services.Tracker.RunAutoPersist();
                    }

                }

                ImGui.EndWindow();
            }
        }
    }
}