/**
 * Build blank (no-text) reusable sprite sheet + trim padding from blanks
 */
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const ROOT = __dirname;

async function trimAndCopy(src, dest, pad = 8) {
  await sharp(src)
    .trim({ threshold: 15 })
    .extend({ top: pad, bottom: pad, left: pad, right: pad, background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toFile(dest);
}

async function main() {
  // Copy any remaining blanks from cursor assets folder
  const cursorAssets = 'C:/Users/ho/.cursor/projects/d-wndr/assets';
  const extras = [
    ['btn_nav_square_blank.png', 'buttons/blank'],
    ['panel_mission_card_blank.png', 'panels/blank'],
    ['panel_resource_bar_blank.png', 'panels/blank'],
    ['popup_victory_emblem_blank.png', 'popups/blank'],
  ];
  for (const [f, folder] of extras) {
    const from = path.join(cursorAssets, f);
    const toDir = path.join(ROOT, folder);
    fs.mkdirSync(toDir, { recursive: true });
    if (fs.existsSync(from)) {
      fs.copyFileSync(from, path.join(toDir, f));
      console.log('copied', f);
    }
  }

  // Trim blanks for cleaner sprites
  const blankDirs = ['buttons/blank', 'panels/blank', 'popups/blank'];
  const trimmedDir = path.join(ROOT, 'buttons', 'blank_trimmed');
  fs.mkdirSync(trimmedDir, { recursive: true });

  const blankButtons = fs.readdirSync(path.join(ROOT, 'buttons', 'blank')).filter((f) => f.endsWith('.png'));
  for (const f of blankButtons) {
    await trimAndCopy(path.join(ROOT, 'buttons', 'blank', f), path.join(trimmedDir, f), 12);
    console.log('trimmed', f);
  }

  // Also trim panels/popups into their own trimmed folders
  for (const rel of ['panels/blank', 'popups/blank', 'backgrounds/clean']) {
    const dir = path.join(ROOT, rel);
    if (!fs.existsSync(dir)) continue;
    const out = path.join(ROOT, rel + '_trimmed');
    fs.mkdirSync(out, { recursive: true });
    for (const f of fs.readdirSync(dir).filter((x) => x.endsWith('.png'))) {
      if (rel.includes('backgrounds')) {
        // Don't trim backgrounds aggressively — just copy hi-res
        await sharp(path.join(dir, f)).png().toFile(path.join(out, f));
      } else {
        await trimAndCopy(path.join(dir, f), path.join(out, f), 10);
      }
      console.log('ready', rel, f);
    }
  }

  // Build blank buttons sprite sheet
  const files = fs.readdirSync(trimmedDir).filter((f) => f.endsWith('.png')).sort();
  const cellW = 512;
  const cellH = 256;
  const cols = 2;
  const rows = Math.ceil(files.length / cols);
  const composites = [];
  for (let i = 0; i < files.length; i++) {
    const buf = await sharp(path.join(trimmedDir, files[i]))
      .resize(cellW - 24, cellH - 24, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
      .extend({ top: 12, bottom: 12, left: 12, right: 12, background: { r: 0, g: 0, b: 0, alpha: 0 } })
      .png()
      .toBuffer();
    composites.push({ input: buf, left: (i % cols) * cellW, top: Math.floor(i / cols) * cellH });
  }
  await sharp({
    create: {
      width: cols * cellW,
      height: rows * cellH,
      channels: 4,
      background: { r: 16, g: 16, b: 18, alpha: 1 },
    },
  })
    .composite(composites)
    .png()
    .toFile(path.join(ROOT, 'sheet_buttons_BLANK.png'));
  console.log('sheet_buttons_BLANK.png', files.length, 'buttons');

  // Build blank panels sheet
  const panelDir = path.join(ROOT, 'panels', 'blank_trimmed');
  if (fs.existsSync(panelDir)) {
    const pf = fs.readdirSync(panelDir).filter((f) => f.endsWith('.png')).sort();
    const pCellW = 640;
    const pCellH = 360;
    const pCols = 2;
    const pRows = Math.ceil(pf.length / pCols);
    const pComp = [];
    for (let i = 0; i < pf.length; i++) {
      const buf = await sharp(path.join(panelDir, pf[i]))
        .resize(pCellW - 20, pCellH - 20, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
        .extend({ top: 10, bottom: 10, left: 10, right: 10, background: { r: 0, g: 0, b: 0, alpha: 0 } })
        .png()
        .toBuffer();
      pComp.push({ input: buf, left: (i % pCols) * pCellW, top: Math.floor(i / pCols) * pCellH });
    }
    await sharp({
      create: {
        width: pCols * pCellW,
        height: pRows * pCellH,
        channels: 4,
        background: { r: 16, g: 16, b: 18, alpha: 1 },
      },
    })
      .composite(pComp)
      .png()
      .toFile(path.join(ROOT, 'sheet_panels_BLANK.png'));
    console.log('sheet_panels_BLANK.png', pf.length, 'panels');
  }

  // Manifest
  const walk = (dir, list = []) => {
    for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
      const p = path.join(dir, ent.name);
      if (ent.isDirectory()) walk(p, list);
      else if (ent.name.endsWith('.png')) list.push(path.relative(ROOT, p).replace(/\\/g, '/'));
    }
    return list;
  };
  const all = walk(ROOT).sort();
  const manifest = {
    source: 'Hooligan Manager UI mockup collage (1024x576 WhatsApp)',
    upscale: '4x Lanczos3 for crops; AI hi-res blanks/BGs',
    note: 'Use buttons/blank* and panels/blank* for reusable no-text UI. *_ref* crops retain original text for style reference only.',
    files: all,
    counts: {
      total: all.length,
      blank_buttons: blankButtons.length,
      screens: 6,
    },
  };
  fs.writeFileSync(path.join(ROOT, 'MANIFEST.json'), JSON.stringify(manifest, null, 2));
  console.log('MANIFEST.json written,', all.length, 'pngs');
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
