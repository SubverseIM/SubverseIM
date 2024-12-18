using SubverseIM.Models;
using SubverseIM.ViewModels.Components;
using System.Collections.ObjectModel;

namespace SubverseIM.ViewModels
{
    public interface IContactContainer
    {
        ObservableCollection<ContactViewModel> ContactsList { get; }
    }
}
