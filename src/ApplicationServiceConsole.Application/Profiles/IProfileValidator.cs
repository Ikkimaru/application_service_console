using ApplicationServiceConsole.Domain;

namespace ApplicationServiceConsole.Application;

public interface IProfileValidator
{
    IReadOnlyList<ServiceDiagnostic> Validate(ProjectProfile profile, IReadOnlyList<ResolvedService> services);
}
