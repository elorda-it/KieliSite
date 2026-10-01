namespace COMMON.Extensions;

public static class ObjectExtension
{
	public static T Clone<T>(this T source)
	{
		return JsonHelper.DeserializeObject<T>(JsonHelper.SerializeObject(source));
	}
}
