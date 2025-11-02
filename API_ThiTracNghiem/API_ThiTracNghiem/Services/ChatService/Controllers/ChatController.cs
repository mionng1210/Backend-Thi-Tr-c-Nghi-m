using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatService.Data;
using ChatService.Models;
using ChatService.Hubs;
using ChatService.Services;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ChatService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ChatDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<ChatController> _logger;
        private readonly IUserSyncService _userSyncService;

        public ChatController(ChatDbContext context, IHubContext<ChatHub> hubContext, ILogger<ChatController> logger, IUserSyncService userSyncService)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _userSyncService = userSyncService;
        }

        /// <summary>
        /// Lấy toàn bộ lịch sử chat của một room
        /// </summary>
        [HttpGet("{roomId}")]
        public async Task<IActionResult> GetChatHistory(
            int roomId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page <= 0) page = 1;
                if (pageSize <= 0 || pageSize > 100) pageSize = 50;

                // Kiểm tra room có tồn tại không
                var room = await _context.ChatRooms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RoomId == roomId && !r.HasDelete);

                if (room == null)
                {
                    return NotFound(new { success = false, message = "Phòng chat không tồn tại" });
                }

                // Lấy userId từ token (claim "sub")
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không thể xác thực người dùng" });
                }

                // Kiểm tra user có quyền truy cập room không
                var isMember = await _context.ChatRoomMembers
                    .AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.IsActive);

                if (!isMember)
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền truy cập phòng chat này" });
                }

                // Lấy tổng số tin nhắn
                var totalMessages = await _context.ChatMessages
                    .CountAsync(m => m.RoomId == roomId && !m.HasDelete);

                // Lấy tin nhắn với phân trang (mới nhất trước)
                var messages = await _context.ChatMessages
                    .AsNoTracking()
                    .Where(m => m.RoomId == roomId && !m.HasDelete)
                    .Include(m => m.Sender)
                    .Include(m => m.ReplyToMessage)
                    .ThenInclude(rm => rm!.Sender)
                    .OrderByDescending(m => m.SentAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new ChatMessageResponse
                    {
                        MessageId = m.MessageId,
                        RoomId = m.RoomId,
                        SenderId = m.SenderId,
                        SenderName = m.Sender != null ? m.Sender.FullName ?? "Unknown" : "Unknown",
                        SenderAvatar = m.Sender != null ? m.Sender.AvatarUrl : null,
                        Content = m.Content,
                        MessageType = m.MessageType,
                        AttachmentUrl = m.AttachmentUrl,
                        AttachmentName = m.AttachmentName,
                        ReplyToMessageId = m.ReplyToMessageId,
                        ReplyToMessage = m.ReplyToMessage != null ? new ChatMessageResponse
                        {
                            MessageId = m.ReplyToMessage.MessageId,
                            SenderId = m.ReplyToMessage.SenderId,
                            SenderName = m.ReplyToMessage.Sender != null ? m.ReplyToMessage.Sender.FullName ?? "Unknown" : "Unknown",
                            Content = m.ReplyToMessage.Content,
                            MessageType = m.ReplyToMessage.MessageType,
                            SentAt = m.ReplyToMessage.SentAt
                        } : null,
                        SentAt = m.SentAt,
                        IsEdited = m.IsEdited,
                        EditedAt = m.EditedAt
                    })
                    .ToListAsync();

                // Đảo ngược để tin nhắn cũ nhất ở đầu
                messages.Reverse();

                // Lấy số lượng thành viên
                var memberCount = await _context.ChatRoomMembers
                    .CountAsync(m => m.RoomId == roomId && m.IsActive);

                var roomResponse = new ChatRoomResponse
                {
                    RoomId = room.RoomId,
                    Name = room.Name,
                    Description = room.Description,
                    RoomType = room.RoomType,
                    CreatedBy = room.CreatedBy,
                    CreatorName = "User", // Tạm thời hardcode
                    CreatedAt = room.CreatedAt,
                    IsActive = room.IsActive,
                    MemberCount = memberCount
                };

                var response = new ChatHistoryResponse
                {
                    Room = roomResponse,
                    Messages = messages,
                    TotalMessages = totalMessages,
                    Page = page,
                    PageSize = pageSize,
                    HasNextPage = (page * pageSize) < totalMessages
                };

                return Ok(new { success = true, data = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat history for room {RoomId}", roomId);
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi lấy lịch sử chat" });
            }
        }

        /// <summary>
        /// Gửi tin nhắn vào room
        /// </summary>
        [HttpPost("{roomId}")]
        public async Task<IActionResult> SendMessage(int roomId, [FromBody] SendMessageRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Content))
                {
                    return BadRequest(new { success = false, message = "Nội dung tin nhắn không được để trống" });
                }

                // Lấy userId từ token (claim "sub")
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (!int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không thể xác thực người dùng" });
                }

                // Kiểm tra room có tồn tại không
                var room = await _context.ChatRooms
                    .FirstOrDefaultAsync(r => r.RoomId == roomId && !r.HasDelete);

                if (room == null)
                {
                    return NotFound(new { success = false, message = "Phòng chat không tồn tại" });
                }

                // Kiểm tra user có quyền gửi tin nhắn không
                var membership = await _context.ChatRoomMembers
                    .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId && m.IsActive);

                if (membership == null)
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền gửi tin nhắn trong phòng chat này" });
                }

                // Kiểm tra tin nhắn reply (nếu có)
                ChatMessage? replyToMessage = null;
                if (request.ReplyToMessageId.HasValue)
                {
                    replyToMessage = await _context.ChatMessages
                        .FirstOrDefaultAsync(m => m.MessageId == request.ReplyToMessageId.Value && 
                                                 m.RoomId == roomId && !m.HasDelete);
                    
                    if (replyToMessage == null)
                    {
                        return BadRequest(new { success = false, message = "Tin nhắn được reply không tồn tại" });
                    }
                }

                // Tạo tin nhắn mới
                var message = new ChatMessage
                {
                    RoomId = roomId,
                    SenderId = userId,
                    Content = request.Content.Trim(),
                    MessageType = request.MessageType,
                    AttachmentUrl = request.AttachmentUrl,
                    AttachmentName = request.AttachmentName,
                    ReplyToMessageId = request.ReplyToMessageId,
                    SentAt = DateTime.UtcNow
                };

                _context.ChatMessages.Add(message);
                await _context.SaveChangesAsync();

                // Load thông tin sender để trả về
                var sender = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                var messageResponse = new ChatMessageResponse
                {
                    MessageId = message.MessageId,
                    RoomId = message.RoomId,
                    SenderId = message.SenderId,
                    SenderName = sender?.FullName ?? "Unknown",
                    SenderAvatar = sender?.AvatarUrl,
                    Content = message.Content,
                    MessageType = message.MessageType,
                    AttachmentUrl = message.AttachmentUrl,
                    AttachmentName = message.AttachmentName,
                    ReplyToMessageId = message.ReplyToMessageId,
                    ReplyToMessage = replyToMessage != null ? new ChatMessageResponse
                    {
                        MessageId = replyToMessage.MessageId,
                        SenderId = replyToMessage.SenderId,
                        Content = replyToMessage.Content,
                        MessageType = replyToMessage.MessageType,
                        SentAt = replyToMessage.SentAt
                    } : null,
                    SentAt = message.SentAt,
                    IsEdited = message.IsEdited,
                    EditedAt = message.EditedAt
                };

                // Emit tin nhắn qua SignalR
                await _hubContext.Clients.Group($"Room_{roomId}")
                    .SendAsync("ReceiveMessage", messageResponse);

                return Ok(new { success = true, data = messageResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to room {RoomId}", roomId);
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi gửi tin nhắn" });
            }
        }

        /// <summary>
        /// Tạo phòng chat mới
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
        {
            try
            {
                _logger.LogInformation("CreateRoom: Method called");
                _logger.LogInformation($"CreateRoom: User.Identity.IsAuthenticated = {User.Identity?.IsAuthenticated}");
                _logger.LogInformation($"CreateRoom: User claims count = {User.Claims.Count()}");
                
                foreach (var claim in User.Claims)
                {
                    _logger.LogInformation($"CreateRoom: Claim - {claim.Type}: {claim.Value}");
                }
                
                // Lấy user ID từ JWT token
                var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _logger.LogInformation($"CreateRoom: userId from sub claim = {User.FindFirst("sub")?.Value}");
                _logger.LogInformation($"CreateRoom: userId from NameIdentifier claim = {User.FindFirst(ClaimTypes.NameIdentifier)?.Value}");
                _logger.LogInformation($"CreateRoom: final userId = {userId}");
                
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int creatorId))
                {
                    _logger.LogWarning("CreateRoom: Token không hợp lệ hoặc không có sub claim");
                    return Unauthorized(new { success = false, message = "Token không hợp lệ" });
                }

                // Đồng bộ user từ AuthService trước khi tạo ChatRoomMember
                var userSyncDto = await _userSyncService.GetUserByIdAsync(creatorId);
                if (userSyncDto == null)
                {
                    return BadRequest(new { success = false, message = "Không thể lấy thông tin user từ AuthService" });
                }

                // Kiểm tra xem user đã tồn tại trong ChatService database chưa
                var existingUser = await _context.Users.FindAsync(creatorId);
                if (existingUser == null)
                {
                    // Tạo user mới trong ChatService database với thông tin cơ bản
                    var newUser = new User
                    {
                        UserId = userSyncDto.UserId,
                        Email = userSyncDto.Email ?? "",
                        FullName = userSyncDto.FullName ?? "",
                        RoleId = userSyncDto.RoleId ?? 3, // Default Student role
                        Status = userSyncDto.Status ?? "Active",
                        IsEmailVerified = userSyncDto.IsEmailVerified,
                        CreatedAt = userSyncDto.CreatedAt,
                        UpdatedAt = userSyncDto.UpdatedAt ?? DateTime.UtcNow,
                        HasDelete = userSyncDto.HasDelete
                    };
                    _context.Users.Add(newUser);
                    await _context.SaveChangesAsync();
                }

                // Tạo phòng chat mới
                var room = new ChatRoom
                {
                    Name = request.Name,
                    Description = request.Description,
                    RoomType = request.RoomType,
                    CourseId = null, // Đặt null vì không sử dụng
                    ExamId = null,   // Đặt null vì không sử dụng
                    CreatedBy = creatorId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.ChatRooms.Add(room);
                await _context.SaveChangesAsync();

                // Thêm creator làm admin của phòng
                var membership = new ChatRoomMember
                {
                    RoomId = room.RoomId,
                    UserId = creatorId,
                    Role = "admin",
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.ChatRoomMembers.Add(membership);
                await _context.SaveChangesAsync();

                var roomResponse = new ChatRoomResponse
                {
                    RoomId = room.RoomId,
                    Name = room.Name,
                    Description = room.Description,
                    RoomType = room.RoomType,
                    CreatedBy = room.CreatedBy,
                    CreatorName = userSyncDto.FullName ?? userSyncDto.Email, // Sử dụng thông tin từ sync
                    CreatedAt = room.CreatedAt,
                    IsActive = room.IsActive,
                    MemberCount = 1
                };

                return Ok(new { success = true, data = roomResponse });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi tạo phòng chat" });
            }
        }

        /// <summary>
        /// Lấy danh sách phòng chat của user
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUserRooms([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                if (page <= 0) page = 1;
                if (pageSize <= 0 || pageSize > 50) pageSize = 20;

                // Lấy userId từ token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int currentUserId))
                {
                    return Unauthorized(new { success = false, message = "Token không hợp lệ" });
                }

                // Lấy danh sách phòng chat mà user tham gia
                var query = _context.ChatRoomMembers
                    .AsNoTracking()
                    .Where(m => m.UserId == currentUserId && m.IsActive)
                    .Include(m => m.Room)
                    .ThenInclude(r => r!.Creator)
                    .Where(m => m.Room != null && !m.Room.HasDelete && m.Room.IsActive);

                var totalRooms = await query.CountAsync();
                var skip = (page - 1) * pageSize;

                var rooms = await query
                    .Skip(skip)
                    .Take(pageSize)
                    .Select(m => new ChatRoomResponse
                    {
                        RoomId = m.Room!.RoomId,
                        Name = m.Room.Name,
                        Description = m.Room.Description,
                        RoomType = m.Room.RoomType,
                        CreatedBy = m.Room.CreatedBy,
                        CreatorName = "User", // Tạm thời hardcode
                        CreatedAt = m.Room.CreatedAt,
                        IsActive = m.Room.IsActive,
                        MemberCount = m.Room.Members.Count(mem => mem.IsActive)
                    })
                    .ToListAsync();

                return Ok(new 
                { 
                    success = true, 
                    data = rooms,
                    pagination = new
                    {
                        page,
                        pageSize,
                        totalItems = totalRooms,
                        totalPages = (int)Math.Ceiling((double)totalRooms / pageSize),
                        hasNextPage = skip + pageSize < totalRooms
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user rooms");
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi lấy danh sách phòng chat" });
            }
        }

        /// <summary>
        /// Tham gia phòng chat
        /// </summary>
        [HttpPost("{roomId}/join")]
        public async Task<IActionResult> JoinRoom(int roomId)
        {
            try
            {
                // Lấy userId từ token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int currentUserId))
                {
                    return Unauthorized(new { success = false, message = "Token không hợp lệ" });
                }

                // Kiểm tra phòng có tồn tại không
                var room = await _context.ChatRooms
                    .FirstOrDefaultAsync(r => r.RoomId == roomId && !r.HasDelete && r.IsActive);

                if (room == null)
                {
                    return NotFound(new { success = false, message = "Phòng chat không tồn tại" });
                }

                // Kiểm tra user đã tham gia chưa
                var existingMembership = await _context.ChatRoomMembers
                    .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == currentUserId);

                if (existingMembership != null)
                {
                    if (existingMembership.IsActive)
                    {
                        return BadRequest(new { success = false, message = "Bạn đã tham gia phòng chat này rồi" });
                    }
                    else
                    {
                        // Kích hoạt lại membership
                        existingMembership.IsActive = true;
                        existingMembership.JoinedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Tạo membership mới
                    var membership = new ChatRoomMember
                    {
                        RoomId = roomId,
                        UserId = currentUserId,
                        Role = "member",
                        JoinedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.ChatRoomMembers.Add(membership);
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Đã tham gia phòng chat thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room {RoomId}", roomId);
                return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi tham gia phòng chat" });
            }
        }

        /// <summary>
        /// Test endpoint không cần authorization
        /// </summary>
        [HttpGet("test")]
        [AllowAnonymous]
        public IActionResult Test()
        {
            return Ok(new { success = true, message = "ChatService is working", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Debug JWT endpoint
        /// </summary>
        [HttpGet("debug-jwt")]
        [AllowAnonymous]
        public IActionResult DebugJwt()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                return Ok(new { success = false, message = "No Authorization header" });
            }

            if (!authHeader.StartsWith("Bearer "))
            {
                return Ok(new { success = false, message = "Invalid Authorization header format" });
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            
            try
            {
                // Decode JWT payload để xem thông tin
                var parts = token.Split('.');
                if (parts.Length != 3)
                {
                    return Ok(new { success = false, message = "Invalid JWT format" });
                }

                var payload = parts[1];
                // Add padding if needed
                payload += new string('=', (4 - payload.Length % 4) % 4);
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                
                return Ok(new { 
                    success = true, 
                    message = "JWT received and decoded", 
                    payload = decoded,
                    tokenLength = token.Length
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"JWT decode error: {ex.Message}" });
            }
        }
    }
}