using System;

using UnityEngine;

namespace Assets.Scripts.Runtime.City
{
    [System.Serializable]
    public struct CityNucleus
    {
        [Tooltip("World-space centre of the nucleus.")]
        public Vector2 Centre;

        [Tooltip("Radius in world units within which road density is boosted.")]
        public float Radius;

        [Tooltip("How strongly road tiles are boosted at the nucleus centre. 1 = no boost, 3 = triple weight.")]
        [Range(1f, 5f)]
        public float Strength;

        [Tooltip("Optional neighborhood style profile used for local road/sidewalk/props look.")]
        public NeighborhoodProfile Profile;
    }
}
