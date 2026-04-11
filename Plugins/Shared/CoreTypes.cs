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
		Downed,    // �˦a���椤
		Dead,      // �F��A (����)
		Executed,  // �w�B�M (�q�`���A�ѻP�ӧ�)
		Evacuated, // �w���\�M��
		Spectator  // �[�Ԫ�
	}
}

