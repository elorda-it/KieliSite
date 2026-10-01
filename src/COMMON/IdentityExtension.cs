using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;

namespace COMMON;

public static class IdentityExtension
{
	public static string RealName(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("RealName");
		if (claim == null)
		{
			return string.Empty;
		}
		return claim.Value;
	}

	public static int AdminId(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("AdminId");
		if (!int.TryParse((claim != null) ? claim.Value : string.Empty, out var adminId))
		{
			return 0;
		}
		return adminId;
	}

	public static List<int> RoleIds(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("RoleIds");
		string roleIdStrs = ((claim != null) ? claim.Value : string.Empty);
		if (!string.IsNullOrEmpty(roleIdStrs))
		{
			return roleIdStrs.Split(",").Select(int.Parse).ToList();
		}
		return new List<int>();
	}

	public static string RoleNames(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("RoleNames");
		if (claim == null)
		{
			return string.Empty;
		}
		return claim.Value;
	}

	public static string Email(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("email") ?? ((ClaimsIdentity)identity).FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
		if (claim == null)
		{
			return string.Empty;
		}
		return claim.Value;
	}

	public static bool IsSuperAdmin(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("isSuperAdmin") ?? ((ClaimsIdentity)identity).FindFirst("IsSuperAdmin");
		if (int.TryParse((claim != null) ? claim.Value : string.Empty, out var isSuperAdmin))
		{
			return isSuperAdmin == 1;
		}
		return false;
	}

	public static string Role(this IIdentity identity)
	{
		Claim roles = ((ClaimsIdentity)identity).Claims.FirstOrDefault((Claim c) => c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
		if (roles == null)
		{
			return string.Empty;
		}
		return roles.Value;
	}

	public static string AvatarUrl(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("AvatarUrl");
		if (claim == null)
		{
			return string.Empty;
		}
		return claim.Value;
	}

	public static string SkinName(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("SkinName");
		if (claim == null)
		{
			return string.Empty;
		}
		return claim.Value;
	}

	public static int LoginTime(this IIdentity identity)
	{
		Claim claim = ((ClaimsIdentity)identity).FindFirst("LoginTime");
		if (!int.TryParse((claim != null) ? claim.Value : string.Empty, out var loginTime))
		{
			return 0;
		}
		return loginTime;
	}
}
