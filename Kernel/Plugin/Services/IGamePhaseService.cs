// File: Code/Kernel/Plugin/Services/IGamePhaseService.cs
// Encoding: UTF-8 (No BOM)

using System;

namespace Trce.Kernel.Plugin.Services
{
	/// <summary>
	/// Service contract for the game phase state machine.
	/// All consumers must resolve this via GetService&lt;IGamePhaseService&gt;()
	/// instead of Scene.Get&lt;GamePhaseManager&gt;().
	/// </summary>
	public interface IGamePhaseService
	{
		/// <summary>The current game phase.</summary>
		GamePhaseEnum CurrentPhase { get; }

		/// <summary>Transitions to the specified phase.</summary>
		void SwitchPhase( GamePhaseEnum newPhase, float duration = 0f );

		/// <summary>Begins the game from the Lobby state.</summary>
		void StartGame();

		void EnterConfrontation();
		void ResumeTaskPhase();

		/// <summary>
		/// Fired when the phase changes.
		/// Parameters: (oldPhase, newPhase, phaseDuration)
		/// </summary>
		event Action<GamePhaseEnum, GamePhaseEnum, float> OnPhaseChanged;

		/// <summary>
		/// Fired when a round ends.
		/// Parameters: (winner, reason)
		/// </summary>
		event Action<string, string> OnRoundEnded;

		/// <summary>Returns the time remaining in the current timed phase, or float.MaxValue if no timer is active.</summary>
		float GetTimeRemaining();

		/// <summary>Resets the round secret for a new round.</summary>
		void ResetForNewRound();
	}

	/// <summary>
	/// Game phase enumeration — duplicated here so the Kernel layer has no dependency
	/// on Trce.Plugins.GameState. The GamePhaseManager enum mirrors these values.
	/// </summary>
	public enum GamePhaseEnum
	{
		Lobby,
		Intro,
		TaskPhase,
		Confrontation,
		ArmoryUnlocked,
		HuntPhase,
		EndRound
	}
}
