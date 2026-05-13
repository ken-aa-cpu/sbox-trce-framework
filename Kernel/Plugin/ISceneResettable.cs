// File: Code/Kernel/Plugin/ISceneResettable.cs
// Encoding: UTF-8 (No BOM)

namespace Trce.Kernel.Plugin
{
	/// <summary>
	/// Marks a <b>non-static</b> class as owning state that must be cleared on every scene change.
	/// <para>
	/// <b>Usage (non-static classes):</b> Implement this interface on any class whose instance
	/// or static-adjacent state persists across s&amp;box scene loads. The framework's scene-reset
	/// pipeline will call <see cref="ResetForNewScene"/> automatically.
	/// </para>
	/// <para>
	/// <b>⚠ Limitation — static classes:</b> C# does not permit <c>static class</c> to implement
	/// any interface (<c>CS0714</c>). For purely static state holders (e.g. <c>ServerHitValidator</c>,
	/// <c>PermissionNode</c>), adopt the <b>naming convention</b> instead:
	/// <list type="bullet">
	///   <item>Name the reset method exactly <c>public static void ResetForNewScene()</c>.</item>
	///   <item>Add a XML doc reference to this interface so the intent is discoverable.</item>
	///   <item>Register an explicit call in <see cref="Trce.Kernel.Bridge.SandboxBridge.OnLevelLoaded"/>
	///         under the Step 4 block.</item>
	/// </list>
	/// </para>
	/// <para>
	/// <b>Acceptance condition (P0-1):</b> For <i>non-static</i> resettable types, implementing
	/// this interface is sufficient — <see cref="Trce.Kernel.Bridge.SandboxBridge"/> does not need
	/// to be modified. For static classes, one explicit call must be added to the Step 4 block.
	/// </para>
	/// </summary>
	public interface ISceneResettable
	{
		/// <summary>
		/// Clears all owned state so the next scene starts with a clean slate.
		/// Implementations must be safe to call multiple times (idempotent).
		/// </summary>
		static abstract void ResetForNewScene();
	}
}

