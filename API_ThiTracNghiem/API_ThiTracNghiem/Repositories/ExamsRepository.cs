using System.Linq;
using System.Threading.Tasks;
using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Models;
using Microsoft.EntityFrameworkCore;

namespace API_ThiTracNghiem.Repositories
{
    public class ExamsRepository : IExamsRepository
    {
        private readonly ApplicationDbContext _db;

        public ExamsRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<PagedResponse<ExamListItemDto>> GetExamsAsync(int pageIndex, int pageSize, int? courseId = null, int? teacherId = null, int? subjectId = null)
        {
            if (pageIndex <= 0) pageIndex = 1;
            if (pageSize <= 0) pageSize = 10;

            var query = _db.Exams
                .AsNoTracking()
                .Where(e => !e.HasDelete)
                .Include(e => e.Course)
                .ThenInclude(c => c!.Teacher)
                .Include(e => e.Course)
                .ThenInclude(c => c!.Subject)
                .AsQueryable();

            // Apply filters
            if (courseId.HasValue)
            {
                query = query.Where(e => e.CourseId == courseId.Value);
            }

            if (teacherId.HasValue)
            {
                query = query.Where(e => e.Course!.TeacherId == teacherId.Value);
            }

            if (subjectId.HasValue)
            {
                query = query.Where(e => e.Course!.SubjectId == subjectId.Value);
            }

            query = query.OrderByDescending(e => e.CreatedAt);

            var total = await query.LongCountAsync();

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new ExamListItemDto
                {
                    Id = e.ExamId,
                    Title = e.Title,
                    Description = e.Description,
                    CourseId = e.CourseId,
                    CourseName = e.Course != null ? e.Course.Title : null,
                    TeacherId = e.Course != null ? e.Course.TeacherId : null,
                    TeacherName = e.Course != null && e.Course.Teacher != null ? e.Course.Teacher.FullName : null,
                    SubjectId = e.Course != null ? e.Course.SubjectId : null,
                    SubjectName = e.Course != null && e.Course.Subject != null ? e.Course.Subject.Name : null,
                    DurationMinutes = e.DurationMinutes,
                    TotalQuestions = e.TotalQuestions,
                    TotalMarks = e.TotalMarks,
                    PassingMark = e.PassingMark,
                    ExamType = e.ExamType,
                    StartAt = e.StartAt,
                    EndAt = e.EndAt,
                    Status = e.Status,
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt
                })
                .ToListAsync();

