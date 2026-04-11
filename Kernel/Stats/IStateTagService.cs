// File: Code/Kernel/Stats/IStateTagService.cs
// Encoding: UTF-8 (No BOM)
// Phase 2: 通用狀態標籤服務合約 — TRCE 狀態標籤系統 (State Tag System)。

using Sandbox;

namespace Trce.Kernel.Stats;

/// <summary>
/// <para>【Phase 2 — 通用狀態標籤服務公開合約 (State Tag System)】</para>
/// <para>
/// 提供對 <see cref="GameObject"/> 標籤的統一管理介面，支援即時添加、移除以及
/// 可選的自動到期機制（Duration），適用於「暈眩」、「燃燒」、「無敵」等時限性狀態。
/// </para>
/// <para>
/// <b>架構原則：</b><br/>
/// 實作層直接包裝 s&amp;box 原生 <c>target.Tags.Has / Add / Remove</c>，
/// 確保與引擎標籤系統完全相容且無額外抽象負擔。
/// </para>
/// <para>
/// <b>事件整合：</b><br/>
/// 當標籤真正發生變更時，實作層將透過 <c>GlobalEventBus</c> 發布
/// <see cref="Trce.Kernel.Event.CoreEvents.TagAddedEvent"/> 或
/// <see cref="Trce.Kernel.Event.CoreEvents.TagRemovedEvent"/>。
/// 若標籤重複添加（已存在）或移除不存在的標籤，不會觸發任何事件。
/// </para>
/// <para>
/// <b>效能保證：</b><br/>
/// <c>HasTag</c> 直接委派至 <c>target.Tags.Has</c>，為 O(1) 操作。
/// 計時器更新迴圈採用 Zero-GC 設計，嚴禁 LINQ 與迭代中移除。
/// </para>
/// </summary>
public interface IStateTagService
{
	/// <summary>
	/// 檢查目標 <see cref="GameObject"/> 是否擁有指定標籤。
	/// <para>
	/// <b>【效能保證 — O(1)】</b>：直接委派至 s&amp;box 原生 <c>target.Tags.Has(tag)</c>，無任何額外開銷。
	/// </para>
	/// </summary>
	/// <param name="target">要檢查的目標物件。</param>
	/// <param name="tag">要查詢的標籤字串。</param>
	/// <returns>若目標擁有此標籤則回傳 <c>true</c>，否則回傳 <c>false</c>。</returns>
	bool HasTag( GameObject target, string tag );

	/// <summary>
	/// 向目標 <see cref="GameObject"/> 添加指定標籤。
	/// <para>
	/// 若 <paramref name="durationSeconds"/> 有值，標籤將在指定秒數後自動移除。<br/>
	/// 若標籤已存在，計時器將被重置為新的持續時間（若有提供）；若無提供持續時間，則為 No-Op。<br/>
	/// 只有在標籤確實被添加到 <c>target.Tags</c> 時，才會發布
	/// <see cref="Trce.Kernel.Event.CoreEvents.TagAddedEvent"/>。
	/// </para>
	/// </summary>
	/// <param name="target">要添加標籤的目標物件。</param>
	/// <param name="tag">要添加的標籤字串。</param>
	/// <param name="durationSeconds">
	/// 可選的標籤持續秒數。若為 <c>null</c>，標籤永久存在直到手動移除。
	/// </param>
	void AddTag( GameObject target, string tag, float? durationSeconds = null );

	/// <summary>
	/// 從目標 <see cref="GameObject"/> 移除指定標籤。
	/// <para>
	/// 若標籤不存在，此方法為 No-Op，不會拋出例外，也不會發布任何事件。<br/>
	/// 只有在標籤確實從 <c>target.Tags</c> 移除時，才會發布
	/// <see cref="Trce.Kernel.Event.CoreEvents.TagRemovedEvent"/>。
	/// </para>
	/// </summary>
	/// <param name="target">要移除標籤的目標物件。</param>
	/// <param name="tag">要移除的標籤字串。</param>
	void RemoveTag( GameObject target, string tag );
}
