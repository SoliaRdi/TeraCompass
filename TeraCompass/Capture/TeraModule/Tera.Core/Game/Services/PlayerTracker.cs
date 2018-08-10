using System;
using System.Collections;
using System.Collections.Generic;
using TeraCompass.Tera.Core.Game.Messages;
using TeraCompass.Tera.Core.Game.Messages.Server;

namespace TeraCompass.Tera.Core.Game.Services
{
    public class PlayerTracker : IEnumerable<Player>
    {
        private readonly EntityTracker _entityTracker;
        private readonly Dictionary<Tuple<uint, uint>, Player> _playerById = new Dictionary<Tuple<uint, uint>, Player>();
        private readonly ServerDatabase _serverDatabase;
        private Player _unknownDamage;
        private List<Tuple<uint, uint>> _currentParty = new List<Tuple<uint, uint>>();

        public int PartySize => _currentParty.Count;
        
        public PlayerTracker(EntityTracker entityTracker, ServerDatabase serverDatabase = null)
        {
            _serverDatabase = serverDatabase;
            _entityTracker = entityTracker;
        }


        public IEnumerator<Player> GetEnumerator()
        {
            return _playerById.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public delegate void PlayerIdChangedEvent(EntityId oldId, EntityId newId);
        public event PlayerIdChangedEvent PlayerIdChangedAction;

        public delegate void PartyChange();
        public event PartyChange PartyChangedEvent;
        


        public Player Get(uint serverId, uint playerId)
        {
            return _playerById[Tuple.Create(serverId, playerId)];
        }

        public Player GetOrNull(uint serverId, uint playerId)
        {
            Player result;
            _playerById.TryGetValue(Tuple.Create(serverId, playerId), out result);
            return result;
        }

        public void UpdateParty(S_BAN_PARTY message)
        {
            _currentParty = new List<Tuple<uint, uint>>();
            PartyChangedEvent?.Invoke();
        }

        public void UpdateParty(S_LEAVE_PARTY m )
        {
            _currentParty = new List<Tuple<uint, uint>>();
            PartyChangedEvent?.Invoke();
        }

        public void UpdateParty(S_LEAVE_PARTY_MEMBER m)
        {
            _currentParty.Remove(Tuple.Create(m.ServerId, m.PlayerId));
            PartyChangedEvent?.Invoke();
        }

        public void UpdateParty(S_BAN_PARTY_MEMBER m)
        {
            _currentParty.Remove(Tuple.Create(m.ServerId, m.PlayerId));
            PartyChangedEvent?.Invoke();
        }

        public void UpdateParty(S_PARTY_MEMBER_LIST m)
        {
            _currentParty = m.Party.ConvertAll(x => Tuple.Create(x.ServerId, x.PlayerId));
            PartyChangedEvent?.Invoke();
        }

        public void UpdateParty(ParsedMessage message)
        {
            message.On<S_BAN_PARTY>(m => UpdateParty(m));
            message.On<S_LEAVE_PARTY>(m => UpdateParty(m));
            message.On<S_LEAVE_PARTY_MEMBER>(m => UpdateParty(m));
            message.On<S_BAN_PARTY_MEMBER>(m => UpdateParty(m));
            message.On<S_PARTY_MEMBER_LIST>(m => UpdateParty(m));
        }

        public bool MyParty(Player player)
        {
            if (player == null) return false;
            return _currentParty.Contains(Tuple.Create(player.ServerId, player.PlayerId)) ||
                   player.User == _entityTracker.CompassUser;
        }

        public bool MyParty(uint serverId, uint playerId)
        {
            return _currentParty.Contains(Tuple.Create(serverId, playerId)) ||
                   (playerId == _entityTracker.CompassUser.PlayerId && serverId == _entityTracker.CompassUser.ServerId);
        }
        
        public List<UserEntity> PartyList()
        {
            List<UserEntity> list = new List<UserEntity>();
            _currentParty.ForEach(x =>
                {
                    if (_playerById.TryGetValue(x, out Player player)) list.Add(player.User);
                });
            if (_entityTracker.CompassUser != null && !list.Contains(_entityTracker.CompassUser)) list.Add(_entityTracker.CompassUser);
            return list;
        }

        public Player Me()
        {
            var user = _entityTracker.CompassUser;
            if (user != null) return Get(user.ServerId, user.PlayerId);
            return null;
        }

        protected virtual void OnPlayerIdChangedAction(EntityId oldid, EntityId newid)
        {
            PlayerIdChangedAction?.Invoke(oldid, newid);
        }

    }
}