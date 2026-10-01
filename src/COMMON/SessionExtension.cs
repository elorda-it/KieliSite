using Microsoft.AspNetCore.Http;

namespace COMMON;

public static class SessionExtension
{
	public static void Set<T>(this ISession session, string key, T value)
	{
		session.SetString(key, JsonHelper.SerializeObject(value));
	}

	public static T Get<T>(this ISession session, string key)
	{
		string value = session.GetString(key);
		if (value != null)
		{
			return JsonHelper.DeserializeObject<T>(value);
		}
		return default(T);
	}
}
