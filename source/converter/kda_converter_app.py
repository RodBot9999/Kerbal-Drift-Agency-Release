"""Packaged EXE entry point and optional command-line conversion."""
import argparse,json,sys,traceback
from pathlib import Path

def main():
    p=argparse.ArgumentParser();p.add_argument('--install-job');p.add_argument('--convert');p.add_argument('--output');p.add_argument('--quality',choices=['quality','balanced','lightweight'],default='balanced');p.add_argument('--seats',type=int,default=2);p.add_argument('--ksp');p.add_argument('--save');p.add_argument('--gui-smoke')
    p.add_argument('--paint-color',help='Override primary body paint with #RRGGBB')
    args=p.parse_args()
    if args.install_job:
        from kdaconvert.elevation import install_job
        return install_job(args.install_job)
    if args.convert:
        from kdaconvert.service import convert
        from kdaconvert.installer import install
        if not args.output:p.error('--output is required for command-line conversion')
        logs=[]
        try:
            output=convert(args.convert,args.output,args.quality,args.seats,log=logs.append,paint_color=args.paint_color)
            if args.ksp:install(output,args.ksp,args.save,log=logs.append)
            (output/'desktop-session.log').write_text('\n'.join(logs)+'\n',encoding='utf-8');return 0
        except Exception:
            error=Path(args.output).parent/'KDAConverter-error.log';error.parent.mkdir(parents=True,exist_ok=True);error.write_text('\n'.join(logs)+'\n'+traceback.format_exc(),encoding='utf-8');return 1
    from kdaconvert.desktop import run
    run(args.gui_smoke);return 0

if __name__=='__main__':sys.exit(main())
