using Connectify.DAL.Entities;

namespace Connectify.ViewModels
{
    public class ChatViewModel
    {
        public User CurrentUser { get; set; }
        public List<User> AllUsers { get; set; }
    }
}