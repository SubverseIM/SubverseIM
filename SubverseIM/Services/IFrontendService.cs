using SubverseIM.Models;

namespace SubverseIM.Services
{
    public interface IFrontendService
    {
        void NavigateContactView();

        void NavigateContactView(SubverseContact contact);

        void NavigateMessageView(SubverseContact contact);
    }
}
