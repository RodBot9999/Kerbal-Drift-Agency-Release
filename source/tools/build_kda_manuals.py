"""Editable print-ready manuals and companion AI guides for the V1.0 release."""
from pathlib import Path
import json, html, argparse
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Flowable, KeepTogether
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT.parent/'documentation'
OUT.mkdir(parents=True,exist_ok=True)
ink=colors.HexColor('#17232c');teal=colors.HexColor('#087f79');light=colors.HexColor('#eef5f5')
styles=getSampleStyleSheet()
styles.add(ParagraphStyle(name='BodyKDA',fontName='Helvetica',fontSize=10.2,leading=14.6,textColor=ink,spaceAfter=9))
styles.add(ParagraphStyle(name='SmallKDA',fontName='Helvetica',fontSize=8.6,leading=11.7,textColor=ink,spaceAfter=5))
styles.add(ParagraphStyle(name='PageKDA',fontName='Helvetica-Bold',fontSize=21,leading=26,textColor=ink,spaceAfter=16))
styles.add(ParagraphStyle(name='SubKDA',fontName='Helvetica-Bold',fontSize=12,leading=16,textColor=teal,spaceBefore=8,spaceAfter=7))
def p(s,style='BodyKDA'):return Paragraph(html.escape(s),styles[style])
def table(rows,widths):
    t=Table([[p(str(c),'SmallKDA') for c in row] for row in rows],colWidths=widths,repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),light),('VALIGN',(0,0),(-1,-1),'TOP'),('LINEBELOW',(0,0),(-1,0),1,teal),('LINEBELOW',(0,1),(-1,-1),.3,colors.HexColor('#d5dfdf')),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),7),('BOTTOMPADDING',(0,0),(-1,-1),7)]))
    return t
class SignalMap(Flowable):
    def __init__(self,labels):super().__init__();self.labels=labels;self.width=480;self.height=78
    def draw(self):
        c=self.canv;n=len(self.labels);w=(self.width-(n-1)*16)/n
        for i,label in enumerate(self.labels):
            x=i*(w+16);c.setFillColor(light);c.setStrokeColor(teal);c.roundRect(x,12,w,56,5,fill=1,stroke=1)
            c.setFillColor(ink);c.setFont('Helvetica-Bold',9)
            lines=label.split('|')
            for j,line in enumerate(lines):c.drawCentredString(x+w/2,48-j*13,line)
            if i<n-1:c.setStrokeColor(teal);c.line(x+w+3,39,x+w+13,39);c.line(x+w+10,42,x+w+13,39);c.line(x+w+10,36,x+w+13,39)

# Every explicit page is one coherent topic; contents stay editable as JSON.
docs=json.loads((OUT/'manual-content.json').read_text(encoding='utf-8'))

# The beginner prompts and technical handoff share one editable content source.
ai_guides=json.loads((OUT/'ai-guides-content.json').read_text(encoding='utf-8'))
docs.update(ai_guides)
styles.add(ParagraphStyle(name='PromptKDA',parent=styles['BodyKDA'],fontSize=10.2,leading=14.4,backColor=light,borderColor=colors.HexColor('#bad8d5'),borderWidth=.5,borderPadding=9,leftIndent=10,rightIndent=10,spaceBefore=4,spaceAfter=14))
styles.add(ParagraphStyle(name='NoteKDA',parent=styles['SmallKDA'],textColor=teal,spaceBefore=6,spaceAfter=10))

def guide_markdown(title,pages):
    lines=['# '+('Easy AI prompts' if title=='Prompts' else 'AI Context'),'', 'Kerbal Drift Agency - updated 2026-10-29.','']
    lines+=['[Download PDF](Prompts.pdf) | [Technical AI Context](AI%20Context.md)',''] if title=='Prompts' else ['[Download PDF](AI%20Context.pdf) | [Beginner prompting guide](Prompts.md)','']
    for heading,items in pages:
        lines+=['## '+heading,'']
        for kind,data in items:
            if kind=='h':lines+=['### '+data,'']
            elif kind=='prompt':lines+=['```text',data,'```','']
            elif kind=='note':lines+=['> **Tip:** '+data,'']
            elif kind=='table':
                for i,row in enumerate(data):
                    lines+=['| '+' | '.join(str(v).replace('|','\\|') for v in row)+' |']
                    if i==0:lines+=['| '+' | '.join('---' for v in row)+' |']
                lines+=['']
            else:lines+=[data,'']
    return '\n'.join(lines)

def footer(c,doc):
    c.setStrokeColor(teal);c.line(42,43,553,43)
    c.setFont('Helvetica',8);c.setFillColor(ink);c.drawString(42,29,'KERBAL DRIFT AGENCY  /  KDA25 / CONVERTER V2 KDA12  /  2026-10-29')
    c.drawRightString(553,29,str(doc.page))
    c.setFont('Helvetica-Bold',8);c.setFillColor(teal);c.drawString(42,808,doc.title.upper())

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--only',nargs='+',choices=list(docs))
args=parser.parse_args()
for title,pages in docs.items():
    if args.only and title not in args.only:continue
    flow=[]
    flow.append(p('Runtime kda25 / Converter V2.0 kda12 | Updated 2026-10-29','SmallKDA'))
    for i,(heading,items) in enumerate(pages):
        if i:flow.append(PageBreak())
        flow.append(p(heading if title=='Prompts' else f'{i+1:02d}  {heading}','PageKDA'))
        for kind,data in items:
            if kind in ('p','step'):flow.append(p(data))
            elif kind=='prompt':flow.append(KeepTogether([p(data,'PromptKDA')]))
            elif kind=='note':flow.append(p(data,'NoteKDA'))
            elif kind=='h':flow.append(p(data,'SubKDA'))
            elif kind=='map':flow.append(SignalMap(data));flow.append(Spacer(1,7))
            elif kind=='table':
                cols=len(data[0]);widths=[145,365] if cols==2 else [85,180,105,140]
                flow.append(table(data,widths));flow.append(Spacer(1,10))
    file=OUT/(title+'.pdf')
    doc=SimpleDocTemplate(str(file),pagesize=(595.28,841.89),leftMargin=42,rightMargin=42,topMargin=57,bottomMargin=58,title=title+' - Kerbal Drift Agency',author='Roderic Systems',pageCompression=1)
    doc.build(flow,onFirstPage=footer,onLaterPages=footer)
    print(file.name)
    if title in ai_guides:(OUT/(title+'.md')).write_text(guide_markdown(title,pages),encoding='utf-8',newline='\n')
(OUT/'manual-content.json').write_text(json.dumps(docs,indent=2)+'\n',encoding='utf-8',newline='\n')
