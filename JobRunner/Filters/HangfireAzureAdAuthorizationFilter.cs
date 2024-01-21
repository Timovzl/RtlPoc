using Hangfire.Dashboard;

namespace Rtl.News.RtlPoc.JobRunner.Filters;

/// <summary>
/// Provides <see cref="Hangfire"/> dashboard authorization using Azure AD.
/// </summary>
internal sealed class HangfireAzureAdAuthorizationFilter : IDashboardAuthorizationFilter
{
	public bool Authorize(DashboardContext context)
	{
		var httpContext = context.GetHttpContext();

		var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;

		if (!isAuthenticated && httpContext.Request.Method == "GET")
		{
			httpContext.Response.Redirect("/MicrosoftIdentity/Account/SignIn");

			// We must return true for the redirect to work, so to be safe, we only do this for GET requests
			return true;
		}

		return isAuthenticated;
	}
}
