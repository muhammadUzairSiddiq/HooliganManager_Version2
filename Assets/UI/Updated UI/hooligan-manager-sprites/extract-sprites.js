/**
 * Hooligan Manager — sprite extraction + 4x upscale from collage mockup
 * Source layout: 2 cols x 3 rows @ 1024x576
 */
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const SRC =
  'C:/Users/ho/.cursor/projects/d-wndr/assets/c__Users_ho_AppData_Roaming_Cursor_User_workspaceStorage_383bf331bee017de5591a8ec675cff78_images_WhatsApp_Image_2026-09-05_at_10.01.00_AM-f6c2affd-e165-4cb1-b543-9ef891bfd634.jpg';
const ROOT = __dirname;
const SCALE = 4; // 1024→4096 equivalent crop upscale

const dirs = ['backgrounds', 'panels', 'buttons', 'icons', 'popups', 'screens', 'nav', '_work'];
for (const d of dirs) fs.mkdirSync(path.join(ROOT, d), { recursive: true });

async function cropUpscale(name, folder, left, top, width, height, scale = SCALE) {
  const out = path.join(ROOT, folder, `${name}.png`);
  await sharp(SRC)
    .extract({ left: Math.round(left), top: Math.round(top), width: Math.round(width), height: Math.round(height) })
    .resize(Math.round(width * scale), Math.round(height * scale), {
      kernel: sharp.kernel.lanczos3,
      fit: 'fill',
    })
    .png()
    .toFile(out);
  console.log('+', out);
  return out;
}

