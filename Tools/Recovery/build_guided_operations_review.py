"""Compile the 05-Nov guided-operations review and unchanged historical appendix.

Supply a reviewed evidence JSON after current native reports/captures finish.
Without --final the output is explicitly a draft; --final verifies every pinned
report/capture hash and completion state. A final document is not device/store
certification and does not require pretending an old APK validates new source.
"""
from pathlib import Path
from datetime import datetime, timezone
from xml.sax.saxutils import escape
import argparse
import hashlib
import json
import re

from PIL import Image as PILImage
from pypdf import PdfReader, PdfWriter
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (SimpleDocTemplate, Paragraph, Spacer, PageBreak,
                               Image, LongTable, TableStyle, KeepTogether, CondPageBreak)
from review_evidence import validate_completed_report

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'output/pdf/Hooligan_Manager_Guided_Operations_Compiled_Review.pdf'
OLD = ROOT / 'output/pdf/Hooligan_Manager_Compiled_RTS_Recovery_Plan.pdf'
SOURCE = ROOT / 'Docs/Recovery/Guided_Operations_Review_Source.md'
DEFAULT_EVIDENCE = ROOT / 'Docs/Recovery/guided_review_evidence.json'
TMP = ROOT / 'tmp/pdfs/guided-operations-review'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def markup(text):
    text = escape(text)
    text = re.sub(r'\[([^\]]+)\]\((https?://[^\s)]+)\)', r'<link href="\2" color="#087F72"><u>\1</u></link>', text)
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'`([^`]+)`', r'<font name="Courier">\1</font>', text)
    return text


def inspect_report(item, final):
    path = ROOT / item['path']
    if not path.is_file():
        if final and item.get('required', True):
            raise ValueError('Required current report missing: ' + item['path'])
        return {'status': 'PENDING', 'passes': 0, 'failures': None, 'path': item['path']}
    text = path.read_text(encoding='utf-8-sig')
    good, detail = validate_completed_report(text, item.get('minimum_pass_only', 0))
    passes = len(re.findall(r'(?m)^PASS\s+\S', text))
    failures = len(re.findall(r'(?m)^FAIL\b', text))
    pin_matches = bool(item.get('sha256')) and item['sha256'].lower() == sha(path)
    current = item.get('current') is True and pin_matches
    accepted = good and current
    if final and item.get('required', True) and not accepted:
        raise ValueError(f"Unverified current evidence {item['path']}: {detail}; pinned_current={current}")
    return {'status': 'PASS' if accepted else 'FAILED' if not good else 'HISTORICAL / UNCONFIRMED',
            'passes': passes, 'failures': failures, 'detail': detail, 'path': item['path'],
            'sha256': sha(path), 'pinned_current': current}


