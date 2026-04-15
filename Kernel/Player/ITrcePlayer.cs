// File: Code/Kernel/Player/ITrcePlayer.cs
// Encoding: UTF-8 (No BOM)
//
// ── 架構規則 ──────────────────────────────────────────────────────────────
//  此介面是 Framework 與 Game 層之間的唯一邊界。
//  Framework 內的所有程式碼（UI、Plugins、Kernel）只能依賴此介面，
//  嚴禁直接引用 Trce.Game.Player.TrcePlayer。
//  Game 層的 TrcePlayer 必須實作此介面（零成本，因為這些 member 本就存在）。
// ─────────────────────────────────────────────────────────────────────────

using Sandbox;

namespace Trce.Kernel.Player
{
	/// <summary>
	/// <para>【玩家抽象介面】</para>
	/// <para>
	/// Framework 層所有需要與「玩家」互動的程式碼，
	/// 一律透過此介面存取，不直接依賴 <c>Trce.Game.Player.TrcePlayer</c>。
	/// </para>
	/// <para>
	/// 只暴露 Framework 內部實際用到的 member，保持最小介面原則。
	/// </para>
	/// </summary>
	public interface ITrcePlayer
	{
		/// <summary>玩家的 Steam ID（64 位元，跨網路唯一識別）。</summary>
		ulong SteamId { get; }

		/// <summary>玩家的顯示名稱（來自 Steam 帳號）。</summary>
		string DisplayName { get; }

		/// <summary>玩家在世界座標中的位置。</summary>
		Vector3 WorldPosition { get; }

		/// <summary>
		/// 玩家對應的 <see cref="GameObject"/>。
		/// UI 組件需要此屬性進行射線忽略（RayIgnore）等操作。
		/// </summary>
		GameObject GameObject { get; }

		/// <summary>
		/// 玩家是否仍有效（GameObject 未被銷毀）。
		/// <para>
		/// 在 s&amp;box 中，<c>Component</c> 在 <c>GameObject</c> 銷毀後仍可能被持有，
		/// 傷害計算、射線檢測等敏感路徑必須先行確認此值。
		/// </para>
		/// </summary>
		bool IsValid { get; }

		/// <summary>
		/// 向伺服器 RPC 發送聊天訊息。
		/// 只有本地擁有者（Owner）可以呼叫此方法。
		/// </summary>
		/// <param name="text">要發送的訊息內容。</param>
		void CmdSendChatMessage( string text );
	}
}
