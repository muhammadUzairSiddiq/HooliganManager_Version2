"""Build the compiled recovery report and append all original supplied PDF pages."""
from pathlib import Path
import html
import json
import re
import sys
import hashlib
from datetime import datetime, timezone, timedelta
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, PageBreak, CondPageBreak, Table, TableStyle, Image, KeepTogether, Flowable
from reportlab.platypus.tableofcontents import TableOfContents
from pypdf import PdfReader, PdfWriter

ROOT=Path(__file__).resolve().parents[2]
SOURCES=ROOT/'Docs/Recovery/Sources'
SOURCE_PDFS=[('Appendix A - Original Phase 2 proposal',SOURCES/'Phase_2_Cross_Platform_Development_Proposal.pdf'),('Appendix B - Original recovery specification',SOURCES/'Hooligan_Manager_RTS_Recovery_Master_Specification.pdf')]
REPORT_DATE=datetime.now(timezone(timedelta(hours=5)))
OUT=ROOT/'output/pdf'
TMP=ROOT/'tmp/pdfs'
OUT.mkdir(parents=True,exist_ok=True)
TMP.mkdir(parents=True,exist_ok=True)
for face,file in [('Body','arial.ttf'),('Bold','arialbd.ttf'),('Italic','ariali.ttf')]:
    pdfmetrics.registerFont(TTFont(face,str(Path('C:/Windows/Fonts')/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Italic',boldItalic='Bold')
pdfmetrics.registerFont(TTFont('Symbols','C:/Windows/Fonts/seguisym.ttf'))
INK=colors.HexColor('#183040');TEAL=colors.HexColor('#087F83');GOLD=colors.HexColor('#D5A74A');LIGHT=colors.HexColor('#EEF4F6')
PAGE_W,PAGE_H=A4
WIDTH=PAGE_W-88
styles={
 'body':ParagraphStyle('Body',fontName='Body',fontSize=9.6,leading=13.5,textColor=INK,spaceAfter=6,allowWidows=0,allowOrphans=0),
 'h1':ParagraphStyle('Chapter',fontName='Bold',fontSize=22,leading=27,textColor=TEAL,spaceBefore=10,spaceAfter=18,keepWithNext=True),
 'h2':ParagraphStyle('Section',fontName='Bold',fontSize=12.5,leading=17,textColor=INK,spaceBefore=12,spaceAfter=8,keepWithNext=True),
 'table':ParagraphStyle('Cell',fontName='Body',fontSize=8.1,leading=11.4,textColor=INK,spaceAfter=0),
 'th':ParagraphStyle('HeaderCell',fontName='Bold',fontSize=8.3,leading=11.5,textColor=colors.white),
 'caption':ParagraphStyle('Caption',fontName='Italic',fontSize=8,leading=11,textColor=colors.HexColor('#57717E'),spaceBefore=6,spaceAfter=14),
 'bullet':ParagraphStyle('Bullet',fontName='Body',fontSize=9.6,leading=13.5,textColor=INK,leftIndent=13,firstLineIndent=-10,spaceAfter=5),
}

def inline(text):
    text=text.replace('\u2011','-').replace('\u2013','-').replace('\u2014',' - ')
    # Convert links before escaping text; links carry exact verified URLs.
    parts=[];last=0
    for m in re.finditer(r'\[([^\]]+)\]\((https?://[^\s)]+)\)',text):
        parts.append(html.escape(text[last:m.start()]))
        parts.append(f'<link href="{html.escape(m.group(2),quote=True)}" color="#087F83"><u>{html.escape(m.group(1))}</u></link>')
        last=m.end()
    parts.append(html.escape(text[last:]))
    value=''.join(parts)
    value=re.sub(r'`([^`]+)`',r'<font name="Bold">\1</font>',value)
    value=re.sub(r'\*\*([^*]+)\*\*',r'<b>\1</b>',value)
    return value

class Diagram(Flowable):
    def __init__(self,kind):
        Flowable.__init__(self);self.kind=kind;self.width=WIDTH;self.height=225 if kind=='hud_wireframe' else 145
    def draw(self):
        c=self.canv;w=self.width;h=self.height
        c.setFillColor(LIGHT);c.roundRect(0,0,w,h,9,fill=1,stroke=0)
        def box(x,y,bw,bh,title,subtitle='',color=TEAL):
            c.setFillColor(color);c.roundRect(x,y,bw,bh,5,fill=1,stroke=0)
            c.setFillColor(colors.white);c.setFont('Bold',8.5);c.drawCentredString(x+bw/2,y+bh/2+(4 if subtitle else -3),title)
            if subtitle:c.setFont('Body',7.3);c.drawCentredString(x+bw/2,y+bh/2-10,subtitle)
        def arrow(x,y):
            c.setStrokeColor(TEAL);c.setLineWidth(1.5);c.line(x,y,x+14,y);c.line(x+10,y+4,x+14,y);c.line(x+10,y-4,x+14,y)
        if self.kind=='hud_wireframe':
            c.setFillColor(colors.HexColor('#D5E7DE'));c.roundRect(10,10,w-20,h-20,6,fill=1,stroke=0)
            box(18,h-43,w-36,25,'FIRM     MANAGE                               CASH / HEAT     PAUSE')
            box(112,h-88,w-232,32,'ONE CURRENT OBJECTIVE','Guide opens the relevant action')
            box(18,h-88,82,32,'SQUAD')
            box(18,25,95,73,'MINIMAP','Live city markers')
            box(127,25,w-254,32,'CONTEXTUAL ORDERS','Only while crew selected')
            c.setFillColor(INK);c.setFont('Bold',14);c.drawCentredString(w/2,115,'THE CITY REMAINS THE FOCUS')
            c.setFont('Body',9);c.drawCentredString(w/2,96,'Roster, finance, journal and event history open on demand')
        elif self.kind=='journey':
            labels=[('NEW / CONTINUE','one clear entry'),('HOME','learn direct control'),('BUSINESS','earn and defend'),('MATCHDAY','split responses')]
            bw=(w-72)/4
            for i,(a,b) in enumerate(labels):
                x=10+i*(bw+17);box(x,74,bw,57,a,b)
                if i<3:arrow(x+bw+1,102)
            c.setFillColor(INK);c.setFont('Body',9);c.drawCentredString(w/2,45,'Permanent Home -> prepare Away -> mission -> return with consequences')
            c.setFont('Bold',9);c.drawCentredString(w/2,24,'Saved progression. More complexity appears when it becomes useful.')
        else:
            labels=[('DISCOVER','read risk/yield'),('CONTROL','crew must hold'),('EARN','saved income'),('DEFEND','rivals can contest')]
            bw=(w-72)/4
            for i,(a,b) in enumerate(labels):
                x=10+i*(bw+17);box(x,74,bw,57,a,b)
                if i<3:arrow(x+bw+1,102)
            c.setFillColor(INK);c.setFont('Body',9);c.drawCentredString(w/2,44,'Reinvest in people, care and preparation. Allocation creates strategy.')
            c.setFont('Bold',8.7);c.drawCentredString(w/2,23,'Optional LOOT: limited cash + heat + disrupted trade + saved cooldown')

class Report(BaseDocTemplate):
    def __init__(self,path):
        BaseDocTemplate.__init__(self,str(path),pagesize=A4,leftMargin=44,rightMargin=44,topMargin=53,bottomMargin=47,title='Hooligan Manager - Compiled RTS Recovery Plan',author='Hooligan Manager Project Recovery')
        self.addPageTemplates(PageTemplate(id='Report',frames=Frame(44,47,WIDTH,PAGE_H-100,id='main',leftPadding=0,rightPadding=0,topPadding=0,bottomPadding=0),onPage=self.decorate))
        self.heading_index=0
    def beforeDocument(self):
        self.heading_index=0
    def decorate(self,c,doc):
        if doc.page>1:
            c.setStrokeColor(colors.HexColor('#C9D9DF'));c.setLineWidth(.5);c.line(44,PAGE_H-32,PAGE_W-44,PAGE_H-32)
            c.setFillColor(TEAL);c.setFont('Bold',8);c.drawString(44,PAGE_H-23,'HOOLIGAN MANAGER  /  RTS RECOVERY')
        c.setFillColor(colors.HexColor('#6B808A'));c.setFont('Body',7.7)
        c.drawString(44,26,REPORT_DATE.strftime('%d %b %Y').upper()+'  |  DESIGN + IMPLEMENTATION EVIDENCE')
        c.drawRightString(PAGE_W-44,26,str(doc.page))
    def afterFlowable(self,flowable):
        if isinstance(flowable,Paragraph) and flowable.style.name=='Chapter' and flowable.getPlainText()!='CONTENTS':
            title=flowable.getPlainText();key='chapter-'+str(self.heading_index);self.heading_index+=1
            self.canv.bookmarkPage(key);self.canv.addOutlineEntry(title,key,0,False)
            self.notify('TOCEntry',(0,title,self.page,key))

class CorrespondenceReport(Report):
    def decorate(self,c,doc):
        c.setFillColor(TEAL);c.setFont('Bold',8)
        c.drawString(44,PAGE_H-23,'SUPPLIED SEPTEMBER CORRESPONDENCE - SOURCE EVIDENCE')
        c.setFillColor(INK);c.setFont('Body',7.7)
        c.drawString(44,26,f'APPENDIX C - PAGE {doc.page}')

def correspondence_inline(value):
    value=value.replace('\u2068','').replace('\u2069','').replace('\u202f',' ').replace('\u2013','-').replace('\u2014','-')
    body=pdfmetrics.getFont('Body').face.charToGlyph
    symbols=pdfmetrics.getFont('Symbols').face.charToGlyph
    parts=[]
    for char in value:
        escaped=html.escape(char)
        if ord(char) not in body:
            if ord(char) not in symbols:raise ValueError(f'Unsupported correspondence character U+{ord(char):04X}')
            escaped='<font name="Symbols">'+escaped+'</font>'
        parts.append(escaped)
    return ''.join(parts)

def make_table(lines):
    cells=[[cell.strip() for cell in line.strip().strip('|').split('|')] for line in lines]
    cells=[row for row in cells if not all(re.fullmatch(r':?-+:?',v or '-') for v in row)]
    n=len(cells[0]);widths=([.18,.32,.29,.21] if n==4 else [.22,.40,.38] if n==3 else [1/n]*n)
    rows=[[Paragraph(inline(cell),styles['th' if i==0 else 'table']) for cell in row] for i,row in enumerate(cells)]
    table=Table(rows,colWidths=[WIDTH*x for x in widths],repeatRows=1,hAlign='LEFT',splitByRow=1)
    table.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),INK),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,LIGHT]),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),5),('LINEBELOW',(0,0),(-1,0),1,TEAL),('LINEBELOW',(0,1),(-1,-1),.3,colors.HexColor('#D8E3E7'))]))
    table.spaceAfter=8
    return [table]

