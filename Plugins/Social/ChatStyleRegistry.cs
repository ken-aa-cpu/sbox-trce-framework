using System.Collections.Generic;
using Sandbox;

namespace Trce.Plugins.Social
{
/// <summary>
/// Represents a visual style configuration for chat messages.
/// </summary>
public class ChatMessageStyle
{
/// <summary>Unique Style ID (e.g., "alive", "ghost", "system").</summary>
public string StyleId { get; set; }

/// <summary>Prefix text displayed before the name (e.g., "[SYSTEM]").</summary>
public string Prefix { get; set; } = "";

/// <summary>Color for the prefix text.</summary>
public string PrefixColor { get; set; } = "#ffffff";

/// <summary>Default color for the message text.</summary>
public string TextColor { get; set; } = "#ffffff";

/// <summary>Border color for the message box.</summary>
public string BorderColor { get; set; } = "rgba(57, 255, 130, 0.6)";

/// <summary>Background color for the message box.</summary>
public string BackgroundColor { get; set; } = "rgba(8, 12, 18, 0.75)";

/// <summary>Whether the text should be italic.</summary>
public bool Italic { get; set; } = false;

/// <summary>If true, only ghosts can see these messages.</summary>
public bool GhostOnly { get; set; } = false;
}

/// <summary>
/// Registry for chat message styles. Provides default styles and allows plugin registration.
/// </summary>
public static class ChatStyleRegistry
{
private static readonly Dictionary<string, ChatMessageStyle> styles = new();

static ChatStyleRegistry()
{
RegisterDefaults();
}

/// <summary>Register a new chat style.</summary>
public static void Register( ChatMessageStyle style )
{
if ( style == null || string.IsNullOrEmpty( style.StyleId ) ) return;
styles[style.StyleId.ToLower()] = style;
Log.Info( $"[ChatStyleRegistry] Registered style: '{style.StyleId}'" );
}

/// <summary>Get a style by ID, fall back to "alive" if not found.</summary>
public static ChatMessageStyle Get( string styleId )
{
if ( !string.IsNullOrEmpty( styleId ) && styles.TryGetValue( styleId.ToLower(), out var style ) )
return style;

return styles.TryGetValue( "alive", out var fallback ) ? fallback : new ChatMessageStyle { StyleId = "alive" };
}

/// <summary>Check if a style exists.</summary>
public static bool Has( string styleId ) =>
!string.IsNullOrEmpty( styleId ) && styles.ContainsKey( styleId.ToLower() );

/// <summary>Get all registered styles.</summary>
public static IEnumerable<ChatMessageStyle> GetAll() => styles.Values;

private static void RegisterDefaults()
{
// Default alive player style
Register( new ChatMessageStyle
{
StyleId         = "alive",
Prefix          = "",
TextColor       = "#ffffff",
BorderColor     = "rgba(57, 255, 130, 0.6)",
BackgroundColor = "rgba(8, 12, 18, 0.75)",
Italic          = false,
GhostOnly       = false
} );

// Ghost style
Register( new ChatMessageStyle
{
StyleId         = "ghost",
Prefix          = "GHOST",
PrefixColor     = "#8892a0",
TextColor       = "#a0aab8",
BorderColor     = "rgba(136, 146, 160, 0.4)",
BackgroundColor = "rgba(8, 12, 18, 0.5)",
Italic          = true,
GhostOnly       = true
} );

// System style
Register( new ChatMessageStyle
{
StyleId         = "system",
Prefix          = "SYSTEM",
PrefixColor     = "#38d8ff",
TextColor       = "#aaffcc",
BorderColor     = "rgba(57, 200, 255, 0.8)",
BackgroundColor = "rgba(10, 30, 50, 0.85)",
Italic          = false,
GhostOnly       = false
} );
}
}
}