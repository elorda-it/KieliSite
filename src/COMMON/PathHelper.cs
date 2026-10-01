namespace COMMON;

public class PathHelper
{
	public static string Combine(string path1, string path2)
	{
		while (path1.EndsWith('/'))
		{
			string text = path1;
			path1 = text.Substring(0, text.Length - 1);
		}
		while (path2.StartsWith('/'))
		{
			string text = path2;
			path2 = text.Substring(1, text.Length - 1);
		}
		return path1 + "/" + path2;
	}

	public static string Combine(string path1, string path2, string path3)
	{
		return Combine(path1, Combine(path2, path3));
	}

	public static string Combine(string path1, string path2, string path3, string path4)
	{
		return Combine(path1, Combine(path2, path3, path4));
	}

	public static string Combine(string[] paths)
	{
		string res = paths[0];
		for (int i = 1; i < paths.Length; i++)
		{
			res = Combine(res, paths[i]);
		}
		return res;
	}
}
