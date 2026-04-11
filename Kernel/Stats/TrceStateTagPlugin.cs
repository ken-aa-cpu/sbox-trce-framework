// File: Code/Kernel/Stats/TrceStateTagPlugin.cs
// Encoding: UTF-8 (No BOM)
// Phase 2: IStateTagService 核心實作 — 包裝 s&box 原生 Tags、Zero-GC 計時器迴圈、熱重載防呆。

using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using Trce.Kernel.Event;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Stats;

/// <summary>
/// 【Phase 2 — TRCE 通用狀態標籤服務實作 (State Tag Plugin)】
/// <para>
/// 直接包裝 s&amp;box 原生 <c>GameObject.Tags.Has / Add / Remove</c>，
/// 並在其上疊加可選的自動到期計時器機制。
/// </para>
/// <para>
/// <b>【Zero-GC 效能死線 — OnUpdate 迴圈】</b><br/>
/// 計時器掃描使用預先分配的 <c>_expiredKeys</c> 移除暫存列表，
/// 以 <c>for</c> 索引迴圈進行倒序移除，絕對零 GC Alloc：
/// <list type="bullet">
///   <item>嚴禁 LINQ</item>
///   <item>嚴禁在 foreach 迭代 Dictionary 期間進行 Remove</item>
///   <item><c>_expiredKeys</c> 在 OnPluginDisabled 統一 Clear，不重複分配</item>
/// </list>
/// </para>
/// <para>
/// <b>【防呆】</b>：OnPluginDisabled 強制清空所有計時器快取，確保熱重載後零殘留。
/// </para>
/// </summary>
[TrcePlugin( Id = "trce.statetag", Name = "TRCE State Tag System", Version = "2.0.0", Author = "TRCE Team" )]
[Icon( "label" )]
[Title( "TRCE State Tag Plugin" )]
public sealed class TrceStateTagPlugin : TrcePlugin, IStateTagService
{
	// ─────────────────────────────────────────────
	//  計時器資料結構
	// ─────────────────────────────────────────────

	/// <summary>
	/// 複合鍵：以 (GameObject, tag) 為 Key，記錄該標籤的到期時間（<c>Time.Now + duration</c>）。
	/// <para>完全無靜態欄位，確保熱重載後自動被 GC 回收，零殘留。</para>
	/// </summary>
	private readonly Dictionary<(GameObject, string), float> _timers = new();

	/// <summary>
	/// 【Zero-GC 設計】預先分配的移除暫存列表。
	/// OnUpdate 每幀重用此列表收集過期 Key，再批次移除，杜絕 InvalidOperationException。
	/// </summary>
	private readonly List<(GameObject, string)> _expiredKeys = new();

	// ─────────────────────────────────────────────
	//  生命週期
	// ─────────────────────────────────────────────

	/// <inheritdoc/>
	protected override Task OnPluginEnabled()
	{
		TrceServiceManager.Instance?.RegisterService<IStateTagService>( this );
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	protected override void OnPluginDisabled()
	{
		// 防呆核心：清空所有計時器快取與暫存列表，確保熱重載後零殘留
		_timers.Clear();
		_expiredKeys.Clear();
		TrceServiceManager.Instance?.UnregisterService<IStateTagService>();
	}

	// ─────────────────────────────────────────────
	//  OnUpdate — Zero-GC 計時器掃描迴圈
	// ─────────────────────────────────────────────

	/// <summary>
	/// 每幀掃描到期標籤並自動移除。
	/// <para>
	/// <b>【Zero-GC 實作細節】</b><br/>
	/// 1. 以 for 迴圈搭配 <c>_timers</c> KeyValuePair List（避免 Dictionary Enumerator 在修改時拋出例外）<br/>
	/// 2. 使用預先分配的 <c>_expiredKeys</c> 收集過期項目<br/>
	/// 3. 倒序遍歷 <c>_expiredKeys</c> 執行移除，防止索引偏移<br/>
	/// 4. 全程無 LINQ、無匿名物件、無 boxing
	/// </para>
	/// </summary>
	protected override void OnUpdate()
	{
		if ( _timers.Count == 0 )
			return;

		float now = Time.Now;

		// 第一遍：收集所有已到期的 Key 至預分配暫存列表
		// 使用 foreach 僅用於讀取（不在此處呼叫 Remove），符合 Zero-GC 規範
		foreach ( var kv in _timers )
		{
			if ( now >= kv.Value )
				_expiredKeys.Add( kv.Key );
		}

		// 第二遍：批次移除到期標籤（倒序，防止 List 索引偏移）
		for ( int i = _expiredKeys.Count - 1; i >= 0; i-- )
		{
			var key = _expiredKeys[i];
			_timers.Remove( key );

			// 只有在標籤確實存在時才執行移除並發布事件
			if ( key.Item1.IsValid() && key.Item1.Tags.Has( key.Item2 ) )
			{
				key.Item1.Tags.Remove( key.Item2 );
				GlobalEventBus.Publish( new CoreEvents.TagRemovedEvent( key.Item1, key.Item2 ) );
			}
		}

		// 清空暫存列表以供下一幀複用（不 new，符合 Zero-GC）
		_expiredKeys.Clear();
	}

	// ─────────────────────────────────────────────
	//  IStateTagService 實作
	// ─────────────────────────────────────────────

	/// <inheritdoc/>
	public bool HasTag( GameObject target, string tag )
	{
		return target.Tags.Has( tag );
	}

	/// <inheritdoc/>
	public void AddTag( GameObject target, string tag, float? durationSeconds = null )
	{
		// 只有標籤不存在時才真正添加，並發布事件（防止重複添加觸發冗餘事件）
		bool alreadyHas = target.Tags.Has( tag );

		if ( !alreadyHas )
		{
			target.Tags.Add( tag );
			GlobalEventBus.Publish( new CoreEvents.TagAddedEvent( target, tag ) );
		}

		// 若有提供持續時間，無論是否為新添加，皆更新（重置）計時器
		if ( durationSeconds.HasValue )
		{
			var key = (target, tag);
			_timers[key] = Time.Now + durationSeconds.Value;
		}
		else if ( alreadyHas )
		{
			// 標籤已存在且無持續時間 → No-Op（保持原狀，不清除既有計時器）
		}
	}

	/// <inheritdoc/>
	public void RemoveTag( GameObject target, string tag )
	{
		// 只有標籤確實存在時才移除，並發布事件
		if ( !target.Tags.Has( tag ) )
			return;

		target.Tags.Remove( tag );

		// 同步移除對應的計時器（若有）
		var key = (target, tag);
		_timers.Remove( key );

		GlobalEventBus.Publish( new CoreEvents.TagRemovedEvent( target, tag ) );
	}
}
