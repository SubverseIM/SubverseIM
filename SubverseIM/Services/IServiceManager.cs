namespace SubverseIM.Services
{
    public interface IServiceManager
    {
        TService GetOrRegister<TImplementation, TService>(TImplementation? instance = null) 
            where TImplementation : class, TService, new()
            where TService : class;

        TService? GetOrRegister<TService>(TService? instance = null)
            where TService : class;
    }
}
