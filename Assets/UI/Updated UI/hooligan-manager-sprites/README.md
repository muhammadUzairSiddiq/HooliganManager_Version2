# Hooligan Manager — UI Sprite Pack

Source collage: `1024×576` WhatsApp mockup → crops upscaled **4× Lanczos**, plus **AI hi-res blank templates** (no text) for reuse.

## Use these first (production-ready blanks)

| Folder | What |
|--------|------|
| `buttons/blank_trimmed/` | **No-text** buttons — red, green, dark secondary, outline, ability circle, matchday circle, nav square |
| `panels/blank_trimmed/` | Empty panels — dark frame, mission card, character card, resource bar, progress bars |
| `popups/blank_trimmed/` | Tall daily popup frame, victory emblem (empty shield) |
| `backgrounds/clean/` | UI-free BGs — main menu city, town isometric, tactical warehouse |

### Blank button sprites (overlay your own labels)

- `btn_primary_red_blank.png` — NEW GAME style
- `btn_primary_green_blank.png` — CLAIM / CONTINUE style
- `btn_secondary_dark_blank.png` — translucent menu button
- `btn_outline_dark_blank.png` — REPLAY / HOME style
- `btn_ability_circle_blank.png` — combat ability frame
- `btn_matchday_circle_blank.png` — large circular CTA
- `btn_nav_square_blank.png` — bottom nav tab

## Reference crops (keep original look; may include text)

| Folder | What |
|--------|------|
| `screens/` | 6 full screens @ 4× |
| `buttons/` (`btn_ref_*`) | Original cropped buttons **with text** — style reference only |
| `panels/` | Mission cards, char cards, rewards, minimap, etc. |
| `icons/` | Cash, crew, star, shield, energy, loot, logo |
| `nav/` | Bottom bar + sidebar tabs |
| `popups/` | Daily bonus + victory emblem (with content) |
| `backgrounds/` | Full-screen crops (still include HUD — prefer `clean/`) |

## Sprite sheets

| File | Contents |
|------|----------|
| `sheet_buttons_BLANK.png` | All blank reusable buttons |
| `sheet_panels_BLANK.png` | All blank panels |
| `sheet_buttons_reference.png` | Textual button crops |
| `sheet_panels.png` / `sheet_icons.png` / `sheet_nav.png` / `sheet_popups.png` / `sheet_backgrounds.png` | Category atlases |

## Rebuild

```bash
cd assets/hooligan-manager-sprites
node extract-sprites.js   # re-crop from source
node organize-blanks.js   # trim blanks + rebuild blank sheets
```

See `MANIFEST.json` for the full file list.
