using Quiche.NET;

namespace SubverseIM.ViewModels;

public class MainViewModel : ViewModelBase
{
    public string Greeting => $"You're running quiche {QuicheLibrary.VersionCode}!";
}
