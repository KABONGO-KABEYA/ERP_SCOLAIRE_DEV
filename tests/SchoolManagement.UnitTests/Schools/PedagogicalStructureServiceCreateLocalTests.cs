using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Application.Schools.Services;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.UnitTests.TestSupport;
using Xunit;

namespace SchoolManagement.UnitTests.Schools;

public sealed class PedagogicalStructureServiceCreateLocalTests
{
    [Fact]
    public async Task CreateLocalAsync_Humanites_enables_class_and_creates_study_option()
    {
        var schoolId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        var classes = new List<PedagogicalClass>
        {
            new()
            {
                Id = classId,
                SchoolId = schoolId,
                TemplateCode = "HUM-COM-CG-1",
                Program = SchoolProgram.Humanites,
                LevelOrder = 1,
                DisplayName = "1ère Humanité Commerciale / Commerciale et Gestion",
                HumanitiesSection = "Commerciale",
                StudyOption = "Commerciale et Gestion",
                IsEnabled = false
            }
        };
        var rooms = new List<ClassRoom>();
        var sections = new List<Section>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolId,
                Code = "HUM",
                Name = "Humanité",
                Cycle = EducationCycle.Secondaire
            }
        };
        var options = new List<StudyOption>();
        var years = new List<AcademicYear>
        {
            new()
            {
                Id = yearId,
                SchoolId = schoolId,
                Label = "2026-2027",
                IsCurrent = true
            }
        };

        var service = new PedagogicalStructureService(
            new InMemoryRepository<PedagogicalClass>(classes),
            new InMemoryRepository<ClassRoom>(rooms),
            new InMemoryRepository<Section>(sections),
            new InMemoryRepository<StudyOption>(options),
            new InMemoryRepository<AcademicYear>(years),
            new InMemoryRepository<Enrollment>([]),
            Substitute.For<ICurriculumSeedService>(),
            Substitute.For<ISectionConsolidationService>(),
            new NoOpUnitOfWork());

        var created = await service.CreateLocalAsync(
            schoolId,
            new CreateClassLocalRequest(classId, yearId, "A", 32, "RAS"));

        created.LocalName.Should().Be("A");
        created.MaxCapacity.Should().Be(32);
        classes.Single().IsEnabled.Should().BeTrue();
        rooms.Should().ContainSingle();
        options.Should().ContainSingle(o =>
            o.SchoolId == schoolId
            && o.Name == "Commerciale et Gestion"
            && o.HumanitiesSection == "Commerciale");
        rooms[0].StudyOptionId.Should().Be(options[0].Id);
        rooms[0].SectionId.Should().Be(sections[0].Id);
    }
}
