using UnityEngine;

namespace Assets.Scripts.Runtime.Demo
{
    [RequireComponent(typeof(Camera))]
    public sealed class PerspectiveCameraDolly : MonoBehaviour
    {
        private static readonly Quaternion FixedRotation =
            new Quaternion(0.707106829f, 0f, 0f, 0.707106829f);

        [SerializeField] private Vector3 _worldStart = new Vector3(500f, 1500f, 500f);
        [SerializeField] private Vector3 _worldEnd = new Vector3(250f, 100f, 250f);

        [SerializeField] private float _durationSeconds = 8f;

        [SerializeField] private bool _playOnEnable = true;

        private float _elapsed;

        private void OnEnable()
        {
            _elapsed = 0f;
            transform.SetPositionAndRotation(_worldStart, FixedRotation);
            var cam = GetComponent<Camera>();
            cam.orthographic = false;
        }

        private void Update()
        {
            if (!_playOnEnable)
            {
                return;
            }

            transform.rotation = FixedRotation;

            if (_durationSeconds <= 0f)
            {
                transform.position = _worldEnd;
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _durationSeconds);
            transform.position = Vector3.Lerp(_worldStart, _worldEnd, t);
        }
    }
}
