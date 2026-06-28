using System.Threading.Tasks;

namespace SubverseIM.Services;

public interface IInjectable
{
    Task InjectAsync(IServiceManager serviceManager);
}
