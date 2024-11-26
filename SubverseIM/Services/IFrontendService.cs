using SubverseIM.Models;
using System;
using System.Collections.Generic;

namespace SubverseIM.Services
{
    public interface IFrontendService
    {
        void NavigateLaunchedUri();

        void NavigateContactView();

        void NavigateContactView(SubverseContact contact);

        void NavigateMessageView(IEnumerable<SubverseContact> contacts);
    }
}
