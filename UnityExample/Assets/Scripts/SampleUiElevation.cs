using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Soft elevation behind the existing nine-slice mesh, without extra scene objects.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class SampleUiElevation : BaseMeshEffect
{
    /// <summary>
    /// Number of offset copies of the source mesh that approximate the soft shadow.
    /// <para>
    /// This is a fill-rate budget, not a style knob: every tap paints the whole card again in a
    /// transparent pass, so N taps cost N+1 times the card's fill for a rim only
    /// <see cref="OctopusSampleBranding.ElevationBlur"/> dp wide. Eight of them made a
    /// fill-bound mobile GPU miss one vsync in nine while scrolling; four measure at a steady
    /// 60 fps. The opacity budget is split across the taps, so the rim keeps its width and very
    /// nearly its weight; dropping the diagonal taps only flattens the falloff at the corners.
    /// </para>
    /// </summary>
    public const int Taps = 4;

    private readonly List<UIVertex> _source = new List<UIVertex>();
    private readonly List<UIVertex> _mesh = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        _source.Clear();
        vertices.GetUIVertexStream(_source);
        _mesh.Clear();
        // Low-opacity taps around the source approximate a soft shadow. Rebuilt only when uGUI
        // dirties the source mesh; all taps reuse the rounded sprite's UVs and the same draw
        // call/material.
        Color ink = OctopusSampleBranding.Palette.ElevationInk;
        float opacity = OctopusSampleBranding.ElevationOpacity;
        for (int tap = 0; tap < Taps; tap++)
        {
            float angle = tap * Mathf.PI * 2f / Taps;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f)
                * OctopusSampleBranding.Dp(OctopusSampleBranding.ElevationBlur);
            offset.y -= OctopusSampleBranding.Dp(OctopusSampleBranding.ElevationOffset);
            foreach (var source in _source)
            {
                var vertex = source;
                vertex.position += offset;
                ink.a = opacity / Taps * source.color.a / 255f;
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
