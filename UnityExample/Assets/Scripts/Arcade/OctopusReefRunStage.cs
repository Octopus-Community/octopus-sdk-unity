using UnityEngine;

/// <summary>
/// The Reef Run world, drawn the way a Unity game is drawn: <see cref="SpriteRenderer"/>s in the
/// scene, lit by nothing, filmed by a dedicated orthographic <see cref="Camera"/>. No uGUI, no
/// UI Toolkit — the HUD around it is uGUI, the game itself is not.
///
/// Two consequences of living inside a sample shell shaped like an app rather than like a game:
///
/// * **The camera renders to a <see cref="RenderTexture"/>**, which the screen shows in a
///   <c>RawImage</c>. A Screen Space – Overlay canvas draws after *every* camera, so a camera
///   painting straight onto the screen would sit behind the shell's own canvas whatever sorting
///   order it were given. Through a texture the game is just another piece of content the overlay
///   composites, in the right order, inside the layout it was given.
/// * **The stage is parked at <see cref="StageOrigin"/>**, ten thousand units above the scene, so
///   the sample's own camera never has it in frame. A dedicated layer would say the same thing
///   more idiomatically, but layers live in `ProjectSettings/TagManager.asset` — shared, global,
///   and not something a sample screen should be allowed to spend.
///
/// Nothing here is created or destroyed while a run is going: the reefs are recycled by
/// <see cref="OctopusReefRun"/> and their renderers are recycled with them.
/// </summary>
public sealed class OctopusReefRunStage : MonoBehaviour
{
    /// <summary>Far enough from the scene that no other camera can frame the stage.</summary>
    public const float StageOrigin = 10000f;

    /// <summary>
    /// 4:3, and the screen fits the texture to that ratio rather than the reverse. The visible
    /// half-width is then 6.67 world units against the game's own 6, so a reef leaves the frame
    /// after the simulation has already retired it, never before.
    /// </summary>
    public const int TextureWidth = 960;
    public const int TextureHeight = 720;

    /// <summary>How much of a reef's inner edge is drawn as a lighter lip, in world units.</summary>
    public const float LipHeight = 0.28f;

    /// <summary>Depth of the sea bed and of the bright surface band, in world units.</summary>
    public const float BedHeight = 0.55f;

    /// <summary>Drifting bubbles. Decoration only: their motion is a function of the step count.</summary>
    public const int BubbleCount = 7;

    private const float Margin = 2f;

    private Camera _camera;
    private RenderTexture _texture;
    private SpriteRenderer _player;
    private SpriteRenderer[] _reefTop;
    private SpriteRenderer[] _reefBottom;
    private SpriteRenderer[] _reefTopLip;
    private SpriteRenderer[] _reefBottomLip;
    private SpriteRenderer[] _bubbles;
    private SpriteRenderer[] _water;
    private SpriteRenderer _bed;
    private SpriteRenderer _surface;

    /// <summary>What the screen displays. Owned by the stage and released with it.</summary>
    public RenderTexture Texture { get { return _texture; } }

    /// <summary>Exposed for the screen's aspect fitter and for tests; never repointed elsewhere.</summary>
    public Camera Camera { get { return _camera; } }

    /// <summary>Builds the stage on its own root object, parked away from the scene.</summary>
    public static OctopusReefRunStage Create()
    {
        var host = new GameObject("OctopusReefRunStage");
        host.transform.position = new Vector3(0f, StageOrigin, 0f);
        var stage = host.AddComponent<OctopusReefRunStage>();
        stage.Build();
        return stage;
    }

    private void Build()
    {
        _texture = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.Default)
        {
            name = "ReefRunStage",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        _texture.Create();

        var eye = new GameObject("Stage camera");
        eye.transform.SetParent(transform, false);
        eye.transform.localPosition = new Vector3(0f, 0f, -10f);
        _camera = eye.AddComponent<Camera>();
        _camera.orthographic = true;
        _camera.orthographicSize = OctopusReefRun.WorldHeight * 0.5f;
        _camera.aspect = TextureWidth / (float)TextureHeight;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 100f;
        _camera.depth = -50f;
        _camera.allowHDR = false;
        _camera.allowMSAA = false;
        _camera.useOcclusionCulling = false;
        _camera.targetTexture = _texture;

        var half = OctopusReefRun.WorldHeight * 0.5f;
        var width = _camera.orthographicSize * 2f * _camera.aspect + Margin;

        _water = new SpriteRenderer[3];
        for (int i = 0; i < _water.Length; i++)
        {
            var band = 2f * half / _water.Length;
            _water[i] = Shape("Water " + i, -40 + i);
            Place(_water[i], 0f, half - band * (i + 0.5f), width, band);
        }
        _surface = Shape("Surface light", -30);
        Place(_surface, 0f, half - BedHeight * 0.5f, width, BedHeight);
        _bed = Shape("Sea bed", -30);
        Place(_bed, 0f, -half + BedHeight * 0.5f, width, BedHeight);

        _bubbles = new SpriteRenderer[BubbleCount];
        for (int i = 0; i < BubbleCount; i++) _bubbles[i] = Shape("Bubble " + i, -20);

        _reefTop = new SpriteRenderer[OctopusReefRun.ReefCount];
        _reefBottom = new SpriteRenderer[OctopusReefRun.ReefCount];
        _reefTopLip = new SpriteRenderer[OctopusReefRun.ReefCount];
        _reefBottomLip = new SpriteRenderer[OctopusReefRun.ReefCount];
        for (int i = 0; i < OctopusReefRun.ReefCount; i++)
        {
            _reefTop[i] = Shape("Reef " + i + " top", 0);
            _reefBottom[i] = Shape("Reef " + i + " bottom", 0);
            _reefTopLip[i] = Shape("Reef " + i + " top lip", 1);
            _reefBottomLip[i] = Shape("Reef " + i + " bottom lip", 1);
        }

        var player = new GameObject("Octopus");
        player.transform.SetParent(transform, false);
        _player = player.AddComponent<SpriteRenderer>();
        _player.sortingOrder = 10;
        var mark = OctopusReefRunArt.Mark();
        // No mark asset — the game still plays, as a disc. A missing PNG must not be a crash on a
        // player's device, and an EditMode test that runs without the importer sees this path.
        _player.sprite = mark != null ? mark : OctopusReefRunArt.Pixel();
        var size = OctopusReefRun.PlayerRadius * 2.4f;
        if (mark != null && mark.bounds.size.y > 0f)
            player.transform.localScale = Vector3.one * (size / mark.bounds.size.y);
        else
            player.transform.localScale = new Vector3(size, size, 1f);

        ApplyPalette();
    }

