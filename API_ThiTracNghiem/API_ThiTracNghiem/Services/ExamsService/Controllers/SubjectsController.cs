using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using ExamsService.Data;
using ExamsService.Models;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/subjects")]
    public class SubjectsController : ControllerBase
    {
        private readonly ExamsDbContext _context;

        public SubjectsController(ExamsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách tất cả môn học
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllSubjects()
        {
            try
            {
                var subjects = await _context.Subjects
                    .OrderBy(s => s.Name)
                    .Select(s => new
                    {
                        s.SubjectId,
                        s.Name,
                        s.Description,
                        s.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { message = "Lấy danh sách môn học thành công", data = subjects });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy chi tiết môn học theo ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetSubjectById(int id)
        {
            try
            {
                var subject = await _context.Subjects
                    .Where(s => s.SubjectId == id)
                    .Select(s => new
                    {
                        s.SubjectId,
                        s.Name,
                        s.Description,
                        s.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (subject == null)
                {
                    return NotFound(new { message = "Không tìm thấy môn học" });
                }

                return Ok(new { message = "Lấy thông tin môn học thành công", data = subject });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }

        /// <summary>
        /// Tạo môn học mới
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            try
            {
                // Check if subject name already exists
                var existingSubject = await _context.Subjects
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == request.Name.ToLower());

                if (existingSubject != null)
                {
                    return BadRequest(new { message = "Tên môn học đã tồn tại" });
                }

                var subject = new Subject
                {
                    Name = request.Name.Trim(),
                    Description = request.Description?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Subjects.Add(subject);
                await _context.SaveChangesAsync();

                var response = new
                {
                    subject.SubjectId,
                    subject.Name,
                    subject.Description,
                    subject.CreatedAt
                };

                return Ok(new { message = "Tạo môn học thành công", data = response });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật thông tin môn học
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdateSubject(int id, [FromBody] UpdateSubjectRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ", errors = ModelState });
            }

            try
            {
                var subject = await _context.Subjects
                    .FirstOrDefaultAsync(s => s.SubjectId == id);

                if (subject == null)
                {
                    return NotFound(new { message = "Không tìm thấy môn học" });
                }

                // Check if new name conflicts with existing subject
                if (!string.IsNullOrEmpty(request.Name) && request.Name.Trim().ToLower() != subject.Name.ToLower())
                {
                    var existingSubject = await _context.Subjects
                        .FirstOrDefaultAsync(s => s.SubjectId != id && s.Name.ToLower() == request.Name.Trim().ToLower());

                    if (existingSubject != null)
                    {
                        return BadRequest(new { message = "Tên môn học đã tồn tại" });
                    }
                }

                // Update subject
                if (!string.IsNullOrEmpty(request.Name))
                {
                    subject.Name = request.Name.Trim();
                }

                if (request.Description != null)
                {
                    subject.Description = request.Description.Trim();
                }

                await _context.SaveChangesAsync();

                var response = new
                {
                    subject.SubjectId,
                    subject.Name,
                    subject.Description,
                    subject.CreatedAt
                };

                return Ok(new { message = "Cập nhật môn học thành công", data = response });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }

        /// <summary>
        /// Xóa môn học
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteSubject(int id)
        {
            try
            {
                var subject = await _context.Subjects
                    .FirstOrDefaultAsync(s => s.SubjectId == id);

                if (subject == null)
                {
                    return NotFound(new { message = "Không tìm thấy môn học" });
                }

                // Check if subject is used in any question banks
                var isUsedInQuestionBanks = await _context.QuestionBanks
                    .AnyAsync(qb => qb.SubjectId == id && !qb.HasDelete);

                // Check if subject is used in any courses
                var isUsedInCourses = await _context.Courses
                    .AnyAsync(c => c.SubjectId == id && !c.HasDelete);

                if (isUsedInQuestionBanks || isUsedInCourses)
                {
                    return BadRequest(new { message = "Không thể xóa môn học đang được sử dụng" });
                }

                _context.Subjects.Remove(subject);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Xóa môn học thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi server", error = ex.Message });
            }
        }
    }

    // DTOs
    public class CreateSubjectRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }
    }

    public class UpdateSubjectRequest
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}

