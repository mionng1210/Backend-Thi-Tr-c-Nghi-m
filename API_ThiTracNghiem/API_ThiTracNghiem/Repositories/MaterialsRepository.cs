using System.Linq;
using System.Threading.Tasks;
using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Models;
using Microsoft.EntityFrameworkCore;

namespace API_ThiTracNghiem.Repositories
{
    public class MaterialsRepository : IMaterialsRepository
    {
        private readonly ApplicationDbContext _db;
        public MaterialsRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<PagedResponse<MaterialListItemDto>> GetMaterialsAsync(int pageIndex, int pageSize)
        {
            if (pageIndex <= 0) pageIndex = 1;
            if (pageSize <= 0) pageSize = 10;

            var query = _db.Materials.AsNoTracking().Where(m => !m.HasDelete).OrderByDescending(m => m.CreatedAt);
            var total = await query.LongCountAsync();

            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MaterialListItemDto
                {
                    Id = m.MaterialId,
                    Title = m.Title,
                    Description = m.Description,
                    MediaType = m.MediaType,
                    IsPaid = m.IsPaid,
                    Price = m.Price,
                    ExternalLink = m.ExternalLink,
                    DurationSeconds = m.DurationSeconds,
                    CourseId = m.CourseId,
                    OrderIndex = m.OrderIndex,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt
                })
                .ToListAsync();

            return new PagedResponse<MaterialListItemDto>
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalItems = total,
                Items = items
            };
        }

        public async Task<MaterialListItemDto?> GetByIdAsync(int id)
        {
            return await _db.Materials
                .AsNoTracking()
                .Where(m => m.MaterialId == id && !m.HasDelete)
                .Select(m => new MaterialListItemDto
                {
                    Id = m.MaterialId,
                    Title = m.Title,
                    Description = m.Description,
                    MediaType = m.MediaType,
                    IsPaid = m.IsPaid,
                    Price = m.Price,
                    ExternalLink = m.ExternalLink,
                    DurationSeconds = m.DurationSeconds,
                    CourseId = m.CourseId,
                    OrderIndex = m.OrderIndex,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> CourseExistsAsync(int courseId)
        {
            return await _db.Courses.AsNoTracking().AnyAsync(c => c.CourseId == courseId && !c.HasDelete);
        }

        public async Task CreateManyAsync(List<Material> materials)
        {
            await _db.Materials.AddRangeAsync(materials);
            await _db.SaveChangesAsync();
        }

        public Task<Material?> GetEntityByIdAsync(int id)
        {
            return _db.Materials.FirstOrDefaultAsync(m => m.MaterialId == id && !m.HasDelete);
        }

        public async Task UpdateAsync(Material material)
        {
            _db.Materials.Update(material);
            await _db.SaveChangesAsync();
        }
    }
}


