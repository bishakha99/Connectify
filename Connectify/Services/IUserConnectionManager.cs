namespace Connectify.Services
{
    public interface IUserConnectionManager
    {
        void AddUser(long userId, string connectionId);
        void RemoveUser(long userId);
        string GetConnectionId(long userId);
        IEnumerable<long> GetOnlineUsers();
    }
}