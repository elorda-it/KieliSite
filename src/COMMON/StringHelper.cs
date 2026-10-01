using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace COMMON;

public class StringHelper
{
	public static string Kaz2LatForURL(string cyrlText)
	{
		char[] Tmp = cyrlText.Trim().ToLower().ToCharArray();
		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < Tmp.Length; i++)
		{
			switch (Tmp[i])
			{
			case 'ю':
				sb.Append("iu");
				continue;
			case 'я':
				sb.Append("ia");
				continue;
			case 'ё':
				sb.Append("io");
				continue;
			case 'э':
				sb.Append("e");
				continue;
			case 'с':
			case 'ц':
				sb.Append("s");
				continue;
			case 'м':
				sb.Append("m");
				continue;
			case 'и':
			case 'й':
			case 'і':
				sb.Append("i");
				continue;
			case 'т':
				sb.Append("t");
				continue;
			case 'б':
				sb.Append("b");
				continue;
			case 'ф':
				sb.Append("f");
				continue;
			case 'ы':
				sb.Append("y");
				continue;
			case 'в':
				sb.Append("v");
				continue;
			case 'а':
			case 'ә':
				sb.Append("a");
				continue;
			case 'п':
				sb.Append("p");
				continue;
			case 'р':
				sb.Append("r");
				continue;
			case 'о':
			case 'ө':
				sb.Append("o");
				continue;
			case 'л':
				sb.Append("l");
				continue;
			case 'д':
				sb.Append("d");
				continue;
			case 'ж':
				sb.Append("j");
				continue;
			case 'у':
			case 'ү':
			case 'ұ':
				sb.Append("u");
				continue;
			case 'к':
				sb.Append("k");
				continue;
			case 'е':
				sb.Append("e");
				continue;
			case 'н':
				sb.Append("n");
				continue;
			case 'г':
				sb.Append("g");
				continue;
			case 'ш':
			case 'щ':
				sb.Append("sh");
				continue;
			case 'з':
				sb.Append("z");
				continue;
			case 'х':
			case 'һ':
				sb.Append("h");
				continue;
			case 'ң':
				sb.Append("n");
				continue;
			case 'ғ':
				sb.Append("g");
				continue;
			case 'қ':
				sb.Append("q");
				continue;
			case 'ч':
				sb.Append("ch");
				continue;
			case ' ':
			case '-':
				sb.Append("-");
				continue;
			}
			if (Tmp[i] > '`' && Tmp[i] < '{')
			{
				sb.Append(Tmp[i]);
			}
			else if (Tmp[i] > '/' && Tmp[i] < ':')
			{
				sb.Append(Tmp[i]);
			}
			else
			{
				sb.Append("");
			}
		}
		return Regex.Replace(sb.ToString(), "\\-+", "-");
	}

	public static string SymbolReplace(string str)
	{
		if (!string.IsNullOrEmpty(str))
		{
			return str.Replace("<<", "«").Replace("<", "«").Replace(">>", "»")
				.Replace(">", "»");
		}
		return str;
	}

	public static string GetSubText(string text, int length)
	{
		if (text.Length <= length)
		{
			return text;
		}
		text = text.Substring(0, length - 3);
		int lastWhitespaceIndex = text.LastIndexOf(" ", StringComparison.Ordinal);
		if (lastWhitespaceIndex > 0)
		{
			text = text.Substring(0, lastWhitespaceIndex);
		}
		if (new string[9] { ",", "?", "!", ":", ".", " ", "\"", "%", "'" }.Any((string x) => x.Equals(text[text.Length - 1])))
		{
			text = text.Substring(0, text.Length - 2);
		}
		return text;
	}
}