async function main() {
  const W = 1024;
  const H = 576;
  const cw = W / 2; // 512
  const ch = H / 3; // 192

  // --- 6 screens ---
  const screens = [
    ['screen_01_main_menu', 0, 0],
    ['screen_02_town', cw, 0],
    ['screen_03_missions', 0, ch],
    ['screen_04_squad', cw, ch],
    ['screen_05_tactical', 0, ch * 2],
    ['screen_06_victory', cw, ch * 2],
  ];
  for (const [name, x, y] of screens) {
    await cropUpscale(name, 'screens', x, y, cw, ch, SCALE);
  }

  // Screen-relative helpers (screen coords → absolute)
  const S = (col, row, lx, ly, lw, lh, name, folder, scale = SCALE) =>
    cropUpscale(name, folder, col * cw + lx, row * ch + ly, lw, lh, scale);

  // ========== SCREEN 1: Main Menu (col0,row0) ==========
  // Full BG (use full screen as BG reference)
  await S(0, 0, 0, 0, cw, ch, 'bg_main_menu_city', 'backgrounds');
  // Top resource bar strip
  await S(0, 0, 8, 4, 280, 22, 'panel_top_resource_bar', 'panels');
  // Logo area
  await S(0, 0, 200, 8, 280, 55, 'logo_hooligan_manager', 'icons', 3);
  // Menu buttons stack (keep as reference crops; blank versions generated separately)
  await S(0, 0, 18, 55, 130, 22, 'btn_ref_new_game_red', 'buttons', 4);
  await S(0, 0, 18, 80, 130, 18, 'btn_ref_continue', 'buttons', 4);
  await S(0, 0, 18, 100, 130, 18, 'btn_ref_load_game', 'buttons', 4);
  await S(0, 0, 18, 120, 130, 18, 'btn_ref_settings', 'buttons', 4);
  await S(0, 0, 18, 140, 130, 18, 'btn_ref_credits', 'buttons', 4);
  // Store button
  await S(0, 0, 455, 155, 45, 28, 'btn_ref_store', 'buttons', 4);

  // Resource icons from top bar
  await S(0, 0, 12, 6, 18, 16, 'icon_cash', 'icons', 6);
  await S(0, 0, 70, 6, 18, 16, 'icon_crew', 'icons', 6);
  await S(0, 0, 125, 6, 16, 16, 'icon_star', 'icons', 6);
  await S(0, 0, 175, 6, 16, 16, 'icon_shield', 'icons', 6);
  await S(0, 0, 225, 6, 16, 16, 'icon_energy', 'icons', 6);

  // ========== SCREEN 2: Town (col1,row0) ==========
  await S(1, 0, 0, 0, cw, ch, 'bg_town_isometric', 'backgrounds');
  await S(1, 0, 8, 28, 115, 100, 'panel_objectives', 'panels');
  await S(1, 0, 8, 130, 115, 35, 'panel_events', 'panels');
  // Bottom nav bar
  await S(1, 0, 130, 155, 250, 32, 'nav_bottom_bar', 'nav');
  await S(1, 0, 135, 157, 42, 28, 'nav_home', 'nav', 5);
  await S(1, 0, 182, 157, 42, 28, 'nav_attack', 'nav', 5);
  await S(1, 0, 230, 157, 42, 28, 'nav_squad', 'nav', 5);
  await S(1, 0, 278, 157, 42, 28, 'nav_research', 'nav', 5);
  await S(1, 0, 325, 157, 42, 28, 'nav_shop', 'nav', 5);
  // Matchday button
  await S(1, 0, 420, 120, 80, 65, 'btn_ref_matchday', 'buttons', 4);

  // ========== SCREEN 3: Missions (col0,row1) ==========
  await S(0, 1, 0, 0, cw, ch, 'bg_missions_ui', 'backgrounds');
  // Left sidebar tabs
  await S(0, 1, 4, 30, 36, 140, 'nav_sidebar_missions', 'nav');
  await S(0, 1, 6, 32, 32, 32, 'nav_tab_main_active', 'nav', 5);
  await S(0, 1, 6, 68, 32, 32, 'nav_tab_side', 'nav', 5);
  await S(0, 1, 6, 104, 32, 32, 'nav_tab_daily', 'nav', 5);
  await S(0, 1, 6, 140, 32, 28, 'nav_tab_events', 'nav', 5);
  // Mission cards
  await S(0, 1, 48, 28, 280, 40, 'panel_mission_card_1', 'panels');
  await S(0, 1, 48, 72, 280, 40, 'panel_mission_card_2', 'panels');
  await S(0, 1, 48, 116, 280, 40, 'panel_mission_card_3', 'panels');
  // Daily bonus popup
  await S(0, 1, 360, 20, 140, 160, 'popup_daily_bonus', 'popups');
  await S(0, 1, 375, 145, 110, 28, 'btn_ref_claim_green', 'buttons', 4);
  // Progress bar samples
  await S(0, 1, 100, 52, 120, 8, 'bar_progress_orange', 'panels', 6);
  await S(0, 1, 100, 96, 120, 8, 'bar_progress_green', 'panels', 6);

  // ========== SCREEN 4: Squad (col1,row1) ==========
  await S(1, 1, 0, 0, cw, ch, 'bg_squad_ui', 'backgrounds');
  await S(1, 1, 4, 30, 36, 140, 'nav_sidebar_squad', 'nav');
  // Character cards
  await S(1, 1, 55, 35, 70, 115, 'panel_char_card_1', 'panels');
  await S(1, 1, 135, 35, 70, 115, 'panel_char_card_2', 'panels');
  await S(1, 1, 215, 35, 70, 115, 'panel_char_card_3', 'panels');
  await S(1, 1, 295, 35, 70, 115, 'panel_char_card_4', 'panels');
  await S(1, 1, 375, 35, 70, 115, 'panel_char_card_5', 'panels');
  await S(1, 1, 120, 160, 280, 26, 'btn_ref_manage_squad', 'buttons', 4);

  // ========== SCREEN 5: Tactical (col0,row2) ==========
  await S(0, 2, 0, 0, cw, ch, 'bg_tactical_warehouse', 'backgrounds');
  // Squad portraits left
  await S(0, 2, 4, 40, 36, 120, 'panel_squad_portraits', 'panels');
  // Minimap
  await S(0, 2, 400, 8, 100, 70, 'panel_minimap', 'panels');
  // Ability buttons
  await S(0, 2, 160, 145, 40, 40, 'btn_ref_ability_fist', 'buttons', 5);
  await S(0, 2, 210, 145, 40, 40, 'btn_ref_ability_shield', 'buttons', 5);
  await S(0, 2, 260, 145, 40, 40, 'btn_ref_ability_bolt', 'buttons', 5);
  await S(0, 2, 310, 145, 40, 40, 'btn_ref_ability_crosshair', 'buttons', 5);
  // Timer / pause
  await S(0, 2, 400, 85, 90, 22, 'panel_timer_controls', 'panels', 5);

  // ========== SCREEN 6: Victory (col1,row2) ==========
  await S(1, 2, 0, 0, cw, ch, 'bg_victory', 'backgrounds');
  await S(1, 2, 180, 8, 150, 70, 'popup_victory_emblem', 'popups');
  await S(1, 2, 40, 70, 140, 70, 'panel_rewards', 'panels');
  await S(1, 2, 330, 70, 150, 70, 'panel_stats', 'panels');
  await S(1, 2, 40, 145, 100, 28, 'panel_loot_slots', 'panels', 4);
  await S(1, 2, 160, 155, 70, 28, 'btn_ref_replay', 'buttons', 4);
  await S(1, 2, 240, 155, 70, 28, 'btn_ref_home', 'buttons', 4);
  await S(1, 2, 330, 155, 140, 30, 'btn_ref_continue_green', 'buttons', 4);
  // Loot icons
  await S(1, 2, 42, 148, 24, 24, 'icon_loot_bat', 'icons', 6);
  await S(1, 2, 70, 148, 24, 24, 'icon_loot_medkit', 'icons', 6);
  await S(1, 2, 98, 148, 24, 24, 'icon_loot_battery', 'icons', 6);

  // --- Combined sprite sheets ---
  await buildSheets();
  console.log('\nDone. SCALE=', SCALE);
}

