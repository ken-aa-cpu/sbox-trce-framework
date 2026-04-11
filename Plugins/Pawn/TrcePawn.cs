using Sandbox;
using System;

namespace Trce.Kernel.Plugin.Pawn

{
/// <summary>
/// Base TrcePawn component for player and NPC pawns.
/// Manages visual representation and animation parameters.
/// </summary>
[Icon( "person_pin" )]
public class TrcePawn : Component
{
/// <summary>Owner identifier (SteamId for players, 0 for NPCs).</summary>
[Sync] public ulong OwnerId { get; set; }
/// <summary>Display name of the pawn.</summary>
[Sync] public string DisplayName { get; set; }
/// <summary>Reference to the skinned model renderer.</summary>
[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
/// <summary>Path to the model asset.</summary>
[Sync] public string ModelPath { get; set; }
protected override void OnStart()
{
UpdateModel();
}

public void UpdateModel()
{
if ( BodyRenderer == null || string.IsNullOrEmpty( ModelPath ) ) return;
BodyRenderer.Model = Model.Load( ModelPath );
}

/// <summary>
/// Sets a float animation parameter on the body renderer.
/// </summary>
public void SetAnimParameter( string name, float value )
{
if ( BodyRenderer != null )
BodyRenderer.Set( name, value );
}

public void SetAnimParameter( string name, bool value )
{
if ( BodyRenderer != null )
BodyRenderer.Set( name, value );
}

}

}