using Microsoft.Extensions.Options;
using SchoolManagement.Bootstrap.API.Options;

namespace SchoolManagement.Bootstrap.API.Services;

public sealed class SchoolRegistry
{
    private readonly BootstrapOptions _options;

    public SchoolRegistry(IOptions<BootstrapOptions> options)
    {
        _options = options.Value;
    }

    public SchoolRegistryEntryOptions Resolve(Guid schoolId)
    {
        var entry = _options.Schools.FirstOrDefault(s => s.SchoolId == schoolId);
        if (entry is null || string.IsNullOrWhiteSpace(entry.ActivationBaseUrl))
        {
            throw new InvalidOperationException(
                $"École {schoolId:D} introuvable dans le registre Bootstrap.");
        }

        return entry;
    }
}
