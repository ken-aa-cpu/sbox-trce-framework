using Sandbox;

namespace Trce.Kernel.Visuals
{
	/// <summary>
	/// Model engine service interface, decoupling core logic from specific rendering implementations.
	/// </summary>
	public interface IModelService
	{
		/// <summary> Sets an animation parameter (float). </summary>
		void SetAnimParameter( GameObject target, string paramName, float value );

		/// <summary> Sets an animation parameter (bool). </summary>
		void SetAnimParameter( GameObject target, string paramName, bool value );

		/// <summary> Sets an animation parameter (int). </summary>
		void SetAnimParameter( GameObject target, string paramName, int value );

		/// <summary> Sets the model path on the target. </summary>
		void SetModel( GameObject target, string modelPath );
	}
}
