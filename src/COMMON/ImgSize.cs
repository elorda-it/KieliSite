using System;
using System.IO;

namespace COMMON;

public class ImgSize
{
	private const char ImgSizeSeperator = '_';

	public static ImgSize Big => new ImgSize("big");

	public static ImgSize Middle => new ImgSize("middle");

	public static ImgSize Small => new ImgSize("small");

	private string Value { get; }

	private ImgSize(string value)
	{
		Value = value;
	}

	private static bool IsImgSize(string str)
	{
		if (!Big.Equals(str) && !Middle.Equals(str))
		{
			return Small.Equals(str);
		}
		return true;
	}

	public static string ConvertImgSize(string path, ImgSize imgSize)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return string.Empty;
		}
		if (!path.Contains('_'))
		{
			return path;
		}
		string[] fileNameArr = Path.GetFileNameWithoutExtension(path).Split('_');
		if (fileNameArr.Length != 2 || !IsImgSize(fileNameArr[1]))
		{
			return path;
		}
		return PathHelper.Combine(Path.GetDirectoryName(path), fileNameArr[0] + "_" + imgSize?.ToString() + Path.GetExtension(path));
	}

	public override string ToString()
	{
		return Value;
	}

	private bool Equals(ImgSize other)
	{
		return StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);
	}

	private bool Equals(string other)
	{
		return StringComparer.OrdinalIgnoreCase.Equals(Value, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is ImgSize other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
	}

	public static bool operator ==(ImgSize left, ImgSize right)
	{
		if (!(left == null))
		{
			return left.Equals(right);
		}
		return right == null;
	}

	public static bool operator !=(ImgSize left, ImgSize right)
	{
		return !(left == right);
	}
}
