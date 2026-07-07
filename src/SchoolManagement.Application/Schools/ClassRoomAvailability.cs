namespace SchoolManagement.Application.Schools;

using SchoolManagement.Domain.Entities.Settings;

public static class ClassRoomAvailability
{
    public static bool IsSelectable(ClassRoom classRoom, IReadOnlyDictionary<Guid, PedagogicalClass> pedagogicalMap)
    {
        if (!classRoom.IsActive)
        {
            return false;
        }

        if (!classRoom.PedagogicalClassId.HasValue)
        {
            return false;
        }

        return pedagogicalMap.TryGetValue(classRoom.PedagogicalClassId.Value, out var pedagogicalClass)
            && pedagogicalClass.IsEnabled;
    }

    public static IReadOnlyDictionary<Guid, PedagogicalClass> BuildMap(IEnumerable<PedagogicalClass> classes) =>
        classes.ToDictionary(c => c.Id);
}
