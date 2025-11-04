using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SubverseIM.Bootstrapper.Filters
{
    public class HostFilteringActionFilterAttribute : ActionFilterAttribute
    {
        private readonly string[] _allowedHosts;

        public HostFilteringActionFilterAttribute(string[] allowedHosts)
        {
            _allowedHosts = allowedHosts;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var host = context.HttpContext.Request.Host.Host;
            if (!_allowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new UnauthorizedResult(); // Or a custom forbidden result
            }
        }
    }
}
