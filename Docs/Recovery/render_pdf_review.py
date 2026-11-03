"""Render a recovery report into a new QA directory and numbered contact sheets."""
from pathlib import Path
from datetime import datetime
import argparse
import subprocess
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--last-page', type=int)
parser.add_argument('--label', default='final')
args = parser.parse_args()
folder = root / 'tmp/pdfs' / (f'v4-{args.label}-' + datetime.now().strftime('%Y%m%d-%H%M%S'))
folder.mkdir(parents=True)
poppler = Path(r'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\poppler\Library\bin\pdftoppm.exe')
command = [str(poppler), '-r', '100', '-png']
if args.last_page:
    command += ['-l', str(args.last_page)]
command += [str(root / 'output/pdf/Hooligan_Manager_Compiled_RTS_Recovery_Plan.pdf'), str(folder / 'page')]
subprocess.run(command, check=True)
pages = sorted(folder.glob('page-*.png'))
for start in range(0, len(pages), 16):
    sheet = Image.new('RGB', (1160, 1696), '#dce5eb')
    draw = ImageDraw.Draw(sheet)
    for index, page in enumerate(pages[start:start + 16]):
        with Image.open(page) as image:
            image.thumbnail((274, 394))
            x, y = 8 + (index % 4) * 290, 22 + (index // 4) * 424
            sheet.paste(image, (x, y))
            draw.text((x, y - 17), f'PAGE {int(page.stem.split("-")[-1])}', fill='#183040')
    sheet.save(folder / f'contact-{start // 16 + 1:02d}.jpg', quality=90)
print(f'Rendered {len(pages)} pages into {folder}')
