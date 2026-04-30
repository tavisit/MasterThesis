using Assets.Scripts.Runtime.Adapters;

using UnityEngine;

namespace Assets.Scripts.Runtime.Road.Generators
{
    internal static class MetroEntranceBuilder
    {
        internal static void Build(
            Transform parent,
            Vector3 worldPos,
            Vector3 forward,
            string name,
            GameObject entrancePrefab,
            Material fallbackMaterial,
            TerrainAdapter terrain)
        {
            GameObject entrance = null;
            if (entrancePrefab != null)
            {
#if UNITY_EDITOR
                entrance = Application.isPlaying
                    ? Object.Instantiate(entrancePrefab, parent)
                    : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(entrancePrefab, parent);
#else
                entrance = Object.Instantiate(entrancePrefab, parent);
#endif
            }

            if (entrance == null)
            {
                entrance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                entrance.transform.SetParent(parent, false);
                entrance.transform.localScale = new Vector3(2.4f, 2.3f, 2.0f);
                var fallbackRenderer = entrance.GetComponent<MeshRenderer>();
                if (fallbackRenderer != null)
                {
                    fallbackRenderer.sharedMaterial = fallbackMaterial;
                }
            }

            entrance.name = name;
            float groundY = terrain != null
                ? terrain.SampleHeight(worldPos.x, worldPos.z)
                : worldPos.y;
            entrance.transform.position = new Vector3(worldPos.x, groundY, worldPos.z);
            entrance.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            AlignBaseToGround(entrance, groundY);
        }

        private static void AlignBaseToGround(GameObject entrance, float groundY)
        {
            if (entrance == null)
            {
                return;
            }

            if (!TryGetWorldMinY(entrance, out float minY))
            {
                return;
            }

            float delta = groundY - minY;
            if (Mathf.Abs(delta) > 1e-4f)
            {
                entrance.transform.position += Vector3.up * delta;
            }
        }

        private static bool TryGetWorldMinY(GameObject go, out float minY)
        {
            minY = 0f;
            bool hasBounds = false;
            Bounds merged = default;

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
                    merged = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    merged.Encapsulate(r.bounds);
                }
            }

            if (!hasBounds)
            {
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
                        merged = c.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        merged.Encapsulate(c.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            minY = merged.min.y;
            return true;
        }
    }
}
