using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace COMMON;

public class Cyrl2ToteHelper
{
	private enum Sound
	{
		Vowel,
		Consonant,
		Unknown
	}

	private static readonly string[] cyrlChars = new string[45]
	{
		"А", "Ә", "Ə", "Б", "В", "Г", "Ғ", "Д", "Е", "Ё",
		"Ж", "З", "И", "Й", "К", "Қ", "Л", "М", "Н", "Ң",
		"О", "Ө", "Ɵ", "П", "Р", "С", "Т", "У", "Ұ", "Ү",
		"Ф", "Х", "Һ", "Ц", "Ч", "Ш", "Щ", "Ъ", "Ы", "І",
		"Ь", "Э", "Ю", "Я", "-"
	};

	private static Dictionary<string, string> dialectWordsDic = new Dictionary<string, string>
	{
		{ "قر", "ق ر" },
		{ "جحر", "ج ح ر" },
		{ "جشس", "ج ش س" },
		{ "شۇار", "ش ۇ ا ر" },
		{ "باق", "ب ا ق" },
		{ "ءباسپاسوز", "باسپا ءسوز" },
		{ "قىتاي", "جۇڭگو" }
	};

	public static string Cyrl2Tote(string cyrlText)
	{
		cyrlText = CopycatCyrlToOriginalCyrl(cyrlText);
		cyrlText += ".";
		cyrlText = WebUtility.HtmlDecode(cyrlText);
		string[] chars = (from x in cyrlText.ToCharArray()
			select x.ToString()).ToArray();
		int length = chars.Length;
		string[] toteStrs = new string[length];
		Sound prevSound = Sound.Unknown;
		string cyrlWord = string.Empty;
		for (int i = 0; i < length; i++)
		{
			if (!cyrlChars.Contains(chars[i].ToUpper()))
			{
				if (!string.IsNullOrEmpty(cyrlWord))
				{
					int wordLength = cyrlWord.Length;
					string[] toteChars = new string[wordLength];
					int j = i - wordLength;
					for (int tIndex = 0; j < i; j++, tIndex++)
					{
						if (j + 1 < length)
						{
							switch ((chars[j] + chars[j + 1]).ToLower())
							{
							case "ия":
								toteChars[tIndex] = "يا";
								j++;
								continue;
							case "йя":
								toteChars[tIndex] = "ييا";
								j++;
								continue;
							case "ию":
								toteChars[tIndex] = "يۋ";
								j++;
								continue;
							case "йю":
								toteChars[tIndex] = "يۋ";
								j++;
								continue;
							case "сц":
								toteChars[tIndex] = "س";
								j++;
								continue;
							case "тч":
								toteChars[tIndex] = "چ";
								j++;
								continue;
							case "ии\u0306":
								toteChars[tIndex] = "ي";
								j++;
								continue;
							case "ХХ":
								toteChars[tIndex] = "ХХ";
								j++;
								continue;
							}
						}
						string text = chars[j].ToLower();
						if (text != null)
						{
							int length2 = text.Length;
							if (length2 == 1)
							{
								switch (text[0])
								{
								case 'я':
									toteChars[tIndex] = ((prevSound == Sound.Consonant) ? "ءا" : "يا");
									continue;
								case 'ю':
									toteChars[tIndex] = ((prevSound == Sound.Consonant) ? "ءۇ" : "يۋ");
									continue;
								case 'щ':
									toteChars[tIndex] = "شش";
									continue;
								case 'э':
									toteChars[tIndex] = "ە";
									continue;
								case 'а':
									toteChars[tIndex] = "ا";
									continue;
								case 'б':
									toteChars[tIndex] = "ب";
									continue;
								case 'ц':
									toteChars[tIndex] = "س";
									continue;
								case 'д':
									toteChars[tIndex] = "د";
									continue;
								case 'е':
									toteChars[tIndex] = "ە";
									continue;
								case 'ф':
									toteChars[tIndex] = "ف";
									continue;
								case 'г':
									toteChars[tIndex] = "گ";
									continue;
								case 'х':
									toteChars[tIndex] = "ح";
									continue;
								case 'Һ':
								case 'һ':
									toteChars[tIndex] = "ھ";
									continue;
								case 'І':
								case 'і':
									toteChars[tIndex] = "ءى";
									continue;
								case 'и':
								case 'й':
									toteChars[tIndex] = "ي";
									continue;
								case 'к':
									toteChars[tIndex] = "ك";
									continue;
								case 'л':
									toteChars[tIndex] = "ل";
									continue;
								case 'м':
									toteChars[tIndex] = "م";
									continue;
								case 'н':
									toteChars[tIndex] = "ن";
									continue;
								case 'о':
									toteChars[tIndex] = "و";
									continue;
								case 'п':
									toteChars[tIndex] = "پ";
									continue;
								case 'қ':
									toteChars[tIndex] = "ق";
									continue;
								case 'р':
									toteChars[tIndex] = "ر";
									continue;
								case 'с':
									toteChars[tIndex] = "س";
									continue;
								case 'т':
									toteChars[tIndex] = "ت";
									continue;
								case 'ұ':
									toteChars[tIndex] = "ۇ";
									continue;
								case 'в':
									toteChars[tIndex] = "ۆ";
									continue;
								case 'у':
									toteChars[tIndex] = "ۋ";
									continue;
								case 'ы':
									toteChars[tIndex] = "ى";
									continue;
								case 'з':
									toteChars[tIndex] = "ز";
									continue;
								case 'ә':
									toteChars[tIndex] = "ءا";
									continue;
								case 'ё':
								case 'ө':
									toteChars[tIndex] = "ءو";
									continue;
								case 'ү':
									toteChars[tIndex] = "ءۇ";
									continue;
								case 'ч':
									toteChars[tIndex] = "چ";
									continue;
								case 'ғ':
									toteChars[tIndex] = "ع";
									continue;
								case 'ш':
									toteChars[tIndex] = "ش";
									continue;
								case 'ж':
									toteChars[tIndex] = "ج";
									continue;
								case 'ң':
									toteChars[tIndex] = "ڭ";
									continue;
								case 'ь':
									toteChars[tIndex] = "";
									continue;
								case 'Ь':
									toteChars[tIndex] = "";
									continue;
								case 'ъ':
									toteChars[tIndex] = "";
									continue;
								case 'Ъ':
									toteChars[tIndex] = "";
									continue;
								case '¬':
									toteChars[tIndex] = "";
									continue;
								}
							}
						}
						toteChars[tIndex] = ((chars[j] != "") ? chars[j] : "");
					}
					string toteWord = string.Concat(toteChars);
					if (toteWord.Contains("ء"))
					{
						toteWord = toteWord.Replace("ء", "");
						if (!toteWord.Contains("ك") && !toteWord.Contains("گ") && !toteWord.Contains("ە"))
						{
							toteWord = "ء" + toteWord;
						}
					}
					toteWord = ReplaceDialectWords(toteWord);
					toteStrs[i - wordLength] = toteWord;
					cyrlWord = string.Empty;
				}
				switch (chars[i])
				{
				case ",":
					toteStrs[i] = "،";
					break;
				case "?":
					toteStrs[i] = "؟";
					break;
				case ";":
					toteStrs[i] = "؛";
					break;
				default:
					toteStrs[i] = chars[i];
					break;
				}
				prevSound = Sound.Unknown;
			}
			else
			{
				cyrlWord += chars[i];
				prevSound = Sound.Unknown;
			}
		}
		toteStrs[length - 1] = "";
		return string.Concat(toteStrs);
	}

