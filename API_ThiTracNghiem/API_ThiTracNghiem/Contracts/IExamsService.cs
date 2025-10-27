using API_ThiTracNghiem.Models;

namespace API_ThiTracNghiem.Contracts
{
    public interface IExamsService
    {
        Task<PagedResponse<ExamListItemDto>> GetExamsAsync(int pageIndex, int pageSize, int? courseId = null, int? teacherId = null, int? subjectId = null);
        Task<ExamDetailDto?> GetExamByIdAsync(int id);
        Task<ExamDetailDto> CreateExamAsync(CreateExamRequest request, int createdBy);
        Task<ExamDetailDto?> UpdateExamAsync(int id, UpdateExamRequest request, int userId);
        Task<bool> DeleteExamAsync(int id, int userId);
        Task<bool> AddQuestionToExamAsync(int examId, CreateExamQuestionRequest request, int userId);
        Task<bool> RemoveQuestionFromExamAsync(int examId, int questionId, int userId);
    }
}