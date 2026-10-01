using System;

namespace COMMON.Extensions;

public static class StringExtension
{
	public static string CapFirst(this string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return input;
		}
		return char.ToUpper(input[0]) + input.Substring(1);
	}

	public static string ConvertImgSize(this string input, ImgSize imgSize)
	{
		return ImgSize.ConvertImgSize(input, imgSize);
	}

	public static bool Similar(this string input, string value)
	{
		return input.Equals(value, StringComparison.OrdinalIgnoreCase);
	}
}