	private static string CopycatCyrlToOriginalCyrl(string cyrlText)
	{
		return new StringBuilder(cyrlText).Replace("Ə", "Ә").Replace("ə", "ә").Replace("Ɵ", "Ө")
			.Replace("ɵ", "ө")
			.ToString();
	}

	private static string ReplaceDialectWords(string word)
	{
		if (dialectWordsDic.ContainsKey(word))
		{
			return dialectWordsDic[word];
		}
		word = Regex.Replace(word, "\\w(ۇلى)\\s|\\w(ۇلى$)", (Match m) => string.Format("{0}", m.Groups[0].Value.Replace("ۇلى", " ۇلى")), RegexOptions.RightToLeft);
		word = Regex.Replace(word, "\\w(ۇلىنىڭ)\\s|\\w(ۇلىنىڭ$)", (Match m) => string.Format("{0}", m.Groups[0].Value.Replace("ۇلىنىڭ", " ۇلىنىڭ")), RegexOptions.RightToLeft);
		word = Regex.Replace(word, "\\w(قىزى)\\s|\\w(قىزى$)", (Match m) => string.Format("{0}", m.Groups[0].Value.Replace("قىزى", " قىزى")), RegexOptions.RightToLeft);
		word = Regex.Replace(word, "\\w(قىزىنىڭ)\\s|\\w(قىزىنىڭ$)", (Match m) => string.Format("{0}", m.Groups[0].Value.Replace("قىزىنىڭ", " قىزىنىڭ")), RegexOptions.RightToLeft);
		word = Regex.Replace(word, "\\w(ەۆ)\\s|\\w(ەۆ)", (Match m) => string.Format("{0}", m.Groups[0].Value.Replace("ەۆ", "يەۆ")), RegexOptions.RightToLeft);
		return word;
	}
}
