# Sample typography

Inter 4.1 Regular and Bold are the unmodified static TTFs from
https://github.com/rsms/inter/releases/tag/v4.1 (`extras/ttf/`).
Copyright 2016 The Inter Project Authors. SIL Open Font License 1.1: `OFL.txt`.

`SampleUiFonts` creates one cached dynamic TMP SDF asset per weight at runtime:
90pt sampling, SDFAA, padding 9, 1024 x 1024 Alpha8 atlas, multi-atlas enabled.
No Font Asset Creator step or generated Inter `.asset` is required. Both fonts
include their source data for player-side glyph population. Regular's 700 weight
maps to the real Bold face. A missing source falls back to `TMP_Settings.defaultFontAsset`.

`TMP/` contains the default Liberation Sans fallback, settings, line-breaking
resources, style sheet and mobile SDF shader from the **TMP Essential Resources**
archive distributed in Unity's `com.unity.ugui` 2.0.0 package. Original asset GUIDs
and importer settings are retained; trailing whitespace is normalized. The settings' unused default emoji sprite
reference is cleared; all other selected resource data is unchanged.
Unity resources: Unity Companion License (`TMP/LICENSE.md`); Liberation Sans:
SIL OFL 1.1 (`TMP/Fonts/LiberationSans - OFL.txt`). The bundled default font material
references the mobile SDF shader, keeping it available in player builds.

The Inter importer metadata is authored from the project's Unity-generated
TrueTypeFontImporter v4 template, with Include Font Data enabled. Folder/script
metadata is authored; the Editor may normalize it on first import. Open this
project in Unity 6000.3.3f1 before visual QA; do not import TMP Essentials again
because these resources retain their upstream GUIDs.
