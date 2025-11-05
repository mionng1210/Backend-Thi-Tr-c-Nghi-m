using System.Net.Mime;
using FluentAssertions;
using MaterialsService.Controllers;
using MaterialsService.Data;
using MaterialsService.Integrations;
using MaterialsService.Models;
using MaterialsService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shared.Contracts.Materials;
using Xunit;

namespace MaterialsService.Tests;

public class MaterialsControllerTests
{
    private MaterialsDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<MaterialsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MaterialsDbContext(opts);
        db.Materials.AddRange(new Material
        {
            MaterialId = 1,
            CourseId = 1,
            Title = "A",
            IsPaid = false,
            HasDelete = false,
            CreatedAt = DateTime.UtcNow
        }, new Material
        {
            MaterialId = 2,
            CourseId = 1,
            Title = "B",
            IsPaid = true,
            Price = 100,
            HasDelete = false,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
        return db;
    }

    private MaterialsController CreateController(MaterialsDbContext db)
    {
        var cloud = new Mock<ICloudStorage>(MockBehavior.Strict);
        cloud.Setup(c => c.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
             .ReturnsAsync("https://cloudinary.example/video.mp4");
        var docs = new Mock<IDocumentStorage>(MockBehavior.Strict);
        docs.Setup(d => d.UploadDocumentAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("https://supabase.example/doc.pdf");

        var service = new MaterialsService.Services.MaterialsService(db, cloud.Object, docs.Object);
        return new MaterialsController(service);
    }

    [Fact]
    public async Task Get_Returns_Paged_List()
    {
        using var db = CreateDb();
        var ctrl = CreateController(db);
        var res = await ctrl.Get(pageIndex: 1, pageSize: 10) as OkObjectResult;
        res.Should().NotBeNull();
        var list = res!.Value as IEnumerable<MaterialListItemDto>;
        list!.Count().Should().Be(2);
    }

    [Fact]
    public async Task GetById_Returns_Item_And_NotFound()
    {
        using var db = CreateDb();
        var ctrl = CreateController(db);
        var ok = await ctrl.GetById(1) as OkObjectResult;
        ok.Should().NotBeNull();
        var notFound = await ctrl.GetById(999);
        notFound.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Put_Update_Succeeds()
    {
        using var db = CreateDb();
        var ctrl = CreateController(db);
        var form = new UpdateMaterialForm
        {
            Title = "Updated",
            Description = "Desc",
            IsPaid = true,
            Price = 200
        };
        var res = await ctrl.Update(1, form) as OkObjectResult;
        res.Should().NotBeNull();
        var dto = res!.Value as MaterialListItemDto;
        dto!.Title.Should().Be("Updated");
        dto.Price.Should().Be(200);
    }

    [Fact]
    public async Task Delete_SoftDelete_Succeeds()
    {
        using var db = CreateDb();
        var ctrl = CreateController(db);
        var res = await ctrl.Delete(1) as OkObjectResult;
        res.Should().NotBeNull();
        (await db.Materials.FindAsync(1))!.HasDelete.Should().BeTrue();
    }
}


