using System.Collections.Generic;

using UnityEngine;

namespace Assets.Scripts.Runtime.Road.Generators
{
    internal static class RoundaboutPropCleanupUtility
    {
        internal static List<GameObject> FindPropsIntersectingRoundabouts(Transform root)
        {
            var result = new List<GameObject>();
            if (root == null)
            {
                return result;
            }

            var roundaboutRenderers = new List<Renderer>();
            foreach (Transform child in root)
            {
                if (child == null)
                {
                    continue;
                }

                if (child.name == "IntersectionRoundabout" || child.name == "RoadStubRoundabout")
                {
                    Renderer r = child.GetComponent<Renderer>();
                    if (r != null)
                    {
                        roundaboutRenderers.Add(r);
                    }
                }
            }

            if (roundaboutRenderers.Count == 0)
            {
                return result;
            }

            foreach (Transform child in root)
            {
                if (child == null)
                {
                    continue;
                }

                foreach (Transform t in child.GetComponentsInChildren<Transform>(includeInactive: true))
                {
                    if (t == null || t.gameObject == null)
                    {
                        continue;
                    }

                    GameObject go = t.gameObject;
                    if (!go.name.StartsWith("StreetProp_", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (DoesPropIntersectAnyRoundabout(go, roundaboutRenderers))
                    {
                        result.Add(go);
                    }
                }
            }

            return result;
        }

        private static bool DoesPropIntersectAnyRoundabout(GameObject prop, List<Renderer> roundaboutRenderers)
        {
            if (prop == null || roundaboutRenderers == null || roundaboutRenderers.Count == 0)
            {
                return false;
            }

            if (!TryGetObjectBounds(prop, out Bounds propBounds))
            {
                return false;
            }

            for (int i = 0; i < roundaboutRenderers.Count; i++)
            {
                Renderer r = roundaboutRenderers[i];
                if (r == null)
                {
                    continue;
                }

                Bounds b = r.bounds;
                if (BoundsIntersectXZ(propBounds, b) &&
                    propBounds.max.y >= b.min.y - 0.2f &&
                    propBounds.min.y <= b.max.y + 2f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BoundsIntersectXZ(Bounds a, Bounds b)
        {
            bool overlapX = a.min.x <= b.max.x && a.max.x >= b.min.x;
            bool overlapZ = a.min.z <= b.max.z && a.max.z >= b.min.z;
            return overlapX && overlapZ;
        }

        private static bool TryGetObjectBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            var renderers = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (hasBounds)
            {
                return true;
            }

            var colliders = go.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return hasBounds;
        }
    }
}
