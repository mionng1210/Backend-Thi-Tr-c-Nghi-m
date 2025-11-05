using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Models;

namespace API_ThiTracNghiem.Services
{
    public class ExamsService : IExamsService
    {
        private readonly IExamsRepository _examsRepository;

        public ExamsService(IExamsRepository examsRepository)
        {
            _examsRepository = examsRepository;
        }

        public async Task<PagedResponse<ExamListItemDto>> GetExamsAsync(int pageIndex, int pageSize, int? courseId = null, int? teacherId = null, int? subjectId = null)
        {
            return await _examsRepository.GetExamsAsync(pageIndex, pageSize, courseId, teacherId, subjectId);
        }

        public async Task<ExamDetailDto?> GetExamByIdAsync(int id)
        {
            return await _examsRepository.GetByIdAsync(id);
        }

        public async Task<ExamDetailDto> CreateExamAsync(CreateExamRequest request, int createdBy)
        {
            // Validate course exists
            if (request.CourseId.HasValue && !await _examsRepository.CourseExistsAsync(request.CourseId.Value))
            {
                throw new ArgumentException("Course không tồn tại");
            }

            var exam = new Exam
            {
                Title = request.Title,
                Description = request.Description,
                CourseId = request.CourseId,
                DurationMinutes = request.DurationMinutes,
                TotalQuestions = request.TotalQuestions,
                TotalMarks = request.TotalMarks,
                PassingMark = request.PassingMark,
                ExamType = request.ExamType,
                StartAt = request.StartAt,
                EndAt = request.EndAt,
                RandomizeQuestions = request.RandomizeQuestions,
                AllowMultipleAttempts = request.AllowMultipleAttempts,
                Status = request.Status ?? "Draft",
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                HasDelete = false
            };

            var createdExam = await _examsRepository.CreateAsync(exam);

            // Add questions to exam if provided
            if (request.Questions != null && request.Questions.Any())
            {
                foreach (var questionRequest in request.Questions)
                {
                    // Validate question exists
                    if (!await _examsRepository.QuestionExistsAsync(questionRequest.QuestionId))
                    {
                        throw new ArgumentException($"Question với ID {questionRequest.QuestionId} không tồn tại");
                    }

                    var examQuestion = new ExamQuestion
                    {
                        ExamId = createdExam.ExamId,
                        QuestionId = questionRequest.QuestionId,
                        SequenceIndex = questionRequest.SequenceIndex,
                        Marks = questionRequest.Marks,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _examsRepository.AddQuestionToExamAsync(examQuestion);
                }
            }

            // Return the created exam with basic details (avoiding complex query for now)
            return new ExamDetailDto
            {
                Id = createdExam.ExamId,
                Title = createdExam.Title,
                Description = createdExam.Description,
                CourseId = createdExam.CourseId,
                DurationMinutes = createdExam.DurationMinutes,
                TotalQuestions = createdExam.TotalQuestions,
                TotalMarks = createdExam.TotalMarks,
                PassingMark = createdExam.PassingMark,
                ExamType = createdExam.ExamType,
                StartAt = createdExam.StartAt,
                EndAt = createdExam.EndAt,
                RandomizeQuestions = createdExam.RandomizeQuestions,
                AllowMultipleAttempts = createdExam.AllowMultipleAttempts,
                Status = createdExam.Status,
                CreatedAt = createdExam.CreatedAt,
                UpdatedAt = createdExam.UpdatedAt,
                Questions = new List<ExamQuestionDto>()
            };
        }

        public async Task<ExamDetailDto?> UpdateExamAsync(int id, UpdateExamRequest request, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(id))
            {
                return null;
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(id, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bài thi này");
            }

            // Validate course exists if provided
            if (request.CourseId.HasValue && !await _examsRepository.CourseExistsAsync(request.CourseId.Value))
            {
                throw new ArgumentException("Course không tồn tại");
            }

            var exam = new Exam
            {
                Title = request.Title,
                Description = request.Description,
                CourseId = request.CourseId ?? 0, // This will be handled in repository
                DurationMinutes = request.DurationMinutes ?? 0,
                TotalQuestions = request.TotalQuestions ?? 0,
                TotalMarks = request.TotalMarks ?? 0,
                PassingMark = request.PassingMark ?? 0,
                ExamType = request.ExamType,
                StartAt = request.StartAt,
                EndAt = request.EndAt,
                RandomizeQuestions = request.RandomizeQuestions ?? false,
                AllowMultipleAttempts = request.AllowMultipleAttempts ?? false,
                Status = request.Status ?? "Draft"
            };

            var updatedExam = await _examsRepository.UpdateAsync(id, exam);
            if (updatedExam == null)
            {
                return null;
            }

            return await _examsRepository.GetByIdAsync(id);
        }

        public async Task<bool> DeleteExamAsync(int id, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(id))
            {
                return false;
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(id, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa bài thi này");
            }

            return await _examsRepository.DeleteAsync(id);
        }

        public async Task<bool> AddQuestionToExamAsync(int examId, CreateExamQuestionRequest request, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(examId))
            {
                throw new ArgumentException("Bài thi không tồn tại");
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(examId, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bài thi này");
            }

            // Check if question exists
            if (!await _examsRepository.QuestionExistsAsync(request.QuestionId))
            {
                throw new ArgumentException("Câu hỏi không tồn tại");
            }

            var examQuestion = new ExamQuestion
            {
                ExamId = examId,
                QuestionId = request.QuestionId,
                SequenceIndex = request.SequenceIndex,
                Marks = request.Marks,
                CreatedAt = DateTime.UtcNow
            };

            await _examsRepository.AddQuestionToExamAsync(examQuestion);
            return true;
        }

        public async Task<bool> RemoveQuestionFromExamAsync(int examId, int questionId, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(examId))
            {
                throw new ArgumentException("Bài thi không tồn tại");
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(examId, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bài thi này");
            }

            return await _examsRepository.RemoveQuestionFromExamAsync(examId, questionId);
        }
    }
}