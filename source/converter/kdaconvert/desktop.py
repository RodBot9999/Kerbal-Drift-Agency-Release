"""Small offline desktop front end. All conversion runs on a worker thread."""
import json,os,queue,threading,traceback,uuid
from datetime import datetime
from pathlib import Path
import tkinter as tk
from tkinter import ttk,filedialog,messagebox,colorchooser
from .service import convert
from .installer import install
from .elevation import elevated_install
from .version import VERSION,BUILD

CAPABILITIES = (
    ('Vehicle data', 'Torque / boost, gearing and active AWD\nTire grip, aero, CG and axle weight balance\nAdjustable fuel use; massless crew seats'),
    ('Driving & effects', 'Original hybrid engine audio; shared presets\nNo exported engine recordings copied\nRace / Drift aids, lights, smoke and telemetry'),
    ('Car appearance', 'Chrome, paint and PBR material conversion\nRally / Desert travel, track width and graphs\nPower graphs, exhaust tunes and visual suspension'),
)

def black_theme(root):
    root.configure(background='#090909')
    style=ttk.Style(root);style.theme_use('clam')
    style.configure('.',background='#090909',foreground='#eeeeee',font=('Segoe UI',10))
    style.configure('TFrame',background='#090909')
    style.configure('TLabel',background='#090909',foreground='#eeeeee')
    style.configure('Muted.TLabel',foreground='#aaaaaa')
    style.configure('Version.TLabel',foreground='#56dec3',font=('Segoe UI',10,'bold'))
    style.configure('Card.TFrame',background='#171717')
    style.configure('Card.TLabel',background='#171717',foreground='#cccccc',font=('Segoe UI',9))
    style.configure('CardHeading.TLabel',background='#171717',foreground='#56dec3',font=('Segoe UI',10,'bold'))
    for kind in ('TEntry','TCombobox','TSpinbox'):
        style.configure(kind,fieldbackground='#202020',background='#242424',foreground='#eeeeee',insertcolor='#eeeeee',bordercolor='#454545',lightcolor='#454545',darkcolor='#454545',arrowcolor='#dddddd',selectbackground='#176456',selectforeground='#ffffff')
        style.map(kind,fieldbackground=[('disabled','#171717'),('readonly','#202020')],foreground=[('disabled','#777777'),('readonly','#eeeeee')])
    style.configure('TButton',background='#262626',foreground='#eeeeee',bordercolor='#454545',lightcolor='#454545',darkcolor='#454545',padding=(12,7))
    style.map('TButton',background=[('pressed','#404040'),('active','#333333')],foreground=[('disabled','#777777')])
    style.configure('Horizontal.TProgressbar',background='#56dec3',troughcolor='#202020',bordercolor='#202020')
    style.configure('Vertical.TScrollbar',background='#333333',troughcolor='#171717',arrowcolor='#cccccc',bordercolor='#171717',lightcolor='#333333',darkcolor='#333333')
    style.map('Vertical.TScrollbar',background=[('pressed','#555555'),('active','#444444'),('!disabled','#333333')])
    root.option_add('*TCombobox*Listbox.background','#202020');root.option_add('*TCombobox*Listbox.foreground','#eeeeee')
    root.option_add('*TCombobox*Listbox.selectBackground','#176456');root.option_add('*TCombobox*Listbox.selectForeground','#ffffff')