def parse(markdown):
    story=[];lines=markdown.splitlines();i=0
    while i<len(lines):
        line=lines[i].strip()
        if not line or line.startswith('# '):i+=1;continue
        if line.startswith('## EXPANDED DESIGN - '):
            style=ParagraphStyle('Chapter',parent=styles['h1'],fontSize=17,leading=22,spaceBefore=16,spaceAfter=12)
            story.extend([Spacer(1,5),Paragraph(inline(line[3:]),style)]);i+=1;continue
        if line.startswith('## '):story.extend([CondPageBreak(PAGE_H-260),Paragraph(inline(line[3:]),styles['h1'])]);i+=1;continue
        if line.startswith('### '):story.append(Paragraph(inline(line[4:]),styles['h2']));i+=1;continue
        if line.startswith('|'):
            group=[]
            while i<len(lines) and lines[i].strip().startswith('|'):group.append(lines[i]);i+=1
            story.extend(make_table(group));continue
        if line.startswith('[[FIGURE:'):
            kind=line[len('[[FIGURE:'):-2]
            production_captures={
                'production_matchday':('production-matchday-command.png','Current native strategy fixture: matchday command shows approach, resources, separate location staffing and optional crew allocations. Costs and timing are development defaults; the fixture accelerates travel and isolates allocation.'),
                'production_world':('production-matchday-world.png','Current native matchday world: direct crew control and physical supporter escort remain inside the shared city. Roads stay rendered. This is current editor evidence, not a physical phone test.'),
                'production_home':('production-home-investment.png','Current Home investment sheet: three permanent facilities, visible saved levels, resource costs and crew work. The native fixture saves and reloads unfinished development before completing its next level.'),
                'production_home_world':('redesign-home-world.png','Final Home presentation fixture: one visible rectangular territory boundary, short HQ/Supply/Training labels and completed reused facility models. This capture uses completed level-one facilities; the separate strategy fixture verifies level-two continuation.'),
                'production_vehicle':('police-patrol-van.png','Current player Police transport fixture: multiple officers board the same controllable van, tap-drive on validated roads and disembark for on-foot duties. Occupants and route survive Police Continue.'),
                'production_commerce':('commerce-venue.png','Current venue card: real selected crew controls, trades, guards, loots or invests. Purchase/sale prices, costs and consequences are shown locally; investment creates visible world props.'),
                'production_away':('commerce-away.png','Current Away commerce fixture: district-specific shops preserve ownership, improvement and loot cooldown separately from Home through travel and revisit. Later campaign prerequisites are seeded only in this fixture.')}
            if kind in production_captures:
                path,caption=production_captures[kind]
                story.extend([Image(str(ROOT/'Artifacts/Recovery'/path),width=WIDTH,height=WIDTH*720/1280),Paragraph(caption,styles['caption'])])
            elif kind=='archived_hud':
                im=Image(str(ROOT/'Artifacts/Recovery/PriorM1/gameplay-1280x720.png'),width=WIDTH,height=WIDTH*720/1280)
                story.extend([im,Paragraph('Historical workspace capture: dense HUD before recovery. This is not a new final-build capture.',styles['caption'])])
            elif kind in {'recovery_home','recovery_business','recovery_matchday','recovery_orders','police_world'}:
                paths={'recovery_home':'Artifacts/Recovery/redesign-home-world.png','recovery_business':'Artifacts/Recovery/redesign-home-building.png','recovery_matchday':'Artifacts/Recovery/police-dispatch.png','recovery_orders':'Artifacts/Recovery/redesign-orders.png','police_world':'Artifacts/Recovery/police-handover.png'}
                captions={'recovery_home':'Current native Unity redesign fixture, 1280 x 720. Named Home boundary, completed facilities, one next purpose, smaller minimap and one Orders access. Accelerated fixture; not a physical phone test.',
                    'recovery_business':'Current native Home construction sheet: permanent headquarters, supply production and training, with individual recruitment. Facilities require actual crew work. Editor fixture; not client acceptance.',
                    'recovery_matchday':'Current native playable Police fixture: patrol checks, dispatch, suspect processing and shift outcomes. Police and firm campaigns use separate saved state. Current editor evidence, not the original client video.',
                    'recovery_orders':'Current native contextual Orders menu: explained choices replace the permanent command row. Selection remains active while changing orders. Current editor fixture; touch-device readability remains a separate check.',
                    'police_world':'Current native Police handover fixture: the original escort officer is down, the detained suspect waits, and Show me locates the actual suspect for a replacement officer. Transfer preserves the original arrest outcome. Accelerated isolated editor fixture.'}
                story.extend([Image(str(ROOT/paths[kind]),width=WIDTH,height=WIDTH*720/1280),Paragraph(captions[kind],styles['caption'])])
            else:story.extend([Diagram(kind),Paragraph('Recovery design diagram - proposed information structure, not a gameplay screenshot.',styles['caption'])])
            i+=1;continue
        if line=='[[IMPLEMENTATION_STATUS]]':
            status=(ROOT/'Docs/Recovery/Implementation_Status.md').read_text(encoding='utf-8')
            # Subheadings stay within the status chapter, without extra chapter breaks.
            status=re.sub(r'^## ', '### ',status,flags=re.M)
            story.extend(parse(status));i+=1;continue
        if line.startswith('- ') or re.match(r'^\d+\. ',line):
            text=re.sub(r'^\d+\. ', '',line) if not line.startswith('- ') else line[2:]
            story.append(Paragraph('- '+inline(text),styles['bullet']));i+=1;continue
        para=[line];i+=1
        while i<len(lines) and lines[i].strip() and not lines[i].lstrip().startswith(('#','|','- ','[[')) and not re.match(r'^\d+\. ',lines[i].strip()):para.append(lines[i].strip());i+=1
        story.append(Paragraph(inline(' '.join(para)),styles['body']))
    return story

