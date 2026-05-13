using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Auth;
using Trce.Plugins.Social;

namespace Trce.Kernel.Command
{
	/// <summary>
	///   TRCE Command Manager: Globally handles chat-based commands (GameObjectSystem).
	/// </summary>
	public class TrceCommandManager : GameObjectSystem, ISceneStartup
	{
		public static TrceCommandManager Instance { get; private set; }

		public class CommandInfo
		{
			public string Name { get; set; }
			public string Description { get; set; }
			public int RequiredWeight { get; set; } = 0;
			public string PermissionNode { get; set; }
			public Action<ulong, string[]> Handler { get; set; }
			public List<string> Args { get; set; } = new List<string>();
		}

		private readonly Dictionary<string, CommandInfo> _commands = new Dictionary<string, CommandInfo>();

		public TrceCommandManager( Scene scene ) : base( scene )
		{
			Instance = this;
		}

		public void OnSceneStartup()
		{
			RegisterBuiltins();
			Log.Info( "[Command] TrceCommandManager initialized." );
		}

		public void Register( CommandInfo info )
		{
			if ( string.IsNullOrEmpty( info.Name ) ) return;
			_commands[info.Name.ToLower()] = info;
			Log.Info( $"[Command] Registered: /{info.Name}" );
		}

		public void Unregister( string name )
		{
			if ( string.IsNullOrEmpty( name ) ) return;
			_commands.Remove( name.ToLower() );
		}

		public bool Execute( ulong steamId, string input )
		{
			if ( string.IsNullOrEmpty( input ) || !input.StartsWith( "/" ) ) return false;

			var parts = input.Substring( 1 ).Split( ' ', StringSplitOptions.RemoveEmptyEntries );
			if ( parts.Length == 0 ) return false;

			var cmdName = parts[0].ToLower();
			if ( !_commands.TryGetValue( cmdName, out var cmd ) ) return false;

			var args = parts.Skip( 1 ).ToArray();

			// Permission check — honours both RequiredWeight (numeric role level) and PermissionNode (named ACL)
			if ( cmd.RequiredWeight > 0 || cmd.PermissionNode != null )
			{
				var auth = TrceAuthService.Instance;
				if ( auth == null )
				{
					Log.Warning( $"[Command] /{cmdName}: TrceAuthService is not ready. Rejecting command that requires authorization." );
					return false;
				}

				if ( cmd.RequiredWeight > 0 && auth.GetWeight( steamId ) < cmd.RequiredWeight )
				{
					Log.Warning( $"[Command] /{cmdName}: Player {steamId} has insufficient weight (required: {cmd.RequiredWeight}, current: {auth.GetWeight( steamId )})." );
					return false;
				}

				if ( cmd.PermissionNode != null && !auth.HasPermission( steamId, cmd.PermissionNode ) )
				{
					Log.Warning( $"[Command] /{cmdName}: Player {steamId} is missing the required permission node '{cmd.PermissionNode}'." );
					return false;
				}
			}

			try
			{
				cmd.Handler?.Invoke( steamId, args );
			}
			catch ( Exception ex )
			{
				Log.Error( $"[Command] Execution error (/{cmdName}): {ex.Message}" );
			}

			return true;
		}

		private void RegisterBuiltins()
		{
			Register( new CommandInfo
			{
				Name = "help",
				Description = "Show available commands",
				Handler = ( steamId, args ) =>
				{
					// Using local discovery for ChatManager since it's a UI component
					var chat = Scene.GetAllComponents<ChatManager>().FirstOrDefault();
					if ( chat == null ) return;

					chat.SendSystemMessage( "&e--- Available Commands ---" );
					foreach ( var c in _commands.Values )
					{
						chat.SendSystemMessage( $"&a/{c.Name} &f- {c.Description}" );
					}
				}
			} );
		}
	}
}