            return new PagedResponse<ExamListItemDto>
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalItems = total,
                Items = items
            };
        }

        public async Task<ExamDetailDto?> GetByIdAsync(int id)
        {
            // Get exam basic info first
            var exam = await _db.Exams
                .AsNoTracking()
                .Where(e => e.ExamId == id && !e.HasDelete)
                .FirstOrDefaultAsync();

            if (exam == null) return null;

            // Get course info separately
            var course = await _db.Courses
                .AsNoTracking()
                .Where(c => c.CourseId == exam.CourseId)
                .FirstOrDefaultAsync();

            // Get teacher info separately if course exists
            User? teacher = null;
            if (course?.TeacherId != null)
            {
                teacher = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.UserId == course.TeacherId)
                    .FirstOrDefaultAsync();
            }

            // Get subject info separately if course exists
            Subject? subject = null;
            if (course?.SubjectId != null)
            {
                subject = await _db.Subjects
                    .AsNoTracking()
                    .Where(s => s.SubjectId == course.SubjectId)
                    .FirstOrDefaultAsync();
            }

            // Get exam questions separately
            var examQuestions = await _db.ExamQuestions
                .AsNoTracking()
                .Where(eq => eq.ExamId == id)
                .OrderBy(eq => eq.SequenceIndex ?? eq.ExamQuestionId)
                .ToListAsync();

            var questions = new List<ExamQuestionDto>();
            
            foreach (var eq in examQuestions)
            {
                // Get question info
                var question = await _db.Questions
                    .AsNoTracking()
                    .Where(q => q.QuestionId == eq.QuestionId)
                    .FirstOrDefaultAsync();

                // Get answer options for this question
                var answerOptions = await _db.AnswerOptions
                    .AsNoTracking()
                    .Where(ao => ao.QuestionId == eq.QuestionId && !ao.HasDelete)
                    .OrderBy(ao => ao.OrderIndex ?? ao.OptionId)
                    .ToListAsync();

                questions.Add(new ExamQuestionDto
                {
                    ExamQuestionId = eq.ExamQuestionId,
                    QuestionId = eq.QuestionId,
                    Content = question?.Content ?? "",
                    QuestionType = question?.QuestionType,
                    Difficulty = question?.Difficulty,
                    Marks = eq.Marks,
                    SequenceIndex = eq.SequenceIndex,
                    Options = answerOptions.Select(ao => new AnswerOptionDto
                    {
                        OptionId = ao.OptionId,
                        Content = ao.Content,
                        IsCorrect = ao.IsCorrect,
                        SequenceIndex = ao.OrderIndex
                    }).ToList()
                });
            }

            return new ExamDetailDto
            {
                Id = exam.ExamId,
                Title = exam.Title,
                Description = exam.Description,
                CourseId = exam.CourseId,
                CourseName = course?.Title,
                TeacherId = course?.TeacherId,
                TeacherName = teacher?.FullName,
                SubjectId = course?.SubjectId,
                SubjectName = subject?.Name,
                DurationMinutes = exam.DurationMinutes,
                TotalQuestions = exam.TotalQuestions,
                TotalMarks = exam.TotalMarks,
                PassingMark = exam.PassingMark,
                ExamType = exam.ExamType,
                StartAt = exam.StartAt,
                EndAt = exam.EndAt,
                RandomizeQuestions = exam.RandomizeQuestions,
                AllowMultipleAttempts = exam.AllowMultipleAttempts,
                Status = exam.Status,
                CreatedAt = exam.CreatedAt,
                UpdatedAt = exam.UpdatedAt,
                Questions = questions
            };
        }

        public async Task<Exam> CreateAsync(Exam exam)
        {
            _db.Exams.Add(exam);
            await _db.SaveChangesAsync();
            return exam;
        }

        public async Task<Exam?> UpdateAsync(int id, Exam exam)
        {
            var existingExam = await _db.Exams.FindAsync(id);
            if (existingExam == null || existingExam.HasDelete)
                return null;

            existingExam.Title = exam.Title;
            existingExam.Description = exam.Description;
            existingExam.CourseId = exam.CourseId;
            existingExam.DurationMinutes = exam.DurationMinutes;
            existingExam.TotalQuestions = exam.TotalQuestions;
            existingExam.TotalMarks = exam.TotalMarks;
            existingExam.PassingMark = exam.PassingMark;
            existingExam.ExamType = exam.ExamType;
            existingExam.StartAt = exam.StartAt;
            existingExam.EndAt = exam.EndAt;
            existingExam.RandomizeQuestions = exam.RandomizeQuestions;
            existingExam.AllowMultipleAttempts = exam.AllowMultipleAttempts;
            existingExam.Status = exam.Status;
            existingExam.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return existingExam;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var exam = await _db.Exams.FindAsync(id);
            if (exam == null || exam.HasDelete)
                return false;

            exam.HasDelete = true;
            exam.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExamExistsAsync(int id)
        {
            return await _db.Exams.AsNoTracking().AnyAsync(e => e.ExamId == id && !e.HasDelete);
        }

        public async Task<bool> CourseExistsAsync(int courseId)
        {
            return await _db.Courses.AsNoTracking().AnyAsync(c => c.CourseId == courseId && !c.HasDelete);
        }

        public async Task<bool> QuestionExistsAsync(int questionId)
        {
            return await _db.Questions.AsNoTracking().AnyAsync(q => q.QuestionId == questionId && !q.HasDelete);
        }

        public async Task<List<ExamQuestion>> GetExamQuestionsAsync(int examId)
        {
            return await _db.ExamQuestions
                .AsNoTracking()
                .Where(eq => eq.ExamId == examId)
                .OrderBy(eq => eq.SequenceIndex ?? eq.ExamQuestionId)
                .ToListAsync();
        }

        public async Task<ExamQuestion> AddQuestionToExamAsync(ExamQuestion examQuestion)
        {
            _db.ExamQuestions.Add(examQuestion);
            await _db.SaveChangesAsync();
            return examQuestion;
        }

        public async Task<bool> RemoveQuestionFromExamAsync(int examId, int questionId)
        {
            var examQuestion = await _db.ExamQuestions
                .FirstOrDefaultAsync(eq => eq.ExamId == examId && eq.QuestionId == questionId);

            if (examQuestion == null)
                return false;

            _db.ExamQuestions.Remove(examQuestion);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsTeacherOwnerOfExamAsync(int examId, int teacherId)
        {
            return await _db.Exams
                .AsNoTracking()
                .Where(e => e.ExamId == examId && !e.HasDelete)
                .Include(e => e.Course)
                .AnyAsync(e => e.Course != null && e.Course.TeacherId == teacherId);
        }
    }
}