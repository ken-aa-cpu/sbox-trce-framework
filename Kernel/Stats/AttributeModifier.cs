// File: Code/Kernel/Stats/AttributeModifier.cs
// Encoding: UTF-8 (No BOM)
// Phase 2: 零 GC 屬性修飾符定義 — 值類型，Stack 分配，不可變。

namespace Trce.Kernel.Stats;

/// <summary>
/// 定義 <see cref="AttributeModifier"/> 如何參與最終值的計算。
/// </summary>
public enum ModifierType : byte
{
	/// <summary>
	/// <b>加法修飾符 (Flat Bonus)。</b>
	/// </summary>
	Add,

	/// <summary>
	/// <b>乘法修飾符 (Percentage Multiplier)。</b>
	/// </summary>
	Multiply,
}

/// <summary>
/// <para>【Zero-Allocation — 不可變屬性修飾符】</para>
/// <para>
/// 描述對某一屬性的單次計算修飾。設計為 <c>readonly struct</c> 以確保無 GC Pressure。
/// </para>
/// </summary>
public readonly struct AttributeModifier
{
	/// <summary>修飾符的計算類型</summary>
	public readonly ModifierType Type;

	/// <summary>修飾符的數值</summary>
	public readonly float Value;

	/// <summary>修飾符的優先權排序</summary>
	public readonly int Priority;

	public AttributeModifier( ModifierType type, float value, int priority = 0 )
	{
		Type     = type;
		Value    = value;
		Priority = priority;
	}

	public static AttributeModifier Flat( float value, int priority = 0 )
		=> new AttributeModifier( ModifierType.Add, value, priority );

	public static AttributeModifier Percent( float multiplier, int priority = 0 )
		=> new AttributeModifier( ModifierType.Multiply, multiplier, priority );

	public override string ToString()
		=> $"[{Type}] {Value:F3} (Priority: {Priority})";
}
