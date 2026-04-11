using Sandbox;

namespace Trce.Kernel.Player
{
	/// <summary>
	///   Citizen 介面：定義了所有「公民」（玩家或 NPC）必須具備的控制接口。
	///   這使得 AI 系統與玩家輸入手動控制可以共用同一套邏輯。
	/// </summary>
	public interface ICitizen
	{
		/// <summary> 
		///   獲取或設定公民的意圖（Intent）。
		///   對於玩家，這通常由輸入系統每幀更新。
		///   對於 NPC，這由 AI 決策系統設定。
		/// </summary>
		CitizenIntent Intent { get; set; }

		/// <summary> 引用所屬的 GameObject </summary>
		GameObject GameObject { get; }

		/// <summary> 世界座標位置 </summary>
		Vector3 Position { get; set; }

		/// <summary> 身體旋轉 </summary>
		Rotation Rotation { get; set; }

		/// <summary> 視線/頭部旋轉 </summary>
		Rotation EyeRotation { get; set; }

		/// <summary> 
		///   主動執行一個具名動作。
		///   由 ActionEngine 解析名稱並轉換為動畫參數或腳本邏輯。
		/// </summary>
		void ExecuteAction( string actionName );

		/// <summary> 當有動作被請求時觸發 </summary>
		event System.Action<string> OnActionRequested;

		/// <summary> 
		///   強制轉換意圖，用於過場動畫或受控狀態（如被擊昏）。
		/// </summary>
		bool IsControlledByScript { get; set; }
	}
}