    private SpriteRenderer Shape(string name, int order)
    {
        var host = new GameObject(name);
        host.transform.SetParent(transform, false);
        var renderer = host.AddComponent<SpriteRenderer>();
        renderer.sprite = OctopusReefRunArt.Pixel();
        renderer.sortingOrder = order;
        return renderer;
    }

    private static void Place(SpriteRenderer renderer, float x, float y, float width, float height)
    {
        renderer.transform.localPosition = new Vector3(x, y, 0f);
        renderer.transform.localScale = new Vector3(width, Mathf.Max(0f, height), 1f);
    }

    /// <summary>Re-reads the token palette. Called on build and whenever the sample's theme flips.</summary>
    public void ApplyPalette()
    {
        if (_camera == null) return;
        var p = OctopusSampleBranding.Palette;
        _camera.backgroundColor = OctopusReefRunArt.Water(0.04f);
        for (int i = 0; i < _water.Length; i++)
            _water[i].color = OctopusReefRunArt.Water(0.05f + 0.06f * (_water.Length - 1 - i));
        _surface.color = OctopusReefRunArt.Water(0.34f);
        _bed.color = p.Surface;
        var reef = p.Accent;
        var lip = OctopusSampleBranding.Tint(p.OnAccent, p.Accent, 0.28f);
        for (int i = 0; i < OctopusReefRun.ReefCount; i++)
        {
            _reefTop[i].color = _reefBottom[i].color = reef;
            _reefTopLip[i].color = _reefBottomLip[i].color = lip;
        }
        var bubble = OctopusSampleBranding.Tint(p.OnAccent, OctopusReefRunArt.Water(0.12f), 0.22f);
        for (int i = 0; i < BubbleCount; i++) _bubbles[i].color = bubble;
        if (OctopusReefRunArt.Mark() == null) _player.color = p.OnAccent;
    }

    /// <summary>Moves every renderer onto the state <paramref name="run"/> is in. Reads, never writes.</summary>
    public void Render(OctopusReefRun run)
    {
        if (run == null || _camera == null) return;
        var half = OctopusReefRun.WorldHeight * 0.5f;
        var width = OctopusReefRun.ReefHalfWidth * 2f;

        for (int i = 0; i < OctopusReefRun.ReefCount; i++)
        {
            var reef = run.GetReef(i);
            var top = reef.GapCenter + reef.GapHalf;
            var bottom = reef.GapCenter - reef.GapHalf;
            var topHeight = half + Margin - top;
            var bottomHeight = bottom + half + Margin;
            Place(_reefTop[i], reef.X, top + topHeight * 0.5f, width, topHeight);
            Place(_reefBottom[i], reef.X, bottom - bottomHeight * 0.5f, width, bottomHeight);
            Place(_reefTopLip[i], reef.X, top + LipHeight * 0.5f, width * 1.12f, LipHeight);
            Place(_reefBottomLip[i], reef.X, bottom - LipHeight * 0.5f, width * 1.12f, LipHeight);
        }

        // Decoration, and deliberately a pure function of the step count: the stage adds no clock
        // of its own, so a paused run is a still image rather than a frozen one that keeps drifting.
        var drift = run.Steps * OctopusReefRun.StepSeconds;
        for (int i = 0; i < BubbleCount; i++)
        {
            var speed = 0.6f + 0.22f * i;
            var x = Mathf.Repeat(2.4f * i - drift * (OctopusReefRun.Speed * 0.35f), 14f) - 7f;
            var y = Mathf.Repeat(i * 1.7f + drift * speed, OctopusReefRun.WorldHeight) - half;
            var size = 0.1f + 0.035f * (i % 3);
            Place(_bubbles[i], x, y, size, size);
        }

        _player.transform.localPosition = new Vector3(OctopusReefRun.PlayerX, run.PlayerY, 0f);
        // Nose up on the impulse, nose down on the fall — the only thing that tells a player which
        // way the tap sent them, on a character that never moves horizontally.
        var pitch = Mathf.Clamp(run.PlayerVelocity * 4.5f, -62f, 26f);
        _player.transform.localRotation = Quaternion.Euler(0f, 0f, run.Running ? pitch : -78f);
    }

    private void OnDestroy() { Release(); }

    /// <summary>Drops the render texture. Idempotent — <c>OnDestroy</c> and the screen both call it.</summary>
    public void Release()
    {
        if (_camera != null) _camera.targetTexture = null;
        if (_texture == null) return;
        _texture.Release();
        if (Application.isPlaying) Destroy(_texture);
        else DestroyImmediate(_texture);
        _texture = null;
    }

    /// <summary>Tears the stage down, render texture included.</summary>
    public void Dispose()
    {
        Release();
        if (this == null) return;
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}
