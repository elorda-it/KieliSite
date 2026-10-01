using System.Security.Principal;
using COMMON;
using COMMON.Extensions;
using Hangfire.Dashboard;

namespace KieliWeb.Filters;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
	public bool Authorize(DashboardContext context)
	{
		IIdentity identity = context.GetHttpContext().User.Identity;
		if (identity != null && identity.IsAuthenticated)
		{
			return identity.Role().Similar("Admin");
		}
		return false;
	}
}
