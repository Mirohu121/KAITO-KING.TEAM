using UnityEngine;

namespace Robogee.Player
{
    /// <summary>
    /// FPS eye point: follow head bone world position, but keep rotation on the unit root /
    /// CameraPivot (motor pitch/yaw). Avoids inheriting head rest-pose that looks at the sky.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class FpsHeadCameraFollow : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Transform cameraPivot;
        [SerializeField] Vector3 headLocalOffset = new Vector3(0f, 0.06f, 0.12f);
        [SerializeField] float fallbackEyeHeight = 1.7f;
        [SerializeField] float fallbackForward = 0.35f;
        [Tooltip("Cockpit is bolted to the body: sample the head once and ignore walk-anim head bob. " +
                 "Step shake comes from CockpitCameraShake instead.")]
        [SerializeField] bool rigidCockpit = true;

        bool _hasCockpitEye;
        Vector3 _cockpitEyeLocal;

        void Awake()
        {
            if (cameraPivot == null)
            {
                var t = transform.Find("CameraPivot");
                if (t == null)
                {
                    foreach (var c in GetComponentsInChildren<Transform>(true))
                    {
                        if (c.name == "CameraPivot")
                        {
                            t = c;
                            break;
                        }
                    }
                }

                cameraPivot = t;
            }

            if (head == null)
                head = FindHead(transform);
        }

        void LateUpdate()
        {
            if (cameraPivot == null)
                return;

            Vector3 eye;
            if (head != null && rigidCockpit)
            {
                if (!_hasCockpitEye)
                {
                    _cockpitEyeLocal = transform.InverseTransformPoint(head.position) + headLocalOffset;
                    _hasCockpitEye = true;
                }

                eye = transform.TransformPoint(_cockpitEyeLocal);
            }
            else if (head != null)
            {
                // Position from head, offset in BODY forward (not head bone forward).
                eye = head.position
                      + Vector3.up * headLocalOffset.y
                      + transform.forward * headLocalOffset.z
                      + transform.right * headLocalOffset.x;
            }
            else
            {
                eye = transform.position
                      + Vector3.up * fallbackEyeHeight
                      + transform.forward * fallbackForward;
            }

            // Keep as child of root for motor pitch; only rewrite world position.
            cameraPivot.position = eye;
        }

        public void Bind(Transform pivot, Transform headBone)
        {
            cameraPivot = pivot;
            head = headBone != null ? headBone : FindHead(transform);
            _hasCockpitEye = false;
        }

        static Transform FindHead(Transform root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            string[] keys =
            {
                "Head", "head", "HEAD",
                "mixamorig:Head", "mixamorig_Head",
                "HeadTop_End", "head.x", "頭"
            };

            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                for (int k = 0; k < keys.Length; k++)
                {
                    if (n.Equals(keys[k], System.StringComparison.OrdinalIgnoreCase))
                        return all[i];
                    if (n.EndsWith(keys[k], System.StringComparison.OrdinalIgnoreCase))
                        return all[i];
                    if (n.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && n.IndexOf("End", System.StringComparison.OrdinalIgnoreCase) < 0)
                        return all[i];
                }
            }

            return null;
        }
    }
}
