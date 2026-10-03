using UnityEngine;

namespace GamePhone.Movement
{
    public abstract class CameraModeControllerBase : MonoBehaviour
    {
        public abstract Vector3 GetMoveDirection(Vector2 rawInput);
        public abstract Vector3 GetHeadLookTarget();
    }
}
