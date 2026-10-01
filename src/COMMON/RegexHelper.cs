using System;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace COMMON;

public class RegexHelper
{
	public static bool IsEmail(string email)
	{
		try
		{
			new MailAddress(email);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	public static bool IsLatinString(string str)
	{
		return Regex.IsMatch(str, "^[a-zA-Z0-9-]+$");
	}

	public static bool IsUrl(string addressString)
	{
		Uri result = null;
		return Uri.TryCreate(addressString, UriKind.RelativeOrAbsolute, out result);
	}

	public static bool IsLocalString(string str)
	{
		if (str.Length < 4)
		{
			return false;
		}
		if (!str.Substring(0, 3).Equals("ls_"))
		{
			return false;
		}
		return Regex.IsMatch(str, "^[a-zA-Z0-9_]+$");
	}

	public static bool IsPhoneNumber(string phone, out string phoneNumber)
	{
		phoneNumber = string.Empty;
		if (phone.StartsWith("1") && phone.Length == 11)
		{
			phoneNumber = "+86" + phone;
			return true;
		}
		if (phone.StartsWith("7") && phone.Length == 10)
		{
			phoneNumber = "+7" + phone;
			return true;
		}
		if (phone.StartsWith("77") && phone.Length == 11)
		{
			phoneNumber = "+" + phone;
			return true;
		}
		if (phone.StartsWith("87") && phone.Length == 11)
		{
			phoneNumber = "+7" + phone.Substring(1);
			return true;
		}
		if ((phone.StartsWith("+861") && phone.Length == 14) || (phone.StartsWith("+77") && phone.Length == 12))
		{
			phoneNumber = phone;
			return true;
		}
		return false;
	}

	public static bool IsHexColorString(string str)
	{
		return Regex.IsMatch(str, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");
	}

	public static string RemoveEmoji(string str)
	{
		return Regex.Replace(str, "[\\u2600-\\u27BF\\uE000-\\uF8FF\\uD83C-\\uDBFF\\uDC00-\\uDFFF]", "");
	}
}
