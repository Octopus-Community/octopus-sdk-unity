using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Soft elevation behind the existing nine-slice mesh, without extra scene objects.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class SampleUiElevation : BaseMeshEffect
{
    private readonly List<UIVertex> _source = new List<UIVertex>();
    private readonly List<UIVertex> _mesh = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        _source.Clear();
        vertices.GetUIVertexStream(_source);
        _mesh.Clear();
        // Eight low-opacity taps approximate a soft shadow. Rebuilt only when uGUI dirties the
        // source mesh; all taps reuse the rounded sprite's UVs and the same draw call/material.
        const int taps = 8;
        Color ink = OctopusSampleBranding.Palette.ElevationInk;
        float opacity = OctopusSampleBranding.ElevationOpacity;
        for (int tap = 0; tap < taps; tap++)
        {
            float angle = tap * Mathf.PI * 2f / taps;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f)
                * OctopusSampleBranding.Dp(OctopusSampleBranding.ElevationBlur);
            offset.y -= OctopusSampleBranding.Dp(OctopusSampleBranding.ElevationOffset);
            foreach (var source in _source)
            {
                var vertex = source;
                vertex.position += offset;
                ink.a = opacity / taps * source.color.a / 255f;
                vertex.color = ink;
                _mesh.Add(vertex);
            }
        }
        // The opaque surface covers the shadow's centre. Raycasts and layout remain the source's.
        _mesh.AddRange(_source);
        vertices.Clear();
        vertices.AddUIVertexTriangleStream(_mesh);
    }
}
