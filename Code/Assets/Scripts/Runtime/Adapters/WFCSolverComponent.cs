using Assets.Scripts.Runtime.City;
using Assets.Scripts.Runtime.WFC;

using UnityEngine;

namespace Assets.Scripts.Runtime.Adapters
{
    public sealed class WFCSolverComponent : MonoBehaviour
    {
        private CityManager _manager;

        public WFCSolver Solver => Manager?.StreetSolver;
        public int Rows => Manager?.Rows ?? 0;
        public int Columns => Manager?.Columns ?? 0;
        public float CellSize => Manager?.CellSize ?? 0f;
        public SolveResult LastResult => Manager?.LastResult ?? SolveResult.Failure;
        public int CollapseCount => Manager?.CollapseCount ?? 0;
        public int BacktrackCount => Manager?.BacktrackCount ?? 0;

        private void Awake() => _manager = GetComponent<CityManager>();

        private CityManager Manager => _manager != null ? _manager : (_manager = GetComponent<CityManager>());
        public void Generate() => Manager?.SolveStreets();
    }
}
