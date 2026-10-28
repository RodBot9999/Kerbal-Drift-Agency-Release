"""Command-line front end using the same validated conversion path as the GUI."""
import argparse,hashlib,json,tempfile
from pathlib import Path
from .automation import safe_extract,inspect
from .service import convert

def main():
    p=argparse.ArgumentParser(description='Automation BeamNG ZIP to KDA vehicle package')
    p.add_argument('source',type=Path);p.add_argument('--output',type=Path,default=Path('converter-output'));p.add_argument('--quality',choices=['quality','balanced','lightweight'],default='balanced');p.add_argument('--config');p.add_argument('--inspect-only',action='store_true');p.add_argument('--seats',type=int,choices=range(9),default=2,help='Planned physical seats; attach seats/doors and crew them in the editor')
    p.add_argument('--paint-color',help='Optional primary body paint override in #RRGGBB format')
    args=p.parse_args()
    if args.inspect_only:
        if args.source.suffix.lower()!='.zip':p.error('Use the Automation BeamNG export ZIP')
        args.output.mkdir(parents=True,exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='kdaconvert-inspect-') as tmp:
            safe_extract(args.source,tmp);m,_=inspect(tmp,hashlib.sha256(args.source.read_bytes()).hexdigest(),args.config)
            (args.output/'inspection.json').write_text(json.dumps(m,indent=2)+'\n',encoding='utf-8')
        print('Inspection ready: '+str(args.output/'inspection.json'))
    else:convert(args.source,args.output,args.quality,args.seats,args.config,paint_color=args.paint_color)
