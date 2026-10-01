using System;
using System.Globalization;

namespace COMMON;

public static class UnixTimeHelper
{
	public static int GetCurrentUnixTime()
	{
		return ConvertToUnixTime(DateTime.Now);
	}

	public static int ConvertToUnixTime(DateTime datetime)
	{
		return (int)datetime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
	}

	public static int ConvertToUnixTime(string datetimeStr)
	{
		if (DateTime.TryParse(datetimeStr, out var datetime))
		{
			return ConvertToUnixTime(datetime);
		}
		return 0;
	}

	public static string UnixTimeToString(int unixTime)
	{
		return UnixTimeToString(UnixTimeToDateTime(unixTime));
	}

	public static string UnixTimeToString(DateTime dateTime)
	{
		return dateTime.ToString("dd/MM/yyyy HH:mm");
	}

	public static DateTime UnixTimeToDateTime(int unixtime)
	{
		return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixtime);
	}

	public static string UnixTimeToStringFromNow(int unixtime, string language = "kz")
	{
		DateTime time = UnixTimeToDateTime(unixtime);
		TimeSpan span = DateTime.Now - time;
		string text = language.ToLower();
		if (!(text == "ru"))
		{
			if (!(text == "kz"))
			{
			}
			return ExecuteToStringFromNow(span);
		}
		if (span.TotalDays > 365.0)
		{
			int res = (int)(span.TotalDays / 365.0);
			if (res != 1)
			{
				return $"{res} года назад";
			}
			return $"{res} год назад";
		}
		if (span.TotalDays > 30.0)
		{
			int res2 = (int)(span.TotalDays / 30.0);
			if (res2 != 1)
			{
				if (res2 >= 5)
				{
					return $"{res2} месяцев назад";
				}
				return $"{res2} месяца назад";
			}
			return $"{res2} месяц назад";
		}
		if (span.TotalDays > 7.0)
		{
			int res3 = (int)(span.TotalDays / 7.0);
			if (res3 != 1)
			{
				if (res3 >= 5)
				{
					return $"{res3} недель назад";
				}
				return $"{res3} недели назад";
			}
			return $"{res3} неделю назад";
		}
		if (span.TotalDays > 1.0)
		{
			int res4 = (int)span.TotalDays;
			if (res4 > 20)
			{
				res4 %= 10;
				if (res4 != 1)
				{
					if (res4 >= 5)
					{
						return $"{res4} дней назад";
					}
					return $"{res4} дня назад";
				}
				return $"{res4} день назад";
			}
			if (res4 != 1)
			{
				if (res4 >= 5)
				{
					return $"{res4} дней назад";
				}
				return $"{res4} дня назад";
			}
			return $"{res4} день назад";
		}
		if (span.TotalHours > 1.0)
		{
			int res5 = (int)span.TotalHours;
			if (res5 > 20)
			{
				res5 %= 10;
				if (res5 != 1)
				{
					if (res5 >= 5)
					{
						return $"{res5} часов назад";
					}
					return $"{res5} часа назад";
				}
				return $"{res5} час назад";
			}
			if (res5 != 1)
			{
				if (res5 >= 5)
				{
					return $"{res5} часов назад";
				}
				return $"{res5} часа назад";
			}
			return $"{res5} час назад";
		}
		if (span.TotalMinutes > 1.0)
		{
			int res6 = (int)span.TotalMinutes;
			if (res6 > 20)
			{
				res6 %= 10;
				if (res6 != 1)
				{
					if (res6 >= 5)
					{
						return $"{res6} минут назад";
					}
					return $"{res6} минуты назад";
				}
				return $"{res6} минуту назад";
			}
			if (res6 != 1)
			{
				if (res6 >= 5)
				{
					return $"{res6} минут назад";
				}
				return $"{res6} минуты назад";
			}
			return $"{res6} минуту назад";
		}
		if (span.TotalSeconds > 1.0)
		{
			int res7 = (int)span.TotalSeconds;
			if (res7 > 20)
			{
				res7 %= 10;
				if (res7 != 1)
				{
					if (res7 >= 5)
					{
						return $"{res7} секунд назад";
					}
					return $"{res7} секунды назад";
				}
				return $"{res7} секунду назад";
			}
			if (res7 != 1)
			{
				if (res7 >= 5)
				{
					return $"{res7} секунд назад";
				}
				return $"{res7} секунды назад";
			}
			return $"{res7} секунду назад";
		}
		return "1 секунд бұрын";
	}

	private static string ExecuteToStringFromNow(TimeSpan span)
	{
		if (span.TotalDays > 365.0)
		{
			return $"{(int)(Math.Floor(span.TotalDays) / 365.0)} жыл бұрын";
		}
		if (span.TotalDays > 30.0)
		{
			return $"{(int)(Math.Floor(span.TotalDays) / 30.0)} ай бұрын";
		}
		if (span.TotalDays > 7.0)
		{
			return $"{(int)(Math.Floor(span.TotalDays) / 7.0)} апта бұрын";
		}
		if (span.TotalDays > 1.0)
		{
			return $"{(int)Math.Floor(span.TotalDays)} күн бұрын";
		}
		if (span.TotalHours > 1.0)
		{
			return $"{(int)Math.Floor(span.TotalHours)} сағат бұрын";
		}
		if (span.TotalMinutes > 1.0)
		{
			return $"{(int)Math.Floor(span.TotalMinutes)} минут бұрын";
		}
		if (span.TotalSeconds >= 1.0)
		{
			return $"{(int)Math.Floor(span.TotalSeconds)} секунд бұрын";
		}
		return "1 секунд бұрын";
	}

	public static string GetTime(int unixtime)
	{
		return UnixTimeToDateTime(unixtime).ToString("HH:mm");
	}

	public static string AstanaUnixTimeToString(int unixtime)
	{
		DateTime astanaTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixtime);
		return DateTime.SpecifyKind(astanaTime, DateTimeKind.Local).ToString("o");
	}

	public static string UnixTimeToLocalString(int unixtime, string language)
	{
		DateTime datetime = UnixTimeToDateTime(unixtime);
		switch (language)
		{
		case "kz":
		case "tote":
		case "latyn":
		{
			string month = datetime.ToString("dd MMM yyyy HH:mm", new CultureInfo("kk-KZ"));
			if (!(language == "tote"))
			{
				if (language == "latyn")
				{
					return Cyrl2LatynHelper.Cyrl2Latyn(month);
				}
				return month;
			}
			return Cyrl2ToteHelper.Cyrl2Tote(month);
		}
		case "ru":
			return datetime.ToString("dd MMM yyyy HH:mm", new CultureInfo("ru-RU"));
		case "zh-cn":
			return datetime.ToString("dd MMM yyyy HH:mm", new CultureInfo("zh-CN"));
		case "tr":
			return datetime.ToString("dd MMM yyyy HH:mm", new CultureInfo("tr-TR"));
		default:
			return datetime.ToString("dd MMM yyyy HH:mm", new CultureInfo("en-US"));
		}
	}

	public static string GetTodayLocalString(string language)
	{
		DateTime date = DateTime.Now;
		switch (language)
		{
		case "kz":
		case "tote":
		case "latyn":
		{
			string res = date.ToString("dddd dd MMMM yyyy", new CultureInfo("kk-KZ"));
			if (!(language == "tote"))
			{
				if (language == "latyn")
				{
					return Cyrl2LatynHelper.Cyrl2Latyn(res);
				}
				return res;
			}
			return Cyrl2ToteHelper.Cyrl2Tote(res);
		}
		case "ru":
			return date.ToString("dddd dd MMMM yyyy", new CultureInfo("ru-RU"));
		case "zh-cn":
			return date.ToString("dddd dd MMMM yyyy", new CultureInfo("zh-CN"));
		case "tr":
			return date.ToString("dddd dd MMMM yyyy", new CultureInfo("tr-TR"));
		default:
			return date.ToString("dddd dd MMMM yyyy", new CultureInfo("en-US"));
		}
	}
}
