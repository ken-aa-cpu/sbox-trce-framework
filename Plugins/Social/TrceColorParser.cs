using Sandbox;
using System.Collections.Generic;

namespace Trce.Plugins.Social
{
	/// <summary>
	/// TRCE-View text span for UI rendering (used by Razor components).
	/// </summary>
	public struct TextSpan
	{
		public string Text { get; set; }
		public Color? Color { get; set; }
		public bool IsBold { get; set; }
	}

	/// <summary>
	/// TRCE Color Code Parser (Minecraft-style '&amp;' color codes).
	///
	/// Supported codes:
	///   &amp;0 = Black, &amp;1 = Dark Blue, &amp;2 = Dark Green, &amp;3 = Dark Aqua, &amp;4 = Dark Red, &amp;5 = Purple, &amp;6 = Gold, &amp;7 = Gray
	///   &amp;8 = Dark Gray, &amp;9 = Blue, &amp;a = Green, &amp;b = Aqua, &amp;c = Red, &amp;d = Light Purple, &amp;e = Yellow, &amp;f = White
	///   &amp;r = Reset color / style
	///   &amp;l = Bold
	/// </summary>
	public static class TrceColorParser
	{
		private static readonly Dictionary<char, Color> ColorCodes = new()
		{
			{ '0', new Color( 0f, 0f, 0f ) },       // Black
			{ '1', new Color( 0f, 0f, 0.66f ) },    // Dark Blue
			{ '2', new Color( 0f, 0.66f, 0f ) },    // Dark Green
			{ '3', new Color( 0f, 0.66f, 0.66f ) }, // Dark Aqua
			{ '4', new Color( 0.66f, 0f, 0f ) },    // Dark Red
			{ '5', new Color( 0.66f, 0f, 0.66f ) }, // Dark Purple
			{ '6', new Color( 1f, 0.66f, 0f ) },    // Gold
			{ '7', new Color( 0.66f, 0.66f, 0.66f ) }, // Gray
			{ '8', new Color( 0.33f, 0.33f, 0.33f ) }, // Dark Gray
			{ '9', new Color( 0.33f, 0.33f, 1f ) }, // Blue
			{ 'a', new Color( 0.33f, 1f, 0.33f ) }, // Green
			{ 'b', new Color( 0.33f, 1f, 1f ) },    // Aqua
			{ 'c', new Color( 1f, 0.33f, 0.33f ) }, // Red
			{ 'd', new Color( 1f, 0.33f, 1f ) },    // Light Purple
			{ 'e', new Color( 1f, 1f, 0.33f ) },    // Yellow
			{ 'f', new Color( 1f, 1f, 1f ) }        // White
		};

		/// <summary>Strips all color codes from the input string.</summary>
		public static string StripColors( string input )
		{
			if ( string.IsNullOrEmpty( input ) ) return input;

			var sb = new System.Text.StringBuilder( input.Length );
			for ( int i = 0; i < input.Length; i++ )
			{
				if ( input[i] == '&' && i + 1 < input.Length )
				{
					char next = char.ToLowerInvariant( input[i + 1] );
					if ( ColorCodes.ContainsKey( next ) || next == 'r' || next == 'l' )
					{
						i++; // Skip the color code character
						continue;
					}

				}
				sb.Append( input[i] );
			}
			return sb.ToString();
		}

		/// <summary>
		/// Parses color-coded text into a list of styled spans for UI rendering.
		/// Usage: @foreach (var span in nodes) { &lt;label style="color: @span.Color"&gt;@span.Text&lt;/label&gt; }
		/// </summary>
		public static List<TextSpan> Parse( string input, Color defaultColor )
		{
			var spans = new List<TextSpan>();
			if ( string.IsNullOrEmpty( input ) ) return spans;
			Color? currentColor = defaultColor;
			bool currentBold = false;
			var currentText = new System.Text.StringBuilder();
			for ( int i = 0; i < input.Length; i++ )
			{
				if ( input[i] == '&' && i + 1 < input.Length )
				{
					char code = char.ToLowerInvariant( input[i + 1] );

					bool isColor = ColorCodes.TryGetValue( code, out Color newColor );
					bool isReset = code == 'r';
					bool isBold = code == 'l';
					if ( isColor || isReset || isBold )
					{
						// Flush current text into a new span
						if ( currentText.Length > 0 )
						{
							spans.Add( new TextSpan { Text = currentText.ToString(), Color = currentColor, IsBold = currentBold } );
							currentText.Clear();
						}
						if ( isColor ) currentColor = newColor;
						if ( isReset ) { currentColor = defaultColor; currentBold = false; }
						if ( isBold ) currentBold = true;
						i++; // Skip the color code character
						continue;
					}

				}
				currentText.Append( input[i] );
			}
			if ( currentText.Length > 0 )
			{
				spans.Add( new TextSpan { Text = currentText.ToString(), Color = currentColor, IsBold = currentBold } );
			}
			return spans;
		}

	}

}

