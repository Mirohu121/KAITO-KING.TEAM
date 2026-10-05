namespace Robogee.Player
{
    /// <summary>
    /// One frame of unit control intent. Filled by humans (Input System) or AI — never by the motor.
    /// </summary>
    public struct UnitInputFrame
    {
        public float MoveX;
        public float MoveY;
        public float LookX;
        public float LookY;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool DashHeld;
        public bool ToggleCursorPressed;
        public bool AttackPressed;
        public bool LockOnPressed;

        public UnityEngine.Vector3 MovePlanar => new UnityEngine.Vector3(MoveX, 0f, MoveY);
    }

    /// <summary>
    /// Provides per-frame control for <see cref="UnitBMotor"/>. Swap implementations for P2 / NPC / net.
    /// </summary>
    public interface IUnitInputSource
    {
        UnitInputFrame Current { get; }
    }

    /// <summary>Optional aim override (e.g. lock-on). Implemented outside the motor.</summary>
    public interface IAimLockProvider
    {
        bool TryGetLockedAimPoint(out UnityEngine.Vector3 worldPoint);
    }
}