def build(evidence_file, final=False):
    spec = json.loads(evidence_file.read_text(encoding='utf-8-sig')) if evidence_file.is_file() else {}
    if final and spec.get('current_evidence_reviewed') is not True:
        raise ValueError('Final rendering requires an explicitly reviewed, hash-pinned current evidence specification.')
    evidence = {item['key']: inspect_report(item, final) for item in spec.get('reports', [])}
    if final and not evidence:
        raise ValueError('No current native evidence supplied.')
    captures = spec.get('captures', {})
    used_images = {}
    old_hash = sha(OLD)
    old_reader = PdfReader(OLD)
    TMP.mkdir(parents=True, exist_ok=True)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    front = TMP / 'guided-operations-front.pdf'
    width, height = landscape(A4)
    page_width = width - 80
    ink, teal, muted = colors.HexColor('#15262C'), colors.HexColor('#087F72'), colors.HexColor('#52676D')
    styles = {
        'body': ParagraphStyle('Body', fontName='Helvetica', fontSize=10.4, leading=14.2, spaceAfter=8, textColor=ink),
        'h1': ParagraphStyle('Chapter', fontName='Helvetica-Bold', fontSize=22, leading=27, spaceAfter=15, textColor=ink, keepWithNext=True),
        'h2': ParagraphStyle('Subheading', fontName='Helvetica-Bold', fontSize=13, leading=17, spaceBefore=9, spaceAfter=7, textColor=teal, keepWithNext=True),
        'caption': ParagraphStyle('Caption', fontName='Helvetica', fontSize=9.2, leading=12, spaceAfter=12, textColor=muted),
        'table': ParagraphStyle('Cell', fontName='Helvetica', fontSize=9.3, leading=12.3, textColor=ink),
    }
    state = 'LOCAL IMPLEMENTATION REVIEW' if final else 'DRAFT - CURRENT VERIFICATION PENDING'

    def decorate(canvas, doc):
        canvas.saveState()
        canvas.setFillColor(ink); canvas.rect(0, height-33, width, 33, fill=1, stroke=0)
        canvas.setFillColor(colors.white); canvas.setFont('Helvetica-Bold', 8.2)
        canvas.drawString(40, height-21, 'HOOLIGAN MANAGER / GUIDED OPERATIONS')
        canvas.setFont('Helvetica', 8); canvas.drawRightString(width-40, height-21, '05 NOVEMBER 2026 | ' + state)
        canvas.setStrokeColor(colors.HexColor('#CCDCDA')); canvas.line(40, 31, width-40, 31)
        canvas.setFont('Helvetica', 7.8); canvas.setFillColor(muted)
        canvas.drawString(40, 18, 'CURRENT REVIEW | Native evidence is scoped; device, store and client acceptance remain open')
        canvas.drawRightString(width-40, 18, str(doc.page))
        canvas.restoreState()

    def table(rows):
        columns = len(rows[0]); widths = [page_width / columns] * columns
        cells = [[Paragraph(markup(cell), styles['table']) for cell in row] for row in rows]
        value = LongTable(cells, colWidths=widths, repeatRows=1, hAlign='LEFT')
        value.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#DDEEEA')),
            ('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.HexColor('#F1F7F5'),colors.white]),
            ('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),9),
            ('RIGHTPADDING',(0,0),(-1,-1),9),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7)]))
        return value

    def screenshot(key):
        item = captures.get(key)
        if not item:
            if final:
                raise ValueError('Current capture is not pinned in evidence JSON: ' + key)
            return [Paragraph('CURRENT CAPTURE PENDING: ' + escape(key), styles['caption'])]
        path = ROOT / item['path']
        valid = path.is_file() and item.get('current') is True and item.get('sha256', '').lower() == sha(path)
        if not valid:
            if final:
                raise ValueError('Capture is missing, stale or unconfirmed: ' + item['path'])
            return [Paragraph('CAPTURE UNCONFIRMED: ' + escape(item['path']), styles['caption'])]
        used_images[item['path']] = sha(path)
        with PILImage.open(path) as im:
            iw, ih = im.size
        scale = min(page_width/iw, 345/ih)
        image = Image(str(path), width=iw*scale, height=ih*scale)
        image.hAlign = 'CENTER'
        return [KeepTogether([image, Spacer(1,6), Paragraph(markup(item.get('caption', key)), styles['caption'])])]

    flow = []
    lines = SOURCE.read_text(encoding='utf-8-sig').splitlines()
    pending = []
    def flush():
        if pending:
            flow.append(Paragraph(markup(' '.join(pending)), styles['body'])); pending.clear()
    index = 0; chapters = 0
    while index < len(lines):
        line = lines[index].strip(); index += 1
        if not line:
            flush(); continue
        if line.startswith('# '):
            flush()
            if chapters: flow.append(CondPageBreak(190))
            chapters += 1; flow.append(Paragraph(markup(line[2:]), styles['h1'])); continue
        if line.startswith('## '):
            flush(); flow.append(Paragraph(markup(line[3:]), styles['h2'])); continue
        if line.startswith('[[IMAGE:'):
            flush(); flow.extend(screenshot(line[8:-2])); continue
        if line == '[[EVIDENCE]]':
            flush(); rows = [['Current check', 'Observed state', 'Evidence report']]
            for item in spec.get('reports', []):
                record = evidence[item['key']]
                observed = f"{record['passes']} PASS / {record['failures']} FAIL" if record['status']=='PASS' else record['status']
                rows.append([item.get('label', item['key']), observed, item['path']])
            if len(rows)==1: rows.append(['Current native evidence', 'PENDING - no reviewed evidence specification', 'Await final reports from integration'])
            flow.append(table(rows)); flow.append(Spacer(1,12)); continue
        if line == '[[PACKAGE]]':
            flush(); package = spec.get('package') or {}
            if package.get('current') is True:
                artifact = ROOT / package['path']
                if not artifact.is_file() or package.get('sha256','').lower()!=sha(artifact):
                    raise ValueError('Claimed current package hash is not verified.')
                text = f"Current local review package: {package['path']}. Actual bytes: {artifact.stat().st_size:,}. SHA-256: {sha(artifact)}. " + package.get('verification', 'Independent package report must be consulted separately.')
            else:
                text = 'No current guided-operations Android package is verified in this document. The earlier recovery/tracking APKs are historical source cuts; neither certifies the current changes. The document may still be delivered as a native-editor review with this packaging gate open.'
            flow.append(Paragraph(markup(text), styles['body'])); continue
        if line.startswith('|'):
            flush(); rows = []
            while True:
                cells = [cell.strip() for cell in line.strip('|').split('|')]
                if not all(re.fullmatch(r':?-+:?', cell.replace(' ','')) for cell in cells): rows.append(cells)
                if index>=len(lines) or not lines[index].strip().startswith('|'): break
                line = lines[index].strip(); index += 1
            flow.append(table(rows)); flow.append(Spacer(1,10)); continue
        if line.startswith('- '):
            flush(); flow.append(Paragraph(markup(line[2:]), styles['body'], bulletText='-')); continue
        pending.append(line)
    flush()
    flow.append(PageBreak()); flow.append(Paragraph('Historical evidence begins on the next page', styles['h1']))
    flow.append(Paragraph(f'The following {len(old_reader.pages)} pages reproduce the earlier compiled recovery plan. Its dates, issue states, tests and design claims are historical. Current verified evidence in this front review supersedes conflicting observations. All original client-source appendices remain included.', styles['body']))
    flow.append(Paragraph('Historical source SHA-256: ' + old_hash, styles['caption']))
    doc = SimpleDocTemplate(str(front), pagesize=(width,height), leftMargin=40, rightMargin=40, topMargin=53, bottomMargin=43,
                            title='Hooligan Manager - Guided Operations Review', author='Hooligan Manager project review')
    doc.build(flow, onFirstPage=decorate, onLaterPages=decorate)
    front_reader = PdfReader(front)
    writer = PdfWriter(); writer.append(str(front), outline_item='05 November - current guided operations review')
    writer.append(str(OLD), outline_item='HISTORICAL - earlier compiled recovery plan and supplied sources')
    writer.add_metadata({'/Title':'Hooligan Manager - Guided Operations Compiled Review' + ('' if final else ' - DRAFT'),
                         '/Subject':'Current review followed by unchanged historical source evidence; no production/device certification'})
    with OUTPUT.open('wb') as target: writer.write(target)
    combined = PdfReader(OUTPUT)
    assert sha(OLD)==old_hash
    assert len(combined.pages)==len(front_reader.pages)+len(old_reader.pages)
    assert combined.pages[len(front_reader.pages)].extract_text()==old_reader.pages[0].extract_text()
    manifest = {'created_utc':datetime.now(timezone.utc).isoformat(),'draft':not final,'date':'2026-11-05',
        'output':str(OUTPUT),'output_sha256':sha(OUTPUT),'current_review_pages':len(front_reader.pages),
        'historical_pages':len(old_reader.pages),'total_pages':len(combined.pages),
        'historical_source_sha256':old_hash,'historical_source_unchanged':True,'source_sha256':sha(SOURCE),
        'evidence_spec_sha256':sha(evidence_file) if evidence_file.is_file() else None,'reports':evidence,'images_sha256':used_images,
        'physical_device_validation':'not performed','production_release_certified':False}
    (OUTPUT.parent/'guided_operations_review_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print(json.dumps({key:manifest[key] for key in ('draft','output','current_review_pages','historical_pages','total_pages','output_sha256')},indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence',type=Path,default=DEFAULT_EVIDENCE)
    parser.add_argument('--final',action='store_true',help='Require reviewed, hash-pinned current reports and captures; not store certification')
    args = parser.parse_args()
    build(args.evidence,args.final)
