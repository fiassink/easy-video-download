using System;
using System.Globalization;

namespace Launcher;

internal static class UiText
{
	private static readonly bool Portuguese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase);

	public static string Get(string portuguese, string english) => Portuguese ? portuguese : english;
}
