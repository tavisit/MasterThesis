using Assets.Scripts.Runtime.Graph;

using UnityEngine;

[CreateAssetMenu(fileName = "RoadSettings", menuName = "City/Road Settings")]
public sealed class RoadSettings : ScriptableObject
{
    [Header("Half Widths")]
    public float streetHalfWidth = 4.0f;
    public float metroHalfWidth = 2.5f;


    [Header("Kerb")]
    public float streetKerbHeight = 0.15f;
    public float metroKerbHeight = 0.25f;

    public float streetKerbWidth = 0.4f;
    public float metroKerbWidth = 0.3f;

    public float GetHalfWidth(RoadType type) => type switch
    {
        RoadType.Street => streetHalfWidth,
        RoadType.Metro => metroHalfWidth,
        _ => streetHalfWidth
    };

    public float GetKerbHeight(RoadType type) => type switch
    {
        RoadType.Street => streetKerbHeight,
        RoadType.Metro => metroKerbHeight,
        _ => streetKerbHeight
    };

    public float GetKerbWidth(RoadType type) => type switch
    {
        RoadType.Street => streetKerbWidth,
        RoadType.Metro => metroKerbWidth,
        _ => streetKerbWidth
    };
}
