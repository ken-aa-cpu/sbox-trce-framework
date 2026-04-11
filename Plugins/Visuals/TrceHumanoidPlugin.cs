using Sandbox;
using System.Threading.Tasks;
using Trce.Kernel.Plugin;
using Trce.Kernel.Visuals;

namespace Trce.Plugins.Visuals
{
	/// <summary>
	/// TRCE 人形模型選配插件，實作 <see cref="IModelService"/>。
	/// 內部封裝 s&box 官方的 <see cref="SkinnedModelRenderer"/>。
	/// </summary>
	[TrcePlugin( Id = "visuals.humanoid", Name = "TRCE Humanoid Visuals", Version = "1.0.0" )]
	public sealed class TrceHumanoidPlugin : TrcePlugin, IModelService
	{
		protected override Task OnPluginEnabled()
		{
			// 註冊服務至定位器
			TrceServiceManager.Instance?.RegisterService<IModelService>( this );
			return Task.CompletedTask;
		}

		protected override void OnPluginDisabled()
		{
			// 銷毀時註銷服務
			TrceServiceManager.Instance?.UnregisterService<IModelService>();
		}

		/// <inheritdoc/>
		public void SetAnimParameter( GameObject target, string paramName, float value )
		{
			if ( target is null ) return;

			var renderer = target.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			renderer?.Set( paramName, value );
		}

		/// <inheritdoc/>
		public void SetAnimParameter( GameObject target, string paramName, bool value )
		{
			if ( target is null ) return;

			var renderer = target.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			renderer?.Set( paramName, value );
		}

		/// <inheritdoc/>
		public void SetAnimParameter( GameObject target, string paramName, int value )
		{
			if ( target is null ) return;

			var renderer = target.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			renderer?.Set( paramName, value );
		}

		/// <inheritdoc/>
		public void SetModel( GameObject target, string modelPath )
		{
			if ( target is null ) return;

			var renderer = target.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
			if ( renderer != null )
			{
				renderer.Model = Model.Load( modelPath );
			}
		}
	}
}