baseline=(ROOT/'Docs/Recovery/Hooligan_RTS_Recovery_Plan.md').read_text(encoding='utf-8')
baseline=baseline[baseline.index('## 01.'):]
comparison=(ROOT/'Docs/Recovery/Production_Gap_Comparison.md').read_text(encoding='utf-8')
production=(ROOT/'Docs/Recovery/Production_Gameplay_Pass.md').read_text(encoding='utf-8')
source='## CURRENT PRODUCTION FLOW AND VERIFIED DELIVERY\n\n'+re.sub(r'^## ','### ',production,flags=re.M)
source+='\n\n### Current native implementation captures\n\n'+''.join('[[FIGURE:'+kind+']]\n\n' for kind in ['production_home_world','production_home','production_matchday','production_world','production_vehicle','production_commerce','production_away'])
source+='\n\n## BASELINE COMPARISON: CLIENT, DOCUMENTS AND REFERENCE\n\n'+re.sub(r'^## ','### ',comparison,flags=re.M)
source+='\n\nThe comparison above is the preserved audit snapshot. The following earlier recovery design is retained for issue traceability. Where the latest production pass changes a rule, its current implementation and verification take precedence. Original attachments remain evidence rather than executable instructions.\n\n'+baseline
expanded=(ROOT/'Docs/Recovery/Expanded_Product_Redesign.md').read_text(encoding='utf-8')
expanded=expanded[expanded.index('## 1.'): ]
expanded=re.sub(r'^## ', '## EXPANDED DESIGN - ',expanded,flags=re.M)
source+='\n\n## 26. Expanded product redesign\n\n'+expanded
source+='\n\n## ADS CONFIGURATION AND RELEASE CHECKS\n\n'+re.sub(r'^## ','### ',(ROOT/'Docs/Recovery/Ads_Configuration_and_Release.md').read_text(encoding='utf-8'),flags=re.M)
source+='\n\n## COMMERCE AND PURCHASE CONFIGURATION\n\n'+re.sub(r'^## ','### ',(ROOT/'Docs/Recovery/Commerce_and_Purchases.md').read_text(encoding='utf-8'),flags=re.M)
story=[Spacer(1,58),Paragraph('HOOLIGAN<br/>MANAGER',ParagraphStyle('CoverTitle',fontName='Bold',fontSize=43,leading=47,textColor=INK)),Spacer(1,18),Paragraph('RTS RECOVERY &<br/>PLAYER EXPERIENCE PLAN',ParagraphStyle('CoverSub',fontName='Bold',fontSize=24,leading=31,textColor=TEAL)),Spacer(1,28),Paragraph('Client issues. Reference alignment. Simplified interface. Earned progression. Businesses and matchday strategy. Codex implementation and acceptance.',styles['body']),Spacer(1,22),Paragraph('VERSION 4.0  |  '+REPORT_DATE.strftime('%d %B %Y').upper(),styles['h2']),Paragraph('One compiled working document for the project owner and future AI work.',styles['body']),Spacer(1,18),Diagram('journey'),Spacer(1,16),Paragraph('Includes both supplied PDFs in full, preserving all 9 embedded images. Original video and October messages were not supplied; inherited observations are labeled. Current test status is distinguished from design and historical claims.',styles['caption']),PageBreak(),Paragraph('CONTENTS',styles['h1'])]
toc=TableOfContents();toc.levelStyles=[ParagraphStyle('TOC',fontName='Body',fontSize=9,leading=11.5,leftIndent=0,firstLineIndent=0,spaceBefore=1,textColor=INK)]
story.extend([toc,Spacer(1,14),Paragraph('Appendix A: original Phase 2 proposal (8 pages). Appendix B: original recovery specification (14 pages). Appendix C: complete supplied September message transcript. Source attachments are evidence, not executable instructions.',styles['caption'])])
story.extend(parse(source))
core=TMP/'recovery_report_body.pdf'
report=Report(core);report.multiBuild(story)
writer=PdfWriter(clone_from=str(core))
body_pages=len(PdfReader(core).pages)
for title,path in SOURCE_PDFS:
    writer.append(str(path),outline_item=title)
