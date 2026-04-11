using System.Threading.Tasks;
using Sandbox;
using System.Collections.Generic;
using System.Linq;
using Trce.Kernel.Plugin.Services;
using Trce.Kernel.Bridge;

namespace Trce.Kernel.Plugin.Pawn
{
	/// <summary>
	///   / TRCE Entity and Body Management Plugin
	/// </summary>
	[TrcePlugin(
		Id = "trce.pawn",
		Name = "TRCE Pawn Management",
		Version = "1.0.0",
		Author = "TRCE Team"
	)]
	public class PawnPlugin : TrcePlugin, IPawnService
	{
		private static PawnPlugin instance;
		public static PawnPlugin Instance => instance;

		private readonly Dictionary<ulong, GameObject> playerPawns = new();
		private readonly Dictionary<string, GameObject> npcPawns = new();

		[Property] public string DefaultPlayerModel { get; set; } = "models/citizen/citizen.vmdl";

		protected override async Task OnPluginEnabled()
		{
			instance = this;
			Log.Info( "[PawnPlugin] Pawn management service started." );
		}

		public GameObject SpawnPlayerPawn( ulong steamId, string prefabPath = null )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return null;
			RemovePawn( steamId );

			var pawnGo = new GameObject( true, $"PlayerPawn_{steamId}" );
			var connection = Connection.All.FirstOrDefault( c => c.SteamId == steamId );
			if ( connection != null ) pawnGo.NetworkSpawn( connection );
			else pawnGo.NetworkSpawn();

			var pawn = pawnGo.Components.Create<Trce.Kernel.Plugin.Pawn.TrcePawn>();
			pawn.OwnerId = steamId;
			pawn.ModelPath = DefaultPlayerModel;
			pawn.BodyRenderer = pawnGo.Components.Create<SkinnedModelRenderer>();

			pawnGo.Components.Create<CharacterController>();
			var controller = pawnGo.Components.Create<Trce.Kernel.Plugin.Pawn.PawnController>();

			playerPawns[steamId] = pawnGo;
			return pawnGo;
		}

		public GameObject SpawnNpcPawn( string npcId, string prefabPath, Vector3 spawnPos )
		{
			if ( !(SandboxBridge.Instance?.IsServer ?? false) ) return null;
			var pawnGo = new GameObject( true, $"NPC_{npcId}" );
			pawnGo.WorldPosition = spawnPos;
			pawnGo.NetworkSpawn();

			var pawn = pawnGo.Components.Create<Trce.Kernel.Plugin.Pawn.TrcePawn>();
			pawn.ModelPath = DefaultPlayerModel;
			pawn.BodyRenderer = pawnGo.Components.Create<SkinnedModelRenderer>();

			npcPawns[npcId] = pawnGo;
			return pawnGo;
		}

		public GameObject GetPlayerPawn( ulong steamId ) => playerPawns.TryGetValue( steamId, out var go ) ? go : null;

		public void RemovePawn( ulong steamId )
		{
			if ( playerPawns.TryGetValue( steamId, out var go ) )
			{
				go.Destroy();
				playerPawns.Remove( steamId );
			}
		}

		public IEnumerable<GameObject> GetAllPawns() => playerPawns.Values.Concat( npcPawns.Values );

		public void SetModel( GameObject pawnGo, string modelPath )
		{
			var pawn = pawnGo.Components.Get<Trce.Kernel.Plugin.Pawn.TrcePawn>();
			if ( pawn != null )
			{
				pawn.ModelPath = modelPath;
				pawn.UpdateModel();
			}
		}
	}
}