class App:
    def __init__(self,root):
        self.root=root;root.title('KDA Vehicle Converter V'+VERSION);root.geometry('920x780');root.minsize(880,740);black_theme(root)
        self.events=queue.Queue();self.busy=False;self.package=None
        self.settings=Path(os.environ.get('LOCALAPPDATA',str(Path.home())))/'KDAConverterV2/settings.json'
        try:prefs=json.loads(self.settings.read_text(encoding='utf-8'))
        except (OSError,ValueError):prefs={}
        default_ksp=Path(os.environ.get('ProgramFiles(x86)','C:/Program Files (x86)'))/'Steam/steamapps/common/Kerbal Space Program'
        self.zip=tk.StringVar();self.ksp=tk.StringVar(value=prefs.get('ksp',str(default_ksp)))
        self.output=tk.StringVar(value=prefs.get('output',str(Path.home()/'Documents/KDA Vehicle Conversions')))
        self.quality=tk.StringVar(value=prefs.get('quality','High (2048)'));self.seats=tk.StringVar(value=str(prefs.get('seats',2)));self.save=tk.StringVar(value=prefs.get('save',''))
        self.status=tk.StringVar(value='Choose an Automation BeamNG export ZIP.')
        self.paint_color=tk.StringVar(value='')
        panel=ttk.Frame(root,padding=18);panel.pack(fill='both',expand=True)
        header=ttk.Frame(panel);header.pack(fill='x')
        ttk.Label(header,text='KDA Vehicle Converter',font=('Segoe UI',18,'bold')).pack(side='left')
        ttk.Label(header,text='V'+VERSION+'  |  '+BUILD,style='Version.TLabel').pack(side='right')
        ttk.Label(panel,text='Automation BeamNG ZIP to KSP. Runs locally, without AI or an internet connection.',style='Muted.TLabel').pack(anchor='w',pady=(4,12))
        capabilities=ttk.Frame(panel,style='Card.TFrame',padding=12);capabilities.pack(fill='x',pady=(0,12));self.capability_panel=capabilities
        for i,(heading,detail) in enumerate(CAPABILITIES):
            capabilities.columnconfigure(i,weight=1,uniform='capability')
            column=ttk.Frame(capabilities,style='Card.TFrame');column.grid(row=0,column=i,sticky='nsew',padx=(0,12) if i<2 else 0)
            ttk.Label(column,text=heading,style='CardHeading.TLabel').pack(anchor='w')
            ttk.Label(column,text=detail,style='Card.TLabel',wraplength=265,justify='left').pack(anchor='w',pady=(5,0))
        self.controls=[]
        def row(label,var,command):
            frame=ttk.Frame(panel);frame.pack(fill='x',pady=5);ttk.Label(frame,text=label,width=17).pack(side='left')
            entry=ttk.Entry(frame,textvariable=var);entry.pack(side='left',fill='x',expand=True)
            button=ttk.Button(frame,text='Browse...',command=command);button.pack(side='left',padx=(8,0));self.controls.extend([entry,button])
        row('Export ZIP',self.zip,self.choose_zip);row('KSP folder',self.ksp,self.choose_ksp);row('Output folder',self.output,self.choose_output)
        options=ttk.Frame(panel);options.pack(fill='x',pady=9)
        ttk.Label(options,text='Texture quality').pack(side='left');quality=ttk.Combobox(options,textvariable=self.quality,values=['High (2048)','Balanced (1024)','Light (512)'],width=17,state='readonly');quality.pack(side='left',padx=8)
        ttk.Label(options,text='Planned seats').pack(side='left',padx=(10,0));seats=ttk.Spinbox(options,from_=0,to=8,textvariable=self.seats,width=4);seats.pack(side='left',padx=8);self.controls.extend([quality,seats])
        self.paint_button=ttk.Button(options,text='Body paint: Export',command=self.choose_paint);self.paint_button.pack(side='left',padx=(12,4));self.controls.append(self.paint_button)
        reset_paint=ttk.Button(options,text='Reset paint',command=self.reset_paint);reset_paint.pack(side='left');self.controls.append(reset_paint)
        games=ttk.Frame(panel);games.pack(fill='x',pady=5);ttk.Label(games,text='Add craft to save',width=17).pack(side='left')
        self.saves=ttk.Combobox(games,textvariable=self.save,state='readonly');self.saves.pack(side='left',fill='x',expand=True)
        refresh=ttk.Button(games,text='Refresh saves',command=self.refresh_saves);refresh.pack(side='left',padx=(8,0));self.controls.extend([self.saves,refresh])
        ttk.Label(panel,text='Install KDA base GameData separately before adding cars. The optional garage building is separate.\nExport with Merge All Fixtures disabled for separate details. Add KDA race seats and doorway markers in the editor, then assign crew.',style='Muted.TLabel',wraplength=860).pack(anchor='w',pady=(10,8))
        actions=ttk.Frame(panel);actions.pack(fill='x',pady=5)
        self.convert_button=ttk.Button(actions,text='Convert only',command=lambda:self.start(False));self.convert_button.pack(side='left')
        self.install_button=ttk.Button(actions,text='Convert and install',command=lambda:self.start(True));self.install_button.pack(side='left',padx=8)
        self.last_button=ttk.Button(actions,text='Install last conversion',command=self.install_last,state='disabled');self.last_button.pack(side='left')
        ttk.Button(actions,text='Open output',command=self.open_output).pack(side='right')
        self.progress=ttk.Progressbar(panel,mode='indeterminate');self.progress.pack(fill='x',pady=(12,5))
        ttk.Label(panel,textvariable=self.status,wraplength=750).pack(anchor='w',pady=(0,7))
        logs=ttk.Frame(panel);logs.pack(fill='both',expand=True);self.logbox=tk.Text(logs,height=8,wrap='word',state='disabled',font=('Consolas',9),background='#141414',foreground='#dddddd',insertbackground='#eeeeee',selectbackground='#176456',relief='flat',highlightthickness=1,highlightbackground='#333333',padx=10,pady=8);self.logbox.pack(side='left',fill='both',expand=True)
        scroll=ttk.Scrollbar(logs,command=self.logbox.yview);scroll.pack(side='right',fill='y');self.logbox.configure(yscrollcommand=scroll.set)
        self.refresh_saves();root.after(100,self.poll);root.protocol('WM_DELETE_WINDOW',self.close)

    def choose_paint(self):
        _,color=colorchooser.askcolor(color=self.paint_color.get() or '#ff9500',title='Override primary body paint')
        if color:self.paint_color.set(color);self.paint_button.configure(text='Body paint: '+color)
    def reset_paint(self):
        self.paint_color.set('');self.paint_button.configure(text='Body paint: Export')
    def choose_zip(self):
        folder=Path(os.environ.get('LOCALAPPDATA',''))/'BeamNG/BeamNG.drive/current/mods'
        name=filedialog.askopenfilename(title='Automation BeamNG export',filetypes=[('BeamNG ZIP exports','*.zip')],initialdir=str(folder) if folder.exists() else None)
        if name:self.zip.set(name);self.reset_paint()
    def choose_ksp(self):
        name=filedialog.askdirectory(title='Folder containing KSP_x64.exe',initialdir=self.ksp.get())
        if name:self.ksp.set(name);self.refresh_saves()
    def choose_output(self):
        name=filedialog.askdirectory(title='Folder for conversion packages')
        if name:self.output.set(name)
    def refresh_saves(self):
        base=Path(self.ksp.get())/'saves'
        try:names=sorted(p.name for p in base.iterdir() if p.is_dir() and (p/'persistent.sfs').is_file())
        except OSError:names=[]
        self.saves['values']=['']+names
        if self.save.get() not in names:self.save.set(names[0] if len(names)==1 else '')
    def log(self,text):self.events.put(('log',str(text)))
    def set_busy(self,value):
        self.busy=value
        for widget in self.controls:
            widget.configure(state='disabled' if value else 'readonly' if isinstance(widget,ttk.Combobox) else 'normal')
        self.convert_button.configure(state='disabled' if value else 'normal');self.install_button.configure(state='disabled' if value else 'normal')
        self.last_button.configure(state='normal' if self.package and not value else 'disabled')
        if value:self.progress.start(12)
        else:self.progress.stop()
    def start(self,want_install):
        try:
            source=Path(self.zip.get());output=Path(self.output.get());seats=int(self.seats.get());quality={'High (2048)':'quality','Balanced (1024)':'balanced','Light (512)':'lightweight'}[self.quality.get()]
            if not source.is_file():raise ValueError('Choose an existing BeamNG export ZIP.')
            if not 0<=seats<=8:raise ValueError('Seat count must be between 0 and 8.')
            if not self.output.get().strip():raise ValueError('Choose an output folder.')
            ksp=self.ksp.get();save=self.save.get() or None;paint_color=self.paint_color.get() or None
            if want_install and not (Path(ksp)/'KSP_x64.exe').is_file() and not (Path(ksp)/'KSP.exe').is_file():raise ValueError('Choose the KSP installation folder first.')
        except (ValueError,KeyError) as error:messagebox.showerror('Check settings',str(error));return
        try:
            self.settings.parent.mkdir(parents=True,exist_ok=True);self.settings.write_text(json.dumps({'ksp':ksp,'output':str(output),'quality':self.quality.get(),'seats':seats,'save':save or ''}),encoding='utf-8')
        except OSError:self.log('Could not save preferences; this conversion can continue.')
        self.set_busy(True);self.status.set('Starting conversion...')
        def work():
            try:
                package=output/(source.stem+'-'+datetime.now().strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:4])
                convert(source,package,quality,seats,log=self.log,paint_color=paint_color);self.events.put(('package',package))
                if want_install:self.do_install(package,ksp,save)
                self.events.put(('done','Installed. Launch KSP and load the KDA craft in SPH.' if want_install else 'Converted and validated. Package ready for installation.'))
            except Exception as error:self.log(traceback.format_exc());self.events.put(('error',str(error)))
        threading.Thread(target=work,daemon=False).start()
    def do_install(self,package,ksp,save):
        self.log('Checking game and installing with backups...')
        try:result=install(package,ksp,save,log=self.log)
        except PermissionError:
            self.log('Windows administrator approval is needed to install in this KSP folder.');result=elevated_install(package,ksp,save)
        self.log('Installed craft: '+result['craft']);self.log('Backup folder: '+result['backup'])
    def install_last(self):
        if not self.package:return
        package=self.package;ksp=self.ksp.get();save=self.save.get() or None;self.set_busy(True)
        def work():
            try:self.do_install(package,ksp,save);self.events.put(('done','Installed. Launch KSP and load the KDA craft in SPH.'))
            except Exception as error:self.events.put(('error',str(error)))
        threading.Thread(target=work,daemon=False).start()
    def poll(self):
        while not self.events.empty():
            kind,text=self.events.get()
            if kind=='package':self.package=Path(text)
            elif kind=='log':
                self.logbox.configure(state='normal');self.logbox.insert('end',text+'\n');self.logbox.see('end');self.logbox.configure(state='disabled');self.status.set(text)
            else:
                self.set_busy(False);self.status.set(text)
                if self.package:
                    try:(self.package/'desktop-session.log').write_text(self.logbox.get('1.0','end'),encoding='utf-8')
                    except OSError:pass
                if kind=='error':messagebox.showerror('Conversion or installation stopped',text)
        self.root.after(100,self.poll)
    def open_output(self):
        folder=self.package or Path(self.output.get())
        if folder.exists():os.startfile(str(folder))
        else:messagebox.showinfo('Output folder','Convert a car first to create the output folder.')
    def close(self):
        if self.busy:messagebox.showinfo('Working','Please wait for conversion or installation to finish before closing.');return
        self.root.destroy()

def run(smoke=None):
    root=tk.Tk();app=App(root)
    if smoke:
        def finish():
            # Inspect only our widgets. Desktop capture is unrelated to this
            # application and can show another window when this one is hidden.
            fits=all(w.winfo_height()>=w.winfo_reqheight() for column in app.capability_panel.winfo_children() for w in column.winfo_children())
            Path(smoke).write_text(json.dumps({'gui_started':True,'window_title':root.title(),'version':VERSION,'build':BUILD,'theme':ttk.Style(root).theme_use(),'background':root.cget('background'),'capabilities':CAPABILITIES,'capability_layout_fits':fits,'window_size':[root.winfo_width(),root.winfo_height()],'actions':[app.convert_button['text'],app.install_button['text'],app.last_button['text']]}),encoding='utf-8');root.destroy()
        root.after(1000,finish)
    root.mainloop()
