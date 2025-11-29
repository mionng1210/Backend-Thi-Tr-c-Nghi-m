using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using ChatService.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatService.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChatDbContext _context;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ChatDbContext context, ILogger<ChatHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    // Lấy danh sách các room mà user là thành viên
                    var userRooms = await _context.ChatRoomMembers
                        .AsNoTracking()
                        .Where(m => m.UserId == userId && m.IsActive)
                        .Select(m => m.RoomId)
                        .ToListAsync();

                    // Join user vào các group tương ứng với các room
                    foreach (var roomId in userRooms)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
                    }

                    _logger.LogInformation("User {UserId} connected to chat hub with connection {ConnectionId}", 
                        userId, Context.ConnectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync for connection {ConnectionId}", Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogInformation("User {UserId} disconnected from chat hub with connection {ConnectionId}", 
                        userId, Context.ConnectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync for connection {ConnectionId}", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Join một room chat cụ thể
        /// </summary>
        public async Task JoinRoom(int roomId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    await Clients.Caller.SendAsync("Error", "Không thể xác thực người dùng");
                    return;
                }

                // Kiểm tra user có quyền join room không
                var isMember = await _context.ChatRoomMembers
                    .AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.IsActive);

                if (!isMember)
                {
                    await Clients.Caller.SendAsync("Error", "Bạn không có quyền truy cập phòng chat này");
                    return;
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"Room_{roomId}");
                await Clients.Caller.SendAsync("JoinedRoom", roomId);

                _logger.LogInformation("User {UserId} joined room {RoomId}", userId, roomId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room {RoomId}", roomId);
                await Clients.Caller.SendAsync("Error", "Đã xảy ra lỗi khi tham gia phòng chat");
            }
        }

        /// <summary>
        /// Leave một room chat cụ thể
        /// </summary>
        public async Task LeaveRoom(int roomId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Room_{roomId}");
                await Clients.Caller.SendAsync("LeftRoom", roomId);

                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving room {RoomId}", roomId);
                await Clients.Caller.SendAsync("Error", "Đã xảy ra lỗi khi rời phòng chat");
            }
        }

        /// <summary>
        /// Báo hiệu user đang typing
        /// </summary>
        public async Task StartTyping(int roomId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return;
                }

                // Lấy thông tin user
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user != null)
                {
                    await Clients.GroupExcept($"Room_{roomId}", Context.ConnectionId)
                        .SendAsync("UserStartedTyping", new { 
                            UserId = userId, 
                            UserName = user.FullName ?? "Unknown",
                            RoomId = roomId 
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StartTyping for room {RoomId}", roomId);
            }
        }

        /// <summary>
        /// Báo hiệu user đã ngừng typing
        /// </summary>
        public async Task StopTyping(int roomId)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return;
                }

                await Clients.GroupExcept($"Room_{roomId}", Context.ConnectionId)
                    .SendAsync("UserStoppedTyping", new { 
                        UserId = userId, 
                        RoomId = roomId 
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StopTyping for room {RoomId}", roomId);
            }
        }

        /// <summary>
        /// Cập nhật trạng thái online của user
        /// </summary>
        public async Task UpdateOnlineStatus(bool isOnline)
        {
            try
            {
                var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Context.User?.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return;
                }

                // Lấy danh sách các room mà user là thành viên
                var userRooms = await _context.ChatRoomMembers
                    .AsNoTracking()
                    .Where(m => m.UserId == userId && m.IsActive)
                    .Select(m => m.RoomId)
                    .ToListAsync();

                // Thông báo trạng thái online cho tất cả các room
                foreach (var roomId in userRooms)
                {
                    await Clients.GroupExcept($"Room_{roomId}", Context.ConnectionId)
                        .SendAsync("UserOnlineStatusChanged", new { 
                            UserId = userId, 
                            IsOnline = isOnline,
                            RoomId = roomId 
                        });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating online status for user");
            }
        }
    }
}