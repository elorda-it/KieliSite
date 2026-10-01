using Newtonsoft.Json;

namespace COMMON;

public class JsonHelper
{
	public static string SerializeObject(object obj)
	{
		return JsonConvert.SerializeObject(obj);
	}

	public static T DeserializeObject<T>(string str)
	{
		return JsonConvert.DeserializeObject<T>(str);
	}
}
