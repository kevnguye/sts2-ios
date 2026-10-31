#!/usr/bin/env python3
"""Render the repository's English HTML technical record to a text-only PDF.

Requires reportlab. No game files, images or external assets are loaded.
"""
import argparse
import html
from html.parser import HTMLParser
from pathlib import Path
import textwrap

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, Table, TableStyle, Preformatted


class Element:
    def __init__(self, tag, attrs=()):
        self.tag, self.attrs, self.children = tag, dict(attrs), []

    def text(self):
        return ''.join(c if isinstance(c, str) else ('\n' if c.tag == 'br' else c.text()) for c in self.children)


class DocumentParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.root = Element('document')
        self.stack = [self.root]

    def handle_starttag(self, tag, attrs):
        node = Element(tag, attrs)
        self.stack[-1].children.append(node)
        if tag not in ('br', 'meta', 'hr', 'link', 'img', 'input'): self.stack.append(node)

    def handle_endtag(self, tag):
        for i in range(len(self.stack) - 1, 0, -1):
            if self.stack[i].tag == tag:
                del self.stack[i:]
                break

    def handle_data(self, data):
        self.stack[-1].children.append(data)


def descendants(node, tag):
    for child in node.children:
        if isinstance(child, Element):
            if child.tag == tag: yield child
            yield from descendants(child, tag)


def inline(node):
    result = []
    for child in node.children:
        if isinstance(child, str): result.append(html.escape(child))
        elif child.tag == 'br': result.append('<br/>')
        elif child.tag in ('strong', 'b'): result.append('<b>' + inline(child) + '</b>')
        elif child.tag in ('em', 'i'): result.append('<i>' + inline(child) + '</i>')
        elif child.tag == 'code': result.append('<font name="Courier">' + inline(child) + '</font>')
        else: result.append(inline(child))
    return ''.join(result)


def render(source, output):
    parser = DocumentParser()
    parser.feed(source.read_text())
    body = next(descendants(parser.root, 'body'))
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle('ReportBody', fontName='Helvetica', fontSize=9, leading=13,
                              spaceAfter=7, textColor=colors.HexColor('#23272e')))
    styles.add(ParagraphStyle('ReportTitle', fontName='Helvetica-Bold', fontSize=30, leading=37,
                              alignment=TA_CENTER, textColor=colors.HexColor('#302234'), spaceAfter=18))
    styles.add(ParagraphStyle('ReportSubtitle', parent=styles['ReportBody'], alignment=TA_CENTER,
                              fontSize=12, leading=17, spaceAfter=16))
    styles.add(ParagraphStyle('ReportH2', parent=styles['Heading2'], fontSize=16, leading=20,
                              textColor=colors.HexColor('#302234'), spaceBefore=14, spaceAfter=10))
    styles.add(ParagraphStyle('ReportH3', parent=styles['Heading3'], fontSize=11, leading=15,
                              textColor=colors.HexColor('#a33b21'), spaceBefore=9, spaceAfter=6))
    styles.add(ParagraphStyle('ReportCell', parent=styles['ReportBody'], fontSize=8, leading=11, spaceAfter=0))
    styles.add(ParagraphStyle('ReportHeadCell', parent=styles['ReportCell'], fontName='Helvetica-Bold', textColor=colors.white))
    styles.add(ParagraphStyle('ReportCode', fontName='Courier', fontSize=7.1, leading=10,
                              backColor=colors.HexColor('#f4f0e8'), borderPadding=8, spaceBefore=5, spaceAfter=12))
    styles.add(ParagraphStyle('ReportBullet', parent=styles['ReportBody'], leftIndent=12, firstLineIndent=-8))
    story = []
    width = A4[0] - 34*mm

    def visit(node):
        if not isinstance(node, Element):
            if node.strip(): story.append(Paragraph(html.escape(node), styles['ReportBody']))
            return
        cls = node.attrs.get('class', '')
        if node.tag == 'div' and cls == 'cover':
            story.append(Spacer(1, 40*mm))
            for child in node.children:
                if not isinstance(child, Element): continue
                style = styles['ReportTitle'] if child.tag == 'h1' else styles['ReportSubtitle']
                story.append(Paragraph(inline(child), style))
            story.append(PageBreak())
        elif node.tag == 'h2':
            if 'sec' in cls: story.append(PageBreak())
            story.append(Paragraph(inline(node), styles['ReportH2']))
        elif node.tag == 'h3':
            if node.text().startswith('7.4 '): story.append(PageBreak())
            story.append(Paragraph(inline(node), styles['ReportH3']))
        elif node.tag in ('p', 'li'):
            bullet = '- ' if node.tag == 'li' else ''
            story.append(Paragraph(bullet + inline(node), styles['ReportBullet' if bullet else 'ReportBody']))
        elif node.tag == 'pre':
            code = []
            for line in node.text().strip().splitlines():
                code.extend(textwrap.wrap(line, width=99, replace_whitespace=False,
                                          drop_whitespace=False, subsequent_indent='  ') or [''])
            story.append(Preformatted('\n'.join(code), styles['ReportCode']))
        elif node.tag == 'table':
            rows = []
            for row in descendants(node, 'tr'):
                cells = [c for c in row.children if isinstance(c, Element) and c.tag in ('th', 'td')]
                rows.append([Paragraph(inline(c), styles['ReportHeadCell' if c.tag == 'th' else 'ReportCell']) for c in cells])
            count = len(rows[0])
            fractions = [.25,.27,.48] if count == 3 else [1/count]*count
            table = Table(rows, colWidths=[width*x for x in fractions], repeatRows=1, hAlign='LEFT')
            table.setStyle(TableStyle([
                ('BACKGROUND',(0,0),(-1,0),colors.HexColor('#302234')),
                ('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.HexColor('#faf6ee'),colors.white]),
                ('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),7),
                ('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),7),
                ('BOTTOMPADDING',(0,0),(-1,-1),7),
                ('LINEBELOW',(0,0),(-1,-1),.3,colors.HexColor('#dfd7c9'))]))
            story.extend([table,Spacer(1,9)])
        elif cls == 'bt': story.append(Paragraph('<b>'+inline(node)+'</b>',styles['ReportBody']))
        elif node.tag == 'div' and cls in ('lead','foot'):
            story.append(Paragraph(inline(node),styles['ReportBody']))
        else:
            for child in node.children: visit(child)

    for child in body.children: visit(child)
    output.parent.mkdir(parents=True, exist_ok=True)
    doc = SimpleDocTemplate(str(output), pagesize=A4, rightMargin=17*mm, leftMargin=17*mm,
                            topMargin=17*mm, bottomMargin=18*mm, title='Slay the Spire 2: Native iOS Port - Technical Record',
                            author='STS2 iOS port contributors', invariant=1)
    def footer(canvas, document):
        canvas.saveState();canvas.setFont('Helvetica',8);canvas.setFillColor(colors.grey)
        canvas.drawString(17*mm,10*mm,'Historical technical record - English translation')
        canvas.drawRightString(A4[0]-17*mm,10*mm,str(document.page));canvas.restoreState()
    doc.build(story,onFirstPage=footer,onLaterPages=footer)


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',type=Path,default=Path('share/technical_doc.html'))
    parser.add_argument('--output',type=Path,default=Path('share/technical_record.pdf'))
    args=parser.parse_args()
    render(args.source,args.output)
