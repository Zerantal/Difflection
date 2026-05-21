using System.Threading;
using System.Threading.Tasks;
using Difflection.Models;

namespace Difflection.Storage;

public interface IApplicationSettingsStorage
{
    Task<ApplicationSettings> LoadApplicationSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveApplicationSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken = default);
}
