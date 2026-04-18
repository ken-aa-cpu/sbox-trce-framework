// File: Code/Kernel/Net/SnapshotStateCollector.cs
// Encoding: UTF-8 (No BOM)
// P2-4: Accumulates opaque state tokens contributed by plugins during a snapshot cycle.

using System.Text;

namespace Trce.Kernel.Net
{
	/// <summary>
	/// Accumulates opaque state tokens contributed by plugins during a <c>SnapshotStateContributionEvent</c> cycle.
	/// <para>
	/// Plugins subscribe to <c>CoreEvents.SnapshotStateContributionEvent</c> and call <see cref="Contribute"/>
	/// with a short, deterministic string that represents a fragment of their authoritative game state
	/// (e.g. a player count hash, item checksum, or round-phase token).
	/// </para>
	/// <para>
	/// <b>Rules for contributors:</b><br/>
	/// - Tokens must be <b>deterministic</b>: the same state must always produce the same string.<br/>
	/// - Tokens must be <b>short</b> (a few chars or hex digits): this runs on every sync interval.<br/>
	/// - Tokens must <b>not contain the pipe character</b> '<c>|</c>', which is used as a separator.
	/// </para>
	/// </summary>
	public sealed class SnapshotStateCollector
	{
		private readonly StringBuilder _sb = new();

		/// <summary>
		/// Appends a labelled state token to the snapshot fingerprint.
		/// </summary>
		/// <param name="key">Short label identifying the contributor (e.g. <c>"players"</c>, <c>"round"</c>).</param>
		/// <param name="value">The deterministic state token.</param>
		public void Contribute( string key, string value )
		{
			_sb.Append( key );
			_sb.Append( ':' );
			_sb.Append( value );
			_sb.Append( '|' );
		}

		/// <summary>
		/// Returns the accumulated state string. Called by <c>SnapshotSync</c> after all contributions are collected.
		/// </summary>
		public string Build() => _sb.ToString();
	}
}
