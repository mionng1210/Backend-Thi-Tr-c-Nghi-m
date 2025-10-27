using System.Threading.Tasks;
using API_ThiTracNghiem.Contracts;

namespace API_ThiTracNghiem.Repositories
{
    public interface IMaterialsRepository
    {
        Task<PagedResponse<MaterialListItemDto>> GetMaterialsAsync(int pageIndex, int pageSize);
        Task<MaterialListItemDto?> GetByIdAsync(int id);
        Task<bool> CourseExistsAsync(int courseId);
        Task CreateManyAsync(List<API_ThiTracNghiem.Models.Material> materials);
        Task<API_ThiTracNghiem.Models.Material?> GetEntityByIdAsync(int id);
        Task UpdateAsync(API_ThiTracNghiem.Models.Material material);
    }
}


