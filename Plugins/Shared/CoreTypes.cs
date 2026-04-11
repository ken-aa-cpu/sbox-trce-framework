namespace Trce.Kernel.Plugin.Services
{
	/// <summary>
	/// </summary>
	public enum CurrencyType
	{
		TraceCoin,
		TracePoint
	}
	public enum AliveState
	{
		Alive,
		Downed,    // knocked down, incapacitated
		Dead,      // dead (game over)
		Executed,  // executed (usually involves other players)
		Evacuated, // successfully evacuated
		Spectator  // spectating
	}
}
