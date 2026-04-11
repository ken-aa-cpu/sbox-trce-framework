// File: Code/Kernel/Player/Base/TrceMovementEngine.cs
using Sandbox;
using Trce.Kernel.Player;

namespace Trce.Kernel.Plugin.Pawn.Base
{
    /// <summary>
    ///   物理移動核心 (Movement Engine Base)
    ///   權力結構的第二層。負責讀取 CitizenRoot 的意圖 (Intent)，並驅動 CharacterController。
    /// </summary>
    [Title( "TRCE Movement Engine (移動引擎)" )]
    [Category( "TRCE Core - Base" )]
    [Icon( "directions_run" )]
    public class TrceMovementEngine : Component
    {
        [Property, Description("基礎移動速度")]
        public float BaseMoveSpeed { get; set; } = 150f;

        [Property, Description("摩擦力")]
        public float Friction { get; set; } = 5.0f;

        protected ICitizen _root;
        protected CharacterController _controller;

        protected override void OnStart()
        {
            // 向上尋找權力核心 (CitizenRoot)
            _root = Components.Get<ICitizen>();

            // 獲取自身的物理控制器
            _controller = Components.Get<CharacterController>();

            if ( _root == null )
            {
                Log.Warning( $"[{GameObject.Name}] TrceMovementEngine 找不到 ICitizen 權力核心，實體將無法移動。" );
            }
        }

        protected override void OnFixedUpdate()
        {
            if ( _root == null || _controller == null ) return;

            // 如果是網路代理物件，且不由本地控制物理，則跳過 (交由 s&box 原生 Sync 處理)
            if ( IsProxy && !Network.IsOwner ) return;

            ApplyMovement( _root.Intent );
        }

        /// <summary>
        ///   將意圖轉換為物理位移 (可供子類別如飛龍飛行、載具駕駛進行覆寫)
        /// </summary>
        protected virtual void ApplyMovement( CitizenIntent intent )
        {
            // 1. 計算目標水平速度
            var targetVelocity = intent.WishMove * BaseMoveSpeed;
            if ( intent.WishSprint )
            {
                targetVelocity *= 1.5f;
            }

            // 2. 繼承當前的垂直速度 (處理重力與跳躍)
            var currentVelocity = _controller.Velocity;
            if ( _controller.IsOnGround )
            {
                currentVelocity.z = 0; // 站在地上時歸零
                if ( intent.WishJump )
                {
                    // 修正物理跳躍
                    currentVelocity.z = 350f; // 給予向上初速
                    _controller.Punch( Vector3.Up * 50f ); // 額外物理衝量
                }
            }
            else
            {
                currentVelocity += Scene.PhysicsWorld.Gravity * Time.Delta; // 在空中受重力影響
            }

            // 3. 結合大腦的水平速度 (X, Y) 與物理的垂直速度 (Z)
            currentVelocity = new Vector3( targetVelocity.x, targetVelocity.y, currentVelocity.z );

            // 5. 讓角色身體跟著移動方向旋轉 (平滑轉向)
            if ( intent.WishMove.Length > 0.1f )
            {
                var targetRot = Rotation.LookAt( intent.WishMove, Vector3.Up );
                GameObject.Transform.Rotation = Rotation.Slerp( GameObject.Transform.Rotation, targetRot, Time.Delta * 10f );
            }

            // 6. 強制寫入速度，並推動控制器！
            _controller.Velocity = currentVelocity;
            _controller.Move();
        }
    }
}
