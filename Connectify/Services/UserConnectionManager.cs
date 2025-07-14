using System.Collections.Concurrent;

namespace Connectify.Services
{
    public class UserConnectionManager : IUserConnectionManager
    {
        private static readonly ConcurrentDictionary<long, string> _userConnections = new ConcurrentDictionary<long, string>();

        public void AddUser(long userId, string connectionId)
        {
            _userConnections[userId] = connectionId;
        }


        public void RemoveUser(long userId)
        {
            _userConnections.TryRemove(userId, out _);
        }

        public string GetConnectionId(long userId)
        {
            _userConnections.TryGetValue(userId, out var connectionId);
            return connectionId;
        }

        public IEnumerable<long> GetOnlineUsers()
        {
            return _userConnections.Keys.ToList();
        }
    }
}