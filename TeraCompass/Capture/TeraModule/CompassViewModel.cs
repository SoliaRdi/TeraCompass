using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Capture.GUI;
using Capture.TeraModule.CameraFinder;
using SharpDX.Direct3D9;
using TeraCompass.GameModels;
using TeraCompass.Processing;
using TeraCompass.Tera.Core.Game;
using Point = System.Drawing.Point;
using Vector = System.Windows.Vector;

namespace TeraCompass.ViewModels
{
    public class CompassViewModel
    {
        public CameraScanner CameraScanner;
        private Process CurrentProcess { get; set; }
        public Dictionary<EntityId, PlayerModel> PlayerModels;
        public CompassViewModel()
        {
            PlayerModels = new Dictionary<EntityId, PlayerModel>();
            CurrentProcess = Process.GetProcessesByName("tera").Single();
            CameraScanner = new CameraScanner(CurrentProcess);
            Task.Factory.StartNew(() =>
            {
                CameraScanner.FindCameraAddress();
            });
            PacketProcessor.Instance.EntityTracker.EntityUpdated += EntityTracker_EntityUpdated;
            PacketProcessor.Instance.EntityTracker.EntityDeleted += EntityTracker_EntityDeleted;
            PacketProcessor.Instance.EntityTracker.EntitysCleared += EntityTracker_EntitysCleared;
        }

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
                var founded=PlayerModels.TryGetValue(obj.Id, out var model);
                if (!founded)
                {
                    model = new PlayerModel((UserEntity)obj);
                    PlayerModels[obj.Id]=model;
                }
                else
                {
                    model.Position = obj.Position;
                    model.Relation = obj.Relation;
                }
                    
            }
        }

        public Vector2 Vector3ToVector2(Vector3f vector)
        {
            return new Vector2(vector.X, vector.Y);
        }

        public static Vector2 RotatePoint(Vector2 pointToRotate, Vector2 centerPoint, float angle, bool angleInRadians = false)
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
            Vector2 screenPos = Vector3ToVector2(entity.Position);
            screenPos = myPos - screenPos;
            float distance = screenPos.Length() * (0.02f * UIState.Zoom);
            distance = Math.Min(distance, 150f -UIState.PlayerSize);
            screenPos = Vector2.Normalize(screenPos);
            screenPos *= distance;
            screenPos += radarCenter;
            screenPos = RotatePoint(screenPos, radarCenter, 90f);
            return new Vector2(screenPos.X, screenPos.Y);
        }
    }
}
