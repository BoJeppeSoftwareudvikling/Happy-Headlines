using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SubscriberService.Features;

public sealed class SubscriberReleaseToggleFilter(
    ISubscriberReleaseToggle releaseToggle) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!releaseToggle.IsEnabled)
        {
            context.Result = new NotFoundResult();
            return;
        }

        await next();
    }
}