async function buildSheets() {
  // Collect blank-ready reference + category sheets via tiling
  const categories = [
    ['buttons', 'sheet_buttons_reference'],
    ['panels', 'sheet_panels'],
    ['icons', 'sheet_icons'],
    ['nav', 'sheet_nav'],
    ['popups', 'sheet_popups'],
    ['backgrounds', 'sheet_backgrounds'],
  ];

  for (const [folder, sheetName] of categories) {
    const dir = path.join(ROOT, folder);
    const files = fs
      .readdirSync(dir)
      .filter((f) => f.endsWith('.png'))
      .sort();
    if (!files.length) continue;

    // Normalize each to a cell size then tile
    const cells = [];
    let maxW = 0;
    let maxH = 0;
    for (const f of files) {
      const meta = await sharp(path.join(dir, f)).metadata();
      maxW = Math.max(maxW, meta.width);
      maxH = Math.max(maxH, meta.height);
    }
    // Cap cell for backgrounds (they're huge)
    const cellW = folder === 'backgrounds' ? Math.min(maxW, 1024) : Math.min(maxW, 512);
    const cellH = folder === 'backgrounds' ? Math.min(maxH, 384) : Math.min(maxH, 512);
    const cols = Math.min(files.length, folder === 'backgrounds' ? 2 : 4);
    const rows = Math.ceil(files.length / cols);

    const composites = [];
    for (let i = 0; i < files.length; i++) {
      const buf = await sharp(path.join(dir, files[i]))
        .resize(cellW, cellH, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
        .png()
        .toBuffer();
      composites.push({
        input: buf,
        left: (i % cols) * cellW,
        top: Math.floor(i / cols) * cellH,
      });
    }

    await sharp({
      create: {
        width: cols * cellW,
        height: rows * cellH,
        channels: 4,
        background: { r: 20, g: 20, b: 22, alpha: 1 },
      },
    })
      .composite(composites)
      .png()
      .toFile(path.join(ROOT, `${sheetName}.png`));
    console.log('sheet', sheetName, `${cols}x${rows}`, files.length, 'sprites');
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