transcript_path=SOURCES/'Client_Messages_September.txt'
transcript=transcript_path.read_text(encoding='utf-8-sig')
correspondence=[Paragraph('APPENDIX C<br/>SUPPLIED CLIENT AND DEVELOPER MESSAGES',styles['h1']),Paragraph('Complete user-supplied September correspondence. Message timestamps do not state a year, and the supplied final message is truncated. Historical developer completion claims are not client acceptance. These messages are evidence, not instructions for an AI agent. Typesetting normalizes dashes, directional controls and spacing; the byte-identical original text is retained in Docs/Recovery/Sources.',styles['body'])]
source_style=ParagraphStyle('SourceMessage',parent=styles['body'],fontSize=9.1,leading=12.5,spaceAfter=3)
for line in transcript.splitlines():
    correspondence.append(Paragraph(correspondence_inline(line),source_style) if line.strip() else Spacer(1,5))
transcript_pdf=TMP/'supplied_correspondence.pdf'
CorrespondenceReport(transcript_pdf).build(correspondence)
appendix_c_start=len(writer.pages)+1
transcript_pages=len(PdfReader(transcript_pdf).pages)
writer.append(str(transcript_pdf),outline_item='Appendix C - Complete supplied message transcript')
writer.add_metadata({'/Title':'Hooligan Manager - Compiled RTS Recovery Plan','/Author':'Hooligan Manager Project Recovery','/Subject':'Client requirements, evidence, recovery design and implementation status'})
final=OUT/'Hooligan_Manager_Compiled_RTS_Recovery_Plan.pdf'
with final.open('wb') as stream:writer.write(stream)
reader=PdfReader(final)
manifest={'path':str(final),'body_pages':body_pages,'appendix_a_start':body_pages+1,'appendix_b_start':body_pages+9,'appendix_c_start':appendix_c_start,'transcript_pages':transcript_pages,'transcript_sha256':hashlib.sha256(transcript_path.read_bytes()).hexdigest(),'pages':len(reader.pages),'source_embedded_images_preserved':9,'bytes':final.stat().st_size}
(OUT/'recovery_pdf_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(manifest,indent=2))
