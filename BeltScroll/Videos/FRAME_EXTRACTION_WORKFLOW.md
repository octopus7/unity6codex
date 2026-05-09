# Frame Extraction Workflow

This documents the workflow used for `Videos/movecycle-dreamina-2026-05-06.MP4`.

## Current Output

The current kept image output is the wizard-based walk cycle:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle
```

The current transparent cropped output is:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_cropped_transparent
```

The front-24 crop-only loop candidate is:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_front24_cropped
```

The cleaned 6x4 bleed-background sprite sheet is:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_front24_grid_6x4_cleaned_bleed/wizard_front24_grid_6x4_cleaned_bleed.png
```

The transparent alpha-split output is:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_front24_alpha_split
```

The black/white opacity preview output is:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview
```

The full-frame dump for choosing a run loop is:

```text
Videos/movecycle-dreamina-2026-05-06_run_full_frames
```

The current run-loop crop sheet candidate is:

```text
Videos/movecycle-dreamina-2026-05-06_run_111_126_wizard_crop_4x4/wizard_run_111_126_grid_4x4.png
```

Current result:

- Source video: `Videos/movecycle-dreamina-2026-05-06.MP4`
- Source duration: `10.000s`
- Source size: `1280x720`
- Sampling basis: `30 fps`
- Ignored source samples: `0`, `1`
- Wizard analysis crop: `x=320, y=360, width=210, height=250`
- Selected cycle start sample: `198`
- Selected cycle end reference sample: `258`
- Saved source sample range: `198..257`
- Cycle period: `60` samples, `2.000s`
- Exact repeated samples removed: `201`, `206`, `211`, `216`, `221`, `226`, `231`, `236`, `241`, `245`, `251`, `256`
- Saved walk cycle frames: `48`
- Transparent cropped frames: `48`
- Transparent crop: `x=240, y=90, width=290, height=540`
- Front-24 crop-only frames: `24`
- Front-24 crop-only transparency: `none`, opaque background
- Cleaned 6x4 sheet: `1740x2160`, `6` columns, `4` rows
- Cleaned 6x4 cell size: `290x540`
- Alpha mask sheet: `wizard_front24_alpha_mask_exact.png`
- Transparent sheet: `wizard_front24_grid_6x4_transparent.png`
- Split transparent frames: `24`, under `split_frames`
- Black/white opacity preview sheet size: `1740x2160`
- Black/white opacity preview files:
  - `01_black_background.png`
  - `02_white_background.png`
  - `03_transparent_from_black_white.png`
  - `opacity_mask_from_black_white.png`
- Run-loop selection dump: `299` full-frame PNGs, source samples `2..300`
- Run-loop review sheets: `5`, under `run_full_frames/review_sheets`
- Run-loop crop candidate: output frames `111..126`, source samples `112..127`
- Run-loop crop rectangle: `x=200, y=65, width=380, height=590`
- Run-loop sheet: `4x4`, `1520x2360`

Samples `0` and `1` are excluded because their brightness differs from the
rest of the video.

## Environment Notes

At the time of extraction:

- `ffmpeg` was not available on `PATH`.
- `python`/`py` were not usable in the sandboxed shell.
- `.NET 10` was available.
- A temporary WPF/.NET extractor was created in:
  - `.codex-tmp/FrameExtractor/FrameExtractor.csproj`
  - `.codex-tmp/FrameExtractor/Program.cs`

The extractor uses Windows media decoding through WPF `MediaPlayer`, so this
workflow is Windows-specific.

## Build Command

Run from the repository root:

```powershell
New-Item -ItemType Directory -Force -Path .\.codex-tmp\dotnet-home, .\.codex-tmp\nuget

& {
  $env:DOTNET_CLI_HOME = (Resolve-Path .\.codex-tmp\dotnet-home).Path
  $env:NUGET_PACKAGES = (Resolve-Path .\.codex-tmp\nuget).Path
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
  dotnet build .\.codex-tmp\FrameExtractor\FrameExtractor.csproj -c Release
}
```

The custom `DOTNET_CLI_HOME` and `NUGET_PACKAGES` paths keep first-run and
package-cache writes inside the workspace.

## Wizard Walk Cycle Command

Run from the repository root:

```powershell
& {
  $env:DOTNET_CLI_HOME = (Resolve-Path .\.codex-tmp\dotnet-home).Path
  $env:NUGET_PACKAGES = (Resolve-Path .\.codex-tmp\nuget).Path
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

  dotnet .\.codex-tmp\FrameExtractor\bin\Release\net10.0-windows\FrameExtractor.dll `
    --video .\Videos\movecycle-dreamina-2026-05-06.MP4 `
    --out .\Videos\movecycle-dreamina-2026-05-06_wizard_walk_cycle `
    --sample-fps 30 `
    --min-stddev 2.0 `
    --seek-delay-ms 140 `
    --analyze-cycle `
    --crop 320,360,210,250 `
    --start-sample 2 `
    --min-period 55 `
    --max-period 65 `
    --exact-duplicate-threshold 0.25
}
```

Important settings:

- `--start-sample 2` keeps source samples `0` and `1` out of the analysis.
- `--crop 320,360,210,250` focuses the scoring on the purple wizard's leg and
  foot motion.
- `--min-period 55 --max-period 65` forces a full two-step walk cycle instead
  of selecting a shorter half-cycle.
- `--exact-duplicate-threshold 0.25` removes exact repeated frames caused by
  the video sampling cadence.

## Output Files

The output directory contains:

- `walk_####_sample_#####_t####.###s.png`
  - Sequential walk cycle frame.
  - File name includes output index, source sample index, and timestamp.
- `manifest.csv`
  - Per-frame metadata:
    - output file
    - source sample index
    - timestamp in seconds
    - duplicate score against the previous kept frame
- `cycle_candidates.csv`
  - Top detected cycle candidates.
- `summary.txt`
  - Run-level extraction settings and final counts.

## Transparent Crop Command

The transparent crop pass uses the selected wizard walk cycle frames as input.
It crops a fixed wizard rectangle, then flood-fills only border-connected
background-like pixels and converts them to transparent or soft-alpha pixels.

```powershell
& {
  $env:DOTNET_CLI_HOME = (Resolve-Path .\.codex-tmp\dotnet-home).Path
  $env:NUGET_PACKAGES = (Resolve-Path .\.codex-tmp\nuget).Path
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

  dotnet .\.codex-tmp\TransparentCropper\bin\Release\net10.0-windows\TransparentCropper.dll `
    --input .\Videos\movecycle-dreamina-2026-05-06_wizard_walk_cycle `
    --out .\Videos\movecycle-dreamina-2026-05-06_wizard_walk_cycle_cropped_transparent `
    --crop 240,90,290,540 `
    --tolerance 52 `
    --soft-alpha-start 24 `
    --prefix walk_
}
```

The output files are named:

```text
wizard_####_sample_#####_t####.###s.png
```

## Front-24 Crop-Only Candidate

The front-24 loop candidate uses only `walk_0001` through `walk_0024` from the
full wizard walk cycle. It uses the same crop rectangle as the transparent
crop, but does not remove the background.

```text
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_front24_cropped
```

Settings:

- Source: first `24` PNG frames from `movecycle-dreamina-2026-05-06_wizard_walk_cycle`
- Crop: `x=240, y=90, width=290, height=540`
- Transparency: none
- Output size: `290x540`
- Output frames: `24`

The output files are named:

```text
wizard_front24_####_sample_#####_t####.###s.png
```

## Cleaned 6x4 Bleed Sheet

The cleaned sheet preserves the exact `6x4` grid and `24` source frames. Edge
fragments from neighboring characters are removed when they touch the crop
edge, then replaced with a sampled cream bleed background color.

```text
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_front24_grid_6x4_cleaned_bleed/wizard_front24_grid_6x4_cleaned_bleed.png
```

Output:

- Columns: `6`
- Rows: `4`
- Cell size: `290x540`
- Sheet size: `1740x2160`
- Transparency: none
- Background fill: sampled edge bleed color, typically around RGB `226,219,206`

## Alpha Mask And Split Output

The imagegen mask attempt was saved for traceability:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_front24_alpha_split/imagegen_alpha_mask_attempt_unaligned.png
```

That generated mask did not preserve the exact source dimensions and grid, so
the production alpha mask was generated in code from the cleaned `1740x2160`
sheet to keep the sprite order and cell boundaries exact.

Production files:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_front24_alpha_split/wizard_front24_alpha_mask_exact.png
Videos/movecycle-dreamina-2026-05-06_wizard_front24_alpha_split/wizard_front24_grid_6x4_transparent.png
Videos/movecycle-dreamina-2026-05-06_wizard_front24_alpha_split/split_frames/
```

Split output:

- Columns: `6`
- Rows: `4`
- Cell size: `290x540`
- Transparent sheet size: `1740x2160`
- Split frame count: `24`
- Split order: left-to-right, top-to-bottom
- Split file names: `wizard_front24_alpha_####.png`

## Black/White Opacity Preview

This pass starts from the transparent `6x4` sheet and renders two opaque
versions over pure black and pure white backgrounds. It then reconstructs
opacity from the color difference between those two renders.

Input:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_front24_alpha_split/wizard_front24_grid_6x4_transparent.png
```

Command:

```powershell
& {
  $env:DOTNET_CLI_HOME = (Resolve-Path .\.codex-tmp\dotnet-home).Path
  $env:NUGET_PACKAGES = (Resolve-Path .\.codex-tmp\nuget).Path
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

  dotnet .\.codex-tmp\BwOpacityPreview\bin\Release\net10.0-windows\BwOpacityPreview.dll `
    .\Videos\movecycle-dreamina-2026-05-06_wizard_front24_alpha_split\wizard_front24_grid_6x4_transparent.png `
    .\Videos\movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview
}
```

Production files:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview/01_black_background.png
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview/02_white_background.png
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview/03_transparent_from_black_white.png
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview/opacity_mask_from_black_white.png
```

