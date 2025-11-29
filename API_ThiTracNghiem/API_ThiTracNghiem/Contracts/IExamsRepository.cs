using API_ThiTracNghiem.Models;

namespace API_ThiTracNghiem.Contracts
{
    public interface IExamsRepository
    {
        Task<PagedResponse<ExamListItemDto>> GetExamsAsync(int pageIndex, int pageSize, int? courseId = null, int? teacherId = null, int? subjectId = null);
        Task<ExamDetailDto?> GetByIdAsync(int id);
        Task<Exam> CreateAsync(Exam exam);
        Task<Exam?> UpdateAsync(int id, Exam exam);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExamExistsAsync(int id);
        Task<bool> CourseExistsAsync(int courseId);
        Task<bool> QuestionExistsAsync(int questionId);
        Task<List<ExamQuestion>> GetExamQuestionsAsync(int examId);
        Task<ExamQuestion> AddQuestionToExamAsync(ExamQuestion examQuestion);
        Task<bool> RemoveQuestionFromExamAsync(int examId, int questionId);
        Task<bool> IsTeacherOwnerOfExamAsync(int examId, int teacherId);
        Task<List<Question>> GetRandomQuestionsByDifficultyAsync(int examId, string difficulty, int count);
        Task<List<ExamQuestion>> GetExamQuestionsByVariantAsync(int examId, string variantCode);
        Task<ExamAttempt> CreateExamAttemptAsync(ExamAttempt examAttempt);
        Task<ExamAttempt?> GetActiveExamAttemptAsync(int examId, int userId);
        Task<List<ExamAttempt>> GetUserExamAttemptsAsync(int examId, int userId);
        Task<List<AnswerOption>> GetAnswerOptionsForQuestionAsync(int questionId);
    }
}