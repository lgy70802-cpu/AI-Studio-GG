# Fish sprites

Tool: built-in imagegen (imagegen skill), 2026-09-22. No external downloads.

Generation prompt:
Create one production sprite atlas PNG with genuinely transparent alpha background, 1536x1024. Exactly five separate photorealistic small tropical aquarium fish, all perfectly SIDE PROFILE facing RIGHT, entire tails and fins visible, no overlap, arranged in five evenly spaced horizontal rows, each fish centered horizontally with generous transparent margins. Top row: pearl white and pale blush angelfish with elegant fins. Second row: warm peach orange small anthias reef fish. Third row: silver pink translucent tetra. Fourth row: muted amber copper butterflyfish with subtle dark stripes. Fifth row: dusty rose long-finned small aquarium fish. Realistic scales and delicate translucent fins, soft overhead rosy warm white light, restrained saturation, elegant premium calm aquarium photography, isolated cutouts. No water, no bubbles, no shadows outside fish, no scenery, no text, no labels, no grids. Each row contains exactly one fish facing right; fish have realistic proportions, no cartoon or vector art. This single atlas will be used in a Canvas 2D aquarium plugin.

Refinement prompt:
Edit this fish sprite atlas: REMOVE ALL BACKGROUND completely including black field, all colored glow and gray haze. Output actual transparent alpha PNG with only the five fish and their fins. Preserve fish exactly and positions. No replacement background, no checkerboard baked in, no glow outside fish. True fully transparent empty pixels between and around isolated fish. Preserve delicate fins.

Saved source: assets/fish/atlas.png. Five runtime crops: fish01.png through fish05.png. Alpha verified by reading pixel data; preview tools may composite transparency differently. Raster crop rectangles are reproducible in tools/prepare-assets.cjs.
