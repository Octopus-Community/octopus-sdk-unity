# Reef Run art

`Resources/ReefRun/octopus-mark.png` — the swimming character of the Reef Run mini-game
(`Assets/Scripts/Arcade/`).

It is this sample's **own application mark**, not a new asset: `Assets/Icons/iOS/1024.png`
(the iOS app icon shipped by this repository), with the opaque white plate flood-filled to
transparency from the four corners, cropped to its content, squared and resampled to 256 x 256.
The enclosed white inside the dark speech bubble is untouched — the flood fill never crosses the
outline — so the mark reads the same in both themes. Re-exported 2026-09-15; no third-party
artwork, no generator, nothing downloaded.

It sits under a `Resources` folder because the sample builds every screen in code and owns no
prefab or scene reference to load it from; `OctopusReefRunArt` reads it with
`Resources.Load<Sprite>("ReefRun/octopus-mark")` and caches it, exactly as `SampleUiIcons` does
for the Material Symbols in `Assets/Sprites/Resources/SampleIcons`.

Importer metadata is the project's Unity-generated TextureImporter v13 template used by those
icons: Sprite (single), bilinear, clamp, no mipmaps, alpha transparency, uncompressed.

Everything else the game draws — the reefs, the sea bed, the water bands, the surface light — is
generated at runtime from a single white pixel and tinted with `OctopusSampleBranding.Palette`,
so the stage follows the sample's light and dark themes with no second set of assets.
