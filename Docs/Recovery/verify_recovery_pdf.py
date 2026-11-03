from pathlib import Path
import hashlib
import json
import re
from pypdf import PdfReader
import pdfplumber

root=Path(__file__).resolve().parents[2]
out=root/'output/pdf'
manifest=json.loads((out/'recovery_pdf_manifest.json').read_text())
pdf=Path(manifest['path'])
reader=PdfReader(pdf)
checks=[]
def check(ok,label):
    checks.append(('PASS ' if ok else 'FAIL ')+label)

check(len(reader.pages)==manifest['pages'],'Final page count matches manifest')
body_text='\n'.join((page.extract_text() or '') for page in reader.pages[:manifest['body_pages']])
empty_pages=[]
for number,page in enumerate(reader.pages[:manifest['body_pages']],1):
    content=page.extract_text() or ''
    content=re.sub(r'^HOOLIGAN MANAGER\s*/\s*RTS RECOVERY\s*$','',content,flags=re.M)
    content=re.sub(r'^\d{2} [A-Z]{3} \d{4}\s*\|\s*DESIGN \+ IMPLEMENTATION EVIDENCE\s*$','',content,flags=re.M)
    content=re.sub(r'^\d+\s*$','',content,flags=re.M)
    if not content.strip():empty_pages.append(number)
check(not empty_pages,'No blank generated main-report pages')
check('[[FIGURE:' not in body_text and '[[IMPLEMENTATION_STATUS]]' not in body_text,'No unresolved template tokens')
delivery=json.loads((root/'Artifacts/Recovery/review-delivery-manifest.json').read_text(encoding='utf-8'))
check(str(delivery['apk_bytes']) in body_text,'PDF includes the current APK byte size')
check(delivery['apk_sha256'] in re.sub(r'\s+','',body_text),'PDF includes the current APK SHA256 identity')
for label,report_path in [
    ('Home','Artifacts/Recovery/redesign-playtest.txt'),
    ('Police','Artifacts/Recovery/police-playtest.txt'),
    ('RTS','Artifacts/CityQA/rts-controls-playtest.txt'),
    ('Ads','Artifacts/Recovery/ads-checks.txt'),
    ('Strategy','Artifacts/Recovery/production-strategy-playtest.txt'),
    ('Vehicle','Artifacts/Recovery/police-vehicle-playtest.txt'),
    ('Commerce','Artifacts/Recovery/commerce-playtest.txt'),
    ('Purchases','Artifacts/Recovery/purchases-checks.txt')]:
    report=(root/report_path).read_text(encoding='utf-8-sig')
    result=re.search(r'(?:RESULT|Result:)\s*(\d+) passed, (\d+) failed',report,re.I)
    passed=len(re.findall(r'^PASS ',report,re.M));failed=len(re.findall(r'^FAIL ',report,re.M))
    consistent=not result or (int(result.group(1))==passed and int(result.group(2))==failed)
    check(passed>0 and failed==0 and consistent,f'Current {label} source report passed')
    expected=f'{passed} passed, {failed} failed'
    check(expected in body_text,f'PDF includes current {label} test counts')
check(all(f'E{i:02d}' in body_text for i in range(1,31)),'All thirty expanded acceptance cases included')
check('ENTERING HOME TERRITORY' in body_text,'Inherited source caption correction included')
check(all(f'R{i:02d}' in body_text for i in range(1,31)),'All 30 issue IDs appear')
check(all(f'T{i:02d}' in body_text for i in range(1,29)),'All 28 acceptance IDs appear')
offset=manifest['body_pages'];images=0
for name,path in [('A',root/'Docs/Recovery/Sources/Phase_2_Cross_Platform_Development_Proposal.pdf'),('B',root/'Docs/Recovery/Sources/Hooligan_Manager_RTS_Recovery_Master_Specification.pdf')]:
    source=PdfReader(path)
    for index,page in enumerate(source.pages):
        actual=reader.pages[offset+index]
        normalize=lambda text:re.sub(r'\s+',' ',text or '').strip()
        check(normalize(page.extract_text())==normalize(actual.extract_text()),f'Appendix {name} page {index+1}: source text preserved')
        expected=[hashlib.sha256(im.data).hexdigest() for im in page.images]
        copied=[hashlib.sha256(im.data).hexdigest() for im in actual.images]
        check(expected==copied,f'Appendix {name} page {index+1}: all source images preserved')
        images+=len(expected)
    offset+=len(source.pages)
check(images==9,'All nine embedded source image objects preserved')
provenance=json.loads((root/'Docs/Recovery/Sources/source_evidence_manifest.json').read_text(encoding='utf-8-sig'))
for item in provenance['sources']:
    check(hashlib.sha256((root/item['copy']).read_bytes()).hexdigest().lower()==item['sha256'].lower(),'Portable source copy matches original SHA256: '+Path(item['copy']).name)
transcript=(root/'Docs/Recovery/Sources/Client_Messages_September.txt').read_text(encoding='utf-8-sig')
transcript_text='\n'.join(page.extract_text() or '' for page in reader.pages[manifest['appendix_c_start']-1:])
transcript_text=re.sub(r'^SUPPLIED SEPTEMBER CORRESPONDENCE - SOURCE EVIDENCE\s*$','',transcript_text,flags=re.M)
transcript_text=re.sub(r'^APPENDIX C - PAGE \d+\s*$','',transcript_text,flags=re.M)
def normalize_source(text):
    return re.sub(r'\s+','',text.replace('\u2068','').replace('\u2069','').replace('\u2013','-').replace('\u2014','-'))
check(normalize_source(transcript) in normalize_source(transcript_text),'Complete supplied transcript content preserved in Appendix C with declared typography normalization')
check(len(reader.pages)-manifest['appendix_c_start']+1==manifest['transcript_pages'],'Appendix C page count matches manifest')
with pdfplumber.open(pdf) as document:
    clipped=[]
    for number,page in enumerate(document.pages[:manifest['body_pages']],1):
        for char in page.chars:
            if char.get('text','').strip() and (char['x0']<42 or char['x1']>page.width-42 or char['top']<12 or char['bottom']>page.height-14):
                clipped.append((number,char['text']))
    check(not clipped,'Main-report text stays within page bounds')
target=root/'Artifacts/Recovery/pdf-checks.txt'
target.write_text('\n'.join(checks)+'\n',encoding='utf-8')
print('\n'.join(checks))
print(f'PDF checks: {sum(x.startswith("PASS") for x in checks)} passed, {sum(x.startswith("FAIL") for x in checks)} failed')
raise SystemExit(any(x.startswith('FAIL') for x in checks))
