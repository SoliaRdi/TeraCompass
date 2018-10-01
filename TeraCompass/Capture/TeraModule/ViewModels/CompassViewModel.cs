using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Capture.GUI;
using Capture.TeraModule.CameraFinder;
using Capture.TeraModule.GameModels;
using Capture.TeraModule.Processing;
using ImGuiNET;
using TeraCompass.Tera.Core;
using TeraCompass.Tera.Core.Game;

namespace Capture.TeraModule.ViewModels
{
    public class CompassViewModel
    {
        public CameraScanner CameraScanner;
        public Dictionary<EntityId, PlayerModel> PlayerModels;
        public List<string> DeathList;
        public CompassViewModel()
        {
            DeathList = new List<string>();
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
                var entity = (UserEntity)obj;
                var founded = PlayerModels.TryGetValue(obj.Id, out var model);
                if (!founded)
                {
                    model = new PlayerModel(entity);
                    PlayerModels[obj.Id] = model;
                }
                else
                {
                    //if (obj.Dead && !model.Dead)
                    //    DeathList.Add($"{entity.Name}({entity.GuildName})");
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
            var radarCenter = new Vector2(150f, 150f);
            var screenPos = Vector3ToVector2(entity.Position);
            screenPos = myPos - screenPos;
            var distance = screenPos.Length() * (0.02f * UIState.Zoom);
            distance = Math.Min(distance, 150f - UIState.PlayerSize);
            screenPos = Vector2.Normalize(screenPos);
            screenPos *= distance;
            screenPos += radarCenter;
            screenPos = RotatePoint(screenPos, radarCenter, 90f);
            return new Vector2(screenPos.X, screenPos.Y);
        }

        internal void Render()
        {
            var window_pos =
                new Vector2(
                    UIState.OverlayCorner == 1 ? ImGui.GetIO().DisplaySize.X - UIState.DISTANCE : UIState.DISTANCE,
                    UIState.OverlayCorner == 2
                        ? ImGui.GetIO().DisplaySize.Y - UIState.DISTANCE * 4
                        : UIState.DISTANCE * 4);

            var window_pos_pivot = new Vector2(UIState.OverlayCorner == 1 ? 1.0f : 0.0f, UIState.OverlayCorner == 2 ? 1.0f : 0.0f);
            var window_size = new Vector2(300, 300);
            var draw_list = ImGui.GetOverlayDrawList();
            if (UIState.OverlayCorner != -1)
                ImGui.SetNextWindowPos(window_pos, Condition.Always, window_pos_pivot);

            if (ImGui.BeginWindow("Overlay", ref UIState.OverlayOpened, window_size, 0.3f,(UIState.OverlayCorner != -1 ? WindowFlags.NoMove : 0) | WindowFlags.NoTitleBar | WindowFlags.NoResize | WindowFlags.NoFocusOnAppearing))
            {
                window_pos = ImGui.GetWindowPosition();
                if (ImGuiNative.igBeginPopupContextWindow("Options", 1, true))
                {
                    if (ImGui.MenuItem("Custom position", null, UIState.OverlayCorner == -1, true))
                        UIState.OverlayCorner = -1;
                    if (ImGui.MenuItem("Top right", null, UIState.OverlayCorner == 0, true)) UIState.OverlayCorner = 0;
                    if (ImGui.MenuItem("Settings", null, UIState.SettingsOpened, true))
                        UIState.SettingsOpened = !UIState.SettingsOpened;

                    ImGuiNative.igEndPopup();
                }

                draw_list.AddLine(new Vector2(window_pos.X + window_size.X * 0.5f, window_pos.Y),
                    new Vector2(window_pos.X + window_size.X * 0.5f, window_pos.Y + window_size.Y),
                    Color.FromArgb(90, 70, 70, 255).ToDx9ARGB(), 1f);

                draw_list.AddLine(new Vector2(window_pos.X, window_pos.Y + window_size.Y * 0.5f),
                    new Vector2(window_pos.X + window_size.X, window_pos.Y + window_size.Y * 0.5f),
                    Color.FromArgb(90, 70, 70, 255).ToDx9ARGB(), 1f);

                var dot1 = new Vector2(window_pos.X + window_size.X * 0.5f, window_pos.Y + window_size.Y * 0.5f);

                var dot2 = new Vector2(window_pos.X + window_size.X - 30, window_pos.Y + window_size.Y * 0.5f);

                var final = CameraScanner.CameraAddress != 0
                    ? RotatePoint(dot2, dot1, new Angle(CameraScanner.Angle()).Gradus - 90)
                    : RotatePoint(dot2, dot1,
                        PacketProcessor.Instance.EntityTracker.CompassUser.Heading.Gradus - 90);

                draw_list.AddLine(dot1, final, Color.FromArgb(120, 255, 255, 255).ToDx9ARGB(), 1f);

                var values = PlayerModels.Values.ToArray();
                for (var i = 0; i < values.Length; i++)
                {
                    if (UIState.CaptureOnlyEnemy && UIState.FriendlyTypes.Contains(values[i].Relation)) continue;
                    if (UIState.FilterByClassess && UIState.FilteredClasses.Contains(values[i].PlayerClass))
                        continue;

                    if (!UIState.RelationColors.TryGetValue(values[i].Relation, out var color))
                        UIState.RelationColors.TryGetValue(RelationType.Unknown, out color);

                    var ScreenPosition = GetScreenPos(values[i]);

                    draw_list.AddCircleFilled(
                        new Vector2(window_pos.X + ScreenPosition.X, window_pos.Y + ScreenPosition.Y),
                        UIState.PlayerSize, color.ToDx9ARGB(), UIState.PlayerSize * 2);

                    if (UIState.ShowNicknames)
                        draw_list.AddText(
                            new Vector2(window_pos.X + ScreenPosition.X - values[i].Name.Length * 4 / 2f,
                                window_pos.Y + ScreenPosition.Y + UIState.PlayerSize), $"{values[i].Name}",
                            color.ToDx9ARGB());
                }
            }

            ImGui.EndWindow();
            if (this != null && PlayerModels.Count > 0)
                if (UIState.StatisticsOpened)
                {
                    var GuldList = PlayerModels.Values
                        .ToArray()
                        .GroupBy(x => x.GuildName.Length == 0 ? "Without Guild" : x.GuildName,
                            (key, g) => new {GuildName = key, Players = g.ToList()})
                        .OrderByDescending(x => x.Players.Count).ToHashSet();

                    if (GuldList.Count > 0)
                    {
                        ImGui.SetNextWindowPos(new Vector2(window_pos.X, window_pos.Y + window_size.Y),Condition.Always, window_pos_pivot);
                        if (ImGui.BeginWindow("Guilds", ref UIState.OverlayOpened, new Vector2(350, 200), 0.3f,
                            WindowFlags.NoTitleBar | WindowFlags.NoFocusOnAppearing))
                        {
                            ImGui.BeginChild("left pane", new Vector2(150, 0), true);

                            foreach (var i in GuldList)
                                if (ImGui.Selectable($"{i.GuildName} ({i.Players.Count})",
                                    UIState.SelectedGuildName == i.GuildName))
                                    UIState.SelectedGuildName = i.GuildName;

                            ImGui.EndChild();
                            ImGui.SameLine();
                            ImGuiNative.igBeginGroup();
                            ImGui.BeginChild("item view", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()),true); // Leave room for 1 line below us

                            ImGui.TextUnformatted($"Guild name {UIState.SelectedGuildName}\n");
                            ImGui.Columns(3, null, true);

                            var players = GuldList.SingleOrDefault(x => x.GuildName == UIState.SelectedGuildName)
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

            if (UIState.SettingsOpened)
            {
                if (ImGui.BeginWindow("Settings", ref UIState.SettingsOpened, new Vector2(350, 400), 0.3f,
                    WindowFlags.NoFocusOnAppearing | WindowFlags.AlwaysAutoResize))
                {
                    ImGui.Checkbox("Guild statistic", ref UIState.StatisticsOpened);
                    ImGui.Checkbox("Show only enemy players", ref UIState.CaptureOnlyEnemy);
                    ImGui.Checkbox("Filter by classes", ref UIState.FilterByClassess);
                    ImGui.Checkbox("Show nicknames", ref UIState.ShowNicknames);
                    ImGui.Checkbox("Perfomance test", ref UIState.ShowFPS);
                    ImGui.SliderFloat("Zoom", ref UIState.Zoom, 1, 20, $"Zoom={UIState.Zoom}", 2f);
                    if (ImGui.IsLastItemActive() || ImGui.IsItemHovered(HoveredFlags.Default))
                        ImGui.SetTooltip($"{UIState.Zoom:F2}");
                    ImGui.SliderInt("PlayerSize", ref UIState.PlayerSize, 1, 10, $"PlayerSize = {UIState.PlayerSize}");
                    if (ImGui.IsLastItemActive() || ImGui.IsItemHovered(HoveredFlags.Default))
                        ImGui.SetTooltip($"{UIState.PlayerSize}");
                    if (ImGui.CollapsingHeader("Settings for filter by class            ",TreeNodeFlags.CollapsingHeader | TreeNodeFlags.AllowItemOverlap))
                    {
                        ImGui.TextUnformatted("Common ignored");
                        ImGui.Columns(3, null, false);
                        foreach (PlayerClass i in Enum.GetValues(typeof(PlayerClass)))
                        {
                            var flag = UIState.FilteredClasses.Contains(i);
                            ImGui.Checkbox(i.ToString(), ref flag);
                            if (flag)
                                UIState.FilteredClasses.Add(i);
                            else if (UIState.FilteredClasses.Contains(i))
                                UIState.FilteredClasses.Remove(i);
                            ImGui.NextColumn();
                        }

                        ImGui.Columns(1, null, false);
                    }

                    if (ImGui.CollapsingHeader("Colors for player relation", TreeNodeFlags.CollapsingHeader))
                    {
                        var keys = UIState.RelationColors.Keys.ToArray();
                        for (var i = 0; i < keys.Length; i++)
                        {
                            UIState.RelationColors.TryGetValue(keys[i], out var color);
                            UIState.R[i] = ((color >> 16) & 255) / 255f;
                            UIState.G[i] = ((color >> 8) & 255) / 255f;
                            UIState.B[i] = ((color >> 0) & 255) / 255f;
                            UIState.A[i] = ((color >> 24) & 255) / 255f;
                            ImGui.TextUnformatted(keys[i].ToString());
                            ImGui.SameLine();
                            ImGui.ColorEdit4(keys[i].ToString(), ref UIState.R[i], ref UIState.G[i], ref UIState.B[i],
                                ref UIState.A[i],
                                ColorEditFlags.NoInputs | ColorEditFlags.RGB | ColorEditFlags.NoLabel);

                            uint mr = UIState.R[i] >= 1.0 ? 255 :
                                    UIState.R[i] <= 0.0 ? 0 : (uint) Math.Round(UIState.R[i] * 255f),

                                mg = UIState.G[i] >= 1.0 ? 255 :
                                    UIState.G[i] <= 0.0 ? 0 : (uint) Math.Round(UIState.G[i] * 255f),

                                mb = UIState.B[i] >= 1.0 ? 255 :
                                    UIState.B[i] <= 0.0 ? 0 : (uint) Math.Round(UIState.B[i] * 255f),

                                ma = UIState.A[i] >= 1.0 ? 255 :
                                    UIState.A[i] <= 0.0 ? 0 : (uint) Math.Round(UIState.A[i] * 255f);

                            UIState.RelationColors[keys[i]] = (ma << 24) | (mr << 16) | (mg << 8) | (mb << 0);
                        }
                    }
                }

                ImGui.EndWindow();
            }
        }
    }
}