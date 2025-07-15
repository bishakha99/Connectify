using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Connectify.DAL;
using Connectify.DAL.Entities;
using Connectify.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Connectify.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ConnectifyDb _context;
        private readonly IUserConnectionManager _userConnectionManager;

        public ChatHub(ConnectifyDb context, IUserConnectionManager userConnectionManager)
        {
            _context = context;
            _userConnectionManager = userConnectionManager;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            if (user != null)
            {
                user.IsActive = true;
                _context.Update(user);
                await _context.SaveChangesAsync();
                _userConnectionManager.AddUser(userId, Context.ConnectionId);
                await Clients.All.SendAsync("UserStatusChanged", userId, true);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);
            
            if (user != null)
            {
                user.IsActive = false;
                _context.Update(user);
                await _context.SaveChangesAsync();
                _userConnectionManager.RemoveUser(userId);
                await Clients.All.SendAsync("UserStatusChanged", userId, false);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(long receiverId, string messageText)
        {
            var senderId = GetCurrentUserId();
            var senderUsername = (await _context.Users.FindAsync(senderId)).Username;

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageText = messageText,
                SentAt = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var receiverConnectionId = _userConnectionManager.GetConnectionId(receiverId);
            if (!string.IsNullOrEmpty(receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", senderId, senderUsername, messageText, null);
            }
            await Clients.Caller.SendAsync("ReceiveMessage", senderId, senderUsername, messageText, null);
        }

        public async Task SendImageMessage(long receiverId, string imageUrl)
        {
            var senderId = GetCurrentUserId();
            var senderUsername = (await _context.Users.FindAsync(senderId)).Username;

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                ImageUrl = imageUrl,
                SentAt = DateTime.UtcNow
            };
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            var receiverConnectionId = _userConnectionManager.GetConnectionId(receiverId);
            if (!string.IsNullOrEmpty(receiverConnectionId))
            {
                await Clients.Client(receiverConnectionId).SendAsync("ReceiveMessage", senderId, senderUsername, null, imageUrl);
            }
            await Clients.Caller.SendAsync("ReceiveMessage", senderId, senderUsername, null, imageUrl);
        }
        
        private long GetCurrentUserId()
        {
            return long.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier).Value);
        }
        public async Task MarkMessagesAsRead(long conversationPartnerId)
        {
            var currentUserId = GetCurrentUserId();

            // Find all unread messages sent by the partner to the current user
            var messagesToUpdate = await _context.Messages
                .Where(m => m.SenderId == conversationPartnerId &&
                             m.ReceiverId == currentUserId &&
                             !m.IsRead)
                .ToListAsync();

            if (messagesToUpdate.Any())
            {
                foreach (var message in messagesToUpdate)
                {
                    message.IsRead = true;
                }
                _context.UpdateRange(messagesToUpdate);
                await _context.SaveChangesAsync();
            }

            // Notify the original sender that their messages have been read
            var partnerConnectionId = _userConnectionManager.GetConnectionId(conversationPartnerId);
            if (!string.IsNullOrEmpty(partnerConnectionId))
            {
                // Tell the partner's client "The user you're talking to (currentUserId) has read your messages"
                await Clients.Client(partnerConnectionId).SendAsync("MessagesHaveBeenRead", currentUserId);
            }
        }
    }
}