﻿// Copyright (c) Gothos
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Capture.TeraModule.Processing.Packets;
using Capture.TeraModule.Tera.Core.Game;
using Capture.TeraModule.Tera.Core.Game.Messages.Server;
using TeraCompass.Processing.Packets;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Messages.Client;
using TeraCompass.Tera.Core.Game.Messages.Server;

namespace TeraCompass.Tera.Core.Game.Services
{
    // Tracks which entities we have seen so far and what their properties are
    public sealed class EntityTracker : IEnumerable<IEntity>
    {
        private readonly Dictionary<EntityId, IEntity> _entities = new Dictionary<EntityId, IEntity>();

        public UserEntity CompassUser { get; private set; }

        public IEnumerator<IEntity> GetEnumerator()
        {
            return _entities.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public event Action<IEntity> EntityUpdated;
        public event Action<IEntity> EntityDeleted;
        public event Action<IEntity> EntitysCleared;

        private void OnEntityUpdated(IEntity entity)
        {
            EntityUpdated?.Invoke(entity);
        }

        private void OnEntityDeleted(IEntity entity)
        {
            EntityDeleted?.Invoke(entity);
        }

        private void OnEntitysCleared(Entity entity)
        {
            EntitysCleared?.Invoke(entity);
        }

        public void Update(LoginServerMessage message)
        {
            var newEntity = LoginMe(message);
            Register(newEntity);
        }

        public void Update(SpawnUserServerMessage message)
        {
            var newEntity = new UserEntity(message);
            Register(newEntity);
        }

        public void Update(SDespawnUser message)
        {
            var entity = (UserEntity) GetOrNull(message.User);
            if (entity != null)
            {
                OnEntityDeleted(entity);
                _entities.Remove(message.User);
            }
        }

        internal void Register(IEntity newEntity)
        {
            _entities[newEntity.Id] = newEntity;
            OnEntityUpdated(newEntity);
        }

        public void Update(SCreatureLife m)
        {
            var entity = GetOrNull(m.User);
            if (entity != null)
            {
                entity.Position = m.Position;
                entity.Dead = m.Dead;
                OnEntityUpdated(entity);
            }else if (m.User == CompassUser.Id)
            {
                CompassUser.Position = m.Position;
                CompassUser.Dead = m.Dead;
                OnEntityUpdated(CompassUser);
            }
            
        }
        public void Update(S_DEAD_LOCATION m)
        {
            var entity = GetOrNull(m.EntityId);
            if (entity != null)
            {
                entity.Position = m.Location;
                entity.Dead = true;
                OnEntityUpdated(entity);
            }

        }

        public void Update(C_PLAYER_LOCATION m)
        {
            if (CompassUser == null) return; //Don't know how, but sometimes this happens.
            CompassUser.Heading = m.Heading;
            CompassUser.Position = m.Position;
            OnEntityUpdated(CompassUser);
        }

        public void Update(C_PLAYER_FLYING_LOCATION m)
        {
            if (CompassUser == null) return; //Don't know how, but sometimes this happens.
            CompassUser.Position = m.Position;
            OnEntityUpdated(CompassUser);
        }

        public void Update(S_USER_LOCATION m)
        {
            var entity = GetOrNull(m.Entity);
            if (entity == null) return;
            entity.Position = m.Start;
            OnEntityUpdated(entity);
        }
        public void Update(S_CHANGE_RELATION m)
        {
            var entity = GetOrNull(m.EntityId);
            if (entity == null) return;
            entity.Relation = (RelationType)m.Relation;
            OnEntityUpdated(entity);
        }
        public void Update(SpawnMeServerMessage m)
        {
            if (CompassUser == null) return; //Don't know how, but sometimes this happens.
            CompassUser.Position = m.Position;
            OnEntitysCleared(CompassUser);
        }
        public void Update(SUserStatus m)
        {
            var entity = GetOrNull(m.User);
            if (entity != null)
            {
                entity.Status = m.Status;
                OnEntityUpdated(entity);
            }
            else if (m.User == CompassUser.Id)
            {
                CompassUser.Status = m.Status;
                OnEntityUpdated(CompassUser);
            }
        }
        public void Update(S_RETURN_TO_LOBBY m)
        {
            OnEntitysCleared(CompassUser);
            Capture.TeraModule.Settings.Services.GameState = Capture.TeraModule.Settings.GameState.InLobby;
        }
        public void Update(S_USER_LOCATION_IN_ACTION m)
        {
            var entity = GetOrNull(m.EntityId);
            if (entity == null) return;
            entity.Position = m.Position;
            OnEntityUpdated(entity);
        }
        public void Update(S_USER_FLYING_LOCATION m)
        {
            var entity = GetOrNull(m.EntityId);
            if (entity == null) return;
            entity.Position = m.Position;
            OnEntityUpdated(entity);
        }
        public void Update(S_SPAWN_COLLECTION m)
        {
            var newEntity = new CollectionEntity();
            newEntity.Position = m.Position;
            newEntity.CollectionId = m.CollectionId;
            newEntity.Amount = m.Amount;
            newEntity.Angle = m.Angle;
            newEntity.Extractor = m.Extractor;
            newEntity.ExtractorDisabled = m.ExtractorDisabled;
            newEntity.Id = m.EntityId;
            if (newEntity.ColorType != ColorType.Grey)
            {
                newEntity.ZoneId = CompassUser.ZoneId;
                Register(newEntity);
            }
        }
        public void Update(S_DESPAWN_COLLECTION m)
        {
            var entity = (CollectionEntity)GetOrNull(m.EntityId);
            if (entity != null)
            {
                OnEntityDeleted(entity);
                _entities.Remove(m.EntityId);
            }
        }
        public void Update(S_LOAD_TOPO m)
        {
            if (CompassUser == null) return;
            CompassUser.ZoneId = m.AreaId;
        }
        /** Easy integrate style - compatible */

        public void Update(ParsedMessage message)
        {
            message.On<SpawnUserServerMessage>(Update);
            message.On<LoginServerMessage>(Update);
            message.On<C_PLAYER_LOCATION>(Update);
            message.On<C_PLAYER_FLYING_LOCATION>(Update);
            message.On<SNpcLocation>(Update);
            message.On<S_USER_LOCATION>(Update);
            message.On<SDespawnUser>(Update);
            message.On<SpawnMeServerMessage>(Update);
            message.On<S_CHANGE_RELATION>(Update);
            message.On<SCreatureLife>(Update);
            message.On<S_DEAD_LOCATION>(Update);
            message.On<SUserStatus>(Update); 
            message.On<S_RETURN_TO_LOBBY>(Update); 
            message.On<S_USER_LOCATION_IN_ACTION>(Update); 
            message.On<S_USER_FLYING_LOCATION>(Update);
            message.On<S_SPAWN_COLLECTION>(Update);
            message.On<S_DESPAWN_COLLECTION>(Update);
            message.On<S_LOAD_TOPO>(Update);
        }

        private Entity LoginMe(LoginServerMessage m)
        {
            CompassUser = new UserEntity(m);
            return CompassUser;
        }

        public IEntity GetOrNull(EntityId id)
        {
            IEntity entity;
            _entities.TryGetValue(id, out entity);
            return entity;
        }

        public IEntity GetOrPlaceholder(EntityId id)
        {
            if (id == EntityId.Empty)
                return null;
            var entity = GetOrNull(id);
            if (entity != null)
                return entity;
            return new PlaceHolderEntity(id);
        }
    }
}