Direct split outputs:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview/split_black/
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview/split_white/
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview/split_transparent/
```

Each split directory contains `24` PNG frames. The split is a direct `6x4`
grid crop only; it does not change color, opacity, masking, or edge cleanup.

Alpha reconstruction:

```text
alpha = 255 - average(white_rgb - black_rgb)
```

Direct split command example:

```powershell
& {
  $env:DOTNET_CLI_HOME = (Resolve-Path .\.codex-tmp\dotnet-home).Path
  $env:NUGET_PACKAGES = (Resolve-Path .\.codex-tmp\nuget).Path
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

  dotnet .\.codex-tmp\GridSplitter\bin\Release\net10.0-windows\GridSplitter.dll `
    --input .\Videos\movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview\03_transparent_from_black_white.png `
    --out .\Videos\movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview\split_transparent `
    --prefix wizard_front24_transparent `
    --columns 6 `
    --rows 4 `
    --cell-width 290 `
    --cell-height 540
}
```

## General Extraction Notes

## Full Frame Dump For Run Loop Selection

This pass extracts every sampled full-frame image so a run loop range can be
chosen manually. It does not remove duplicate-looking poses.

Output:

```text
Videos/movecycle-dreamina-2026-05-06_run_full_frames
```

Command:

```powershell
& {
  $env:DOTNET_CLI_HOME = (Resolve-Path .\.codex-tmp\dotnet-home).Path
  $env:NUGET_PACKAGES = (Resolve-Path .\.codex-tmp\nuget).Path
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

  dotnet .\.codex-tmp\FrameExtractor\bin\Release\net10.0-windows\FrameExtractor.dll `
    --video .\Videos\movecycle-dreamina-2026-05-06.MP4 `
    --out .\Videos\movecycle-dreamina-2026-05-06_run_full_frames `
    --sample-fps 30 `
    --threshold -1 `
    --min-stddev 2.0 `
    --seek-delay-ms 140 `
    --start-sample 2
}
```

Settings and result:

- Samples `0` and `1` are ignored.
- Source sample range: `2..300`
- Saved frames: `299`
- Output file pattern: `unique_####_sample_#####_t####.###s.png`
- `manifest.csv` maps output frame number to source sample and timestamp.
- `review_sheets/` contains 60-frame thumbnail sheets for visual range picking.

## Run 111-126 Wizard Crop Sheet

After manually keeping only output frames `111..126`, the second purple wizard
was cropped from each remaining full-frame PNG and merged into a `4x4` sheet.

Output:

```text
Videos/movecycle-dreamina-2026-05-06_run_111_126_wizard_crop_4x4
```

Settings:

- Source files: `unique_0111...png` through `unique_0126...png`
- Source samples: `112..127`
- Crop: `x=200, y=65, width=380, height=590`
- Individual cropped frames: `16`
- Sheet columns: `4`
- Sheet rows: `4`
- Sheet size: `1520x2360`
- Sheet order: left-to-right, top-to-bottom
- Sheet file: `wizard_run_111_126_grid_4x4.png`

## General Extraction Notes

For a manually selected source sample range, use:

```powershell
dotnet .\.codex-tmp\FrameExtractor\bin\Release\net10.0-windows\FrameExtractor.dll `
  --video .\Videos\YOUR_VIDEO.mp4 `
  --out .\Videos\YOUR_OUTPUT_FOLDER `
  --sample-fps 30 `
  --threshold -1 `
  --min-stddev 2.0 `
  --seek-delay-ms 140 `
  --start-sample 2 `
  --end-sample 60
```

Use `--threshold -1` to keep every sampled frame in the selected range.

## Cleanup Policy

Intermediate image result folders should be removed after choosing a final
cycle. The current retained image outputs are:

```text
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_cropped_transparent
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_front24_cropped
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_front24_grid_6x4
Videos/movecycle-dreamina-2026-05-06_wizard_walk_cycle_front24_grid_6x4_cleaned_bleed
Videos/movecycle-dreamina-2026-05-06_wizard_front24_alpha_split
Videos/movecycle-dreamina-2026-05-06_wizard_front24_bw_opacity_preview
Videos/movecycle-dreamina-2026-05-06_run_full_frames
Videos/movecycle-dreamina-2026-05-06_run_111_126_wizard_crop_4x4
```

The source video and this workflow document are retained.

## Unity Project Safety

This workflow does not require Unity scene changes.

- No Unity scenes were opened or regenerated.
- No `Assets` content was modified.
- Generated frame images are written under `Videos`.
- Temporary tool files are written under `.codex-tmp`.
