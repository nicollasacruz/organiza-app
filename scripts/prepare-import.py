#!/usr/bin/env python3
"""Normalize a private XLSX and optional TSV without publishing personal records."""
import argparse, datetime, decimal, json, zipfile, xml.etree.ElementTree as ET
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('workbook',type=Path)
parser.add_argument('--tsv',type=Path)
parser.add_argument('--first-date',help='Confirmed replacement for the first workbook data-row date (YYYY-MM-DD)')
parser.add_argument('--batch',default='initial-earnings-v1',help='Stable import identifier. Reuse it when repeating this import.')
parser.add_argument('--output',type=Path,default=Path('.local/earnings-import.json'))
args=parser.parse_args()
ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
kinds={'SALA':'Sala','AULA 30M':'Aula30','AULA 50M':'Aula50'}
rows=[]
def add(date,kind,quantity,capacity,value,source):
    quantity=decimal.Decimal(str(quantity).replace(',','.'))
    capacity=int(decimal.Decimal(capacity)) if capacity else None
    cents=int(decimal.Decimal(str(value).replace('€','').strip().replace(',','.'))*100)
    if kind=='SALA':calculated=int((quantity*700).quantize(decimal.Decimal('1'),rounding=decimal.ROUND_HALF_UP))
    else:
        if capacity is None or capacity<=0 or quantity!=int(quantity) or quantity<0 or quantity>capacity:raise ValueError('Invalid lesson input in '+source)
        tier=0 if quantity*100<=capacity*49 else 1 if quantity*100<capacity*90 else 2
        calculated=([750,975,1060] if kind=='AULA 30M' else [1300,1600,1750])[tier]
    if calculated!=cents:raise ValueError('Cached amount differs from tariff in '+source)
    rows.append(dict(date=date,kind=kinds[kind],quantity=float(quantity),capacity=capacity,sourceId=args.batch+':'+source,expectedCents=cents))
with zipfile.ZipFile(args.workbook) as z:
    strings=[''.join(e.itertext()) for e in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si',ns)]
    sheet=ET.fromstring(z.read('xl/worksheets/sheet1.xml'))
    for r in sheet.findall('s:sheetData/s:row',ns):
        number=int(r.attrib['r'])
        if number<2:continue
        cells={}
        for c in r.findall('s:c',ns):
            v=c.find('s:v',ns)
            if v is None:continue
            value=v.text or ''
            if c.attrib.get('t')=='s':value=strings[int(value)]
            cells[''.join(x for x in c.attrib['r'] if x.isalpha())]=value
        if not cells.get('B') or cells['B'] not in kinds:continue
        value=cells['A']
        if number==2 and args.first_date:date=datetime.date.fromisoformat(args.first_date)
        elif value.replace('.','',1).isdigit():date=datetime.date(1899,12,30)+datetime.timedelta(days=float(value))
        else:date=datetime.date.fromisoformat(value.replace('/','-'))
        add(date.isoformat(),cells['B'],cells['C'],cells.get('D'),cells['E'],f'xlsx:sheet1:row{number}')
if args.tsv:
    for number,line in enumerate(args.tsv.read_text().splitlines(),1):
        if not line.strip():continue
        parts=line.split('\t')
        if len(parts)!=5:raise ValueError('TSV requires five columns: date, type, quantity, capacity, amount')
        add(parts[0].replace('/','-'),*parts[1:],f'tsv:row{number}')
args.output.parent.mkdir(parents=True,exist_ok=True)
args.output.write_text(json.dumps(rows,indent=2,ensure_ascii=False)+'\n')
totals={}
for row in rows:totals[row['date'][:7]]=totals.get(row['date'][:7],0)+row['expectedCents']
print(f'{len(rows)} records written to private file {args.output}; monthly totals in cents: {totals}')
