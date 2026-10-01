using System;
using System.Collections.Generic;
using System.Linq;
using MODEL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using KieliWeb.Caches;

namespace KieliWeb.Routing;

public class CultureRouteConstraint : IRouteConstraint, IParameterPolicy
{
	public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
	{
		string culture = values[routeKey]?.ToString();
		if (string.IsNullOrWhiteSpace(culture))
		{
			return false;
		}
		IMemoryCache memoryCache = httpContext.RequestServices.GetService<IMemoryCache>();
		List<Language> languageList = QarCache.GetLanguageList(memoryCache);
		if (languageList == null || languageList.Count == 0)
		{
			if (!culture.Equals("kz", StringComparison.OrdinalIgnoreCase) && !culture.Equals("ru", StringComparison.OrdinalIgnoreCase))
			{
				return culture.Equals("en", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
		return languageList.Any((Language x) => x.LanguageCulture.Equals(culture, StringComparison.OrdinalIgnoreCase));
	}
}
