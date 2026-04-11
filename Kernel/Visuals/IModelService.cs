using Sandbox;

namespace Trce.Kernel.Visuals
{
	/// <summary>
	/// 模型引擎服務介面，用於解耦核心邏輯與具體的渲染實作。
	/// </summary>
	public interface IModelService
	{
		/// <summary> 設定動畫參數 (float) </summary>
		void SetAnimParameter( GameObject target, string paramName, float value );

		/// <summary> 設定動畫參數 (bool) </summary>
		void SetAnimParameter( GameObject target, string paramName, bool value );

		/// <summary> 設定動畫參數 (int) </summary>
		void SetAnimParameter( GameObject target, string paramName, int value );

		/// <summary> 設定模型的路徑 </summary>
		void SetModel( GameObject target, string modelPath );
	}
}
