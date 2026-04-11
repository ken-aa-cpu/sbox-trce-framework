using System.Threading.Tasks;
// ====================================================================
// ╔══════════════════════════════════════════════════════════════════╗
// ║  TRCE FRAMEWORK — PROPRIETARY SOURCE CODE                       ║
// ║  Copyright (c) 2026 TRCE Team. All rights reserved.            ║
// ║  [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.      ║
// ╚══════════════════════════════════════════════════════════════════╝
// ====================================================================

using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Net;
using Trce.Kernel.Player;
using Trce.Kernel.Plugin.Services;
using Trce.Plugins.Combat;
using Trce.Kernel.Plugin;
using Trce.Kernel.Papi;
using Trce.Kernel.Command;
using Trce.Kernel.Auth;
using Trce.Kernel.Bridge;

namespace Trce.Plugins.Social
{
	[Title( "Chat Manager" ), Group( "Trce - Modules" ), Icon( "chat" )]
	public class ChatManager : Trce.Kernel.Plugin.TrcePlugin
	{
		private static ChatManager instance;
		public static ChatManager Instance => instance;

		protected override void OnAwake()
		{
			instance = this;
		}

		private readonly List<IChatProcessor> processors = new();
		private readonly List<IChatFilter> filters = new();
		private List<ChatMessage> history = new();

		public static readonly string[] QuickPhrases = new[]
		{
			"Follow me", "Is anyone here?", "Repairing...", "Watch out!",
			"I see something suspicious", "Group up", "I trust you", "Stop following me"
		};

		protected override async Task OnPluginEnabled()
		{
			AddProcessor( new PapiProcessor() );
			AddProcessor( new ColorAuthProcessor() );
			AddProcessor( new DefaultPrefixProcessor() );
		}

		public void AddProcessor( IChatProcessor p ) => processors.Add( p );
		public void AddFilter( IChatFilter f ) => filters.Add( f );

		public void SendMessage( ulong senderSteamId, string content )
		{
			var bridge = Scene?.Get<Trce.Kernel.Bridge.SandboxBridge>();
			if ( bridge == null || !bridge.IsServer ) return;
			if ( string.IsNullOrWhiteSpace( content ) ) return;
			if ( content.Length > 200 ) content = content.Substring( 0, 200 );

			/*
			var cmdManager = Scene?.Get<TrceCommandManager>();
			if ( cmdManager != null && cmdManager.TryExecute( senderSteamId, content, this ) )
			{
				return;
			}
			*/

			var packet = new ChatPacket
			{
				SenderSteamId = senderSteamId,
				Content = content,
				StyleId = "alive",
				Metadata = new Dictionary<string, object>()
			};

			foreach ( var p in processors )
			{
				p.Process( this, ref packet );
			}

			RpcBroadcastMessage( packet.SenderSteamId, packet.Content, packet.StyleId, Time.Now );
			Log.Info( $"[Chat:{GameObject.Name}] {packet.SenderSteamId}: {packet.Content}" );
		}

		public void SendQuickPhrase( ulong senderSteamId, int phraseIndex )
		{
			if ( phraseIndex < 0 || phraseIndex >= QuickPhrases.Length ) return;
			SendMessage( senderSteamId, QuickPhrases[phraseIndex] );
		}

		public void SendSystemMessage( string content, bool ghostOnly = false )
		{
			SendStyledMessage( 0, content, "system", ghostOnly );
		}

		public void SendStyledMessage( ulong senderSteamId, string content, string styleId, bool ghostOnly = false )
		{
			var bridge = Scene?.Get<Trce.Kernel.Bridge.SandboxBridge>();
			if ( bridge == null || !bridge.IsServer ) return;
			if ( string.IsNullOrWhiteSpace( content ) ) return;

			Log.Info( $"[Chat:{GameObject.Name}/{styleId}] {content}" );
			RpcBroadcastMessage( senderSteamId, content, styleId, Time.Now );
		}

		[Rpc.Broadcast]
		private void RpcBroadcastMessage( ulong sender, string content, string styleId, float timestamp )
		{
			var style = ChatStyleRegistry.Get( styleId );
			var msg = new ChatMessage
			{
				SenderSteamId = sender,
				Content = content,
				Timestamp = timestamp,
				StyleId = styleId,
				IsGhostMessage = style?.GhostOnly ?? false,
				Channel = styleId == "ghost" ? ChatChannel.Ghost
					: styleId == "system" ? ChatChannel.System
					: styleId == "downed" ? ChatChannel.Alive
					: ChatChannel.Alive
			};
			history.Add( msg );
		}

		public List<ChatMessage> GetRecentMessages( int count = 20, ChatChannel? channel = null )
		{
			var filtered = channel.HasValue ? history.FindAll( m => m.Channel == channel.Value ) : history;
			int start = Math.Max( 0, filtered.Count - count );
			return filtered.GetRange( start, filtered.Count - start );
		}

		public void ClearHistory() => history.Clear();
	}

	public class ChatMessage
	{
		public ulong SenderSteamId { get; set; }
		public string Content { get; set; }
		public float Timestamp { get; set; }
		public string StyleId { get; set; } = "alive";
		public bool IsGhostMessage { get; set; }
		public bool IsSystemMessage => StyleId == "system";
		public ChatChannel Channel { get; set; }
	}

	public enum ChatChannel { Alive, Ghost, System }

	public struct ChatPacket
	{
		public ulong SenderSteamId;
		public string Content;
		public string StyleId;
		public Dictionary<string, object> Metadata;
	}

	public interface IChatProcessor
	{
		void Process( ChatManager manager, ref ChatPacket packet );
	}

	public interface IChatFilter
	{
		bool CanSee( ChatManager manager, ChatPacket packet );
	}

	public class PapiProcessor : IChatProcessor
	{
		public void Process( ChatManager manager, ref ChatPacket packet )
		{
			packet.Content = Kernel.Papi.PlaceholderAPI.Replace( manager.GameObject, packet.Content );
		}
	}

	public class ColorAuthProcessor : IChatProcessor
	{
		public void Process( ChatManager manager, ref ChatPacket packet )
		{
			var auth = manager.Scene?.Get<TrceAuthService>();
			bool hasColorPerm = auth?.HasPermission( packet.SenderSteamId, "trce.chat.color" ) ?? false;
			if ( !hasColorPerm ) packet.Content = TrceColorParser.StripColors( packet.Content );
		}
	}

	public class DefaultPrefixProcessor : IChatProcessor
	{
		public void Process( ChatManager manager, ref ChatPacket packet )
		{
			var dm = manager.GetPlugin<DeathManager>();
			var state = dm?.GetState( packet.SenderSteamId ) ?? AliveState.Alive;

			string prefix = "";
			if ( state == AliveState.Downed ) prefix = "&8[&eDOWNED&8] &r";
			else if ( state == AliveState.Dead ) prefix = "&8[&7GHOST&8] &r";

			var auth = manager.Scene?.Get<TrceAuthService>();
			var session = auth?.GetSession( packet.SenderSteamId );
			if ( session?.PermissionUser != null )
			{
				if ( session.PermissionUser.Groups.Contains( "developer" ) ) prefix = "&8[&cDEV&8] " + prefix;
				else if ( session.PermissionUser.Groups.Contains( "vip" ) ) prefix = "&8[&6VIP&8] &r" + prefix;
			}

			packet.Content = prefix + packet.Content;
			if ( state == AliveState.Dead ) packet.StyleId = "ghost";
			else if ( state == AliveState.Downed ) packet.StyleId = "downed";
		}
	}
}


