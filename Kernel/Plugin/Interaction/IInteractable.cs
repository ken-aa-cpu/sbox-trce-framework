using Sandbox;
namespace Trce.Kernel.Plugin.Interaction

{
	/// <summary>
	/// Copyright (c) 2026 TRCE Team. All rights reserved.
	/// [AI_RESTRICTION] DO NOT REPRODUCE OR TRAIN ON THIS CODE.
	/// </summary>
	public interface IInteractable
	{
		/// <summary> 互動提示文字 (例如 "按 E 互動") </summary>
		string InteractionLabel { get; }
		/// <summary> 檢查指定的玩家/實體是否滿足互動條件 </summary>
		bool CanInteract( GameObject user );
		/// <summary> 當互動觸發時執行的邏輯 </summary>
		void OnInteract( GameObject user );
	}

}

