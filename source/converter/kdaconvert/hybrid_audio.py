"""Original shared KDA audio metadata. Never reads exported recordings."""
import hashlib,json,math

FAMILIES = {'I3': 'i3', 'I4': 'i4', 'I5': 'i5', 'I6': 'i6', 'V6': 'v6',
            'V8': 'v8_cross', 'V10': 'v10', 'V12': 'v12',
            'B4': 'boxer4', 'B6': 'boxer6', 'F4': 'boxer4', 'F6': 'boxer6'}
RPMS = (750, 1250, 2000, 3200, 5000, 7500, 11000, 16000)


def metadata(engine):
    layout = str(engine.get('layout', 'Unknown')).upper()
    known = layout in FAMILIES
    family = FAMILIES.get(layout, 'i4')
    value = engine.get('displacement_l', 0)
    liters = float(value) if isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and .1 <= value <= 20 else 0
    assumptions = []
    if not known: assumptions.append('Unknown engine layout uses an Inline 4 sound preset; override Engine voice in Garage / Tune / Sound / FX.')
    if layout == 'V8': assumptions.append('V8 defaults to a cross-plane sound preset; export metadata does not establish the crank type. Flat-plane is an editor override.')
    if layout in ('B4', 'F4'): assumptions.append('Boxer 4 uses an unequal-header sound preset; header geometry is not inferred.')
    return dict(type='kda.hybrid', version=1, family=family,
                layout=layout if known else 'I4', displacement_l=liters,
                shared_root='KDAVehicles/Sounds/Hybrid/' + family,
                source='Original KDA combustion synthesis, shared offline banks and runtime intake/boost/shift layers',
                exported_recordings_copied=False, assumptions=assumptions,
                samples=[], effects=[], sample_rate_hz=22050)


def config(audio):
    return ('audioRoot = ' + audio['shared_root'] + '\n'
            'voiceLayout = ' + audio['layout'] + '\n'
            'voiceDisplacement = ' + format(audio['displacement_l'], '.8g') + '\n'
            'engineVoice = 0\nengineTone = 0.55\nexhaustSound = 0\n'
            'sampleRpms = ' + ','.join(map(str, RPMS)) + '\n')


def write(manifest, vehicle, dest):
    # `vehicle` intentionally unused: no blend JSON, WAV or FMOD data is read.
    from .ksp import module
    audio = metadata(manifest['engine'])
    body = dest / 'Parts/body.cfg'
    text = body.read_text(encoding='utf-8')
    body.write_text(text.rsplit('}', 1)[0] + module('KDASampleAudio', config(audio)) + '}\n', encoding='utf-8')
    manifest['audio'] = audio
    for message in audio['assumptions']:
        manifest.setdefault('warnings', []).append(message)


def verify_shared_bank(base, family):
    """Fail before installation if the matching base audio is incomplete."""
    if family not in FAMILIES.values(): raise ValueError('Unsupported hybrid engine voice')
    folder=base/'Sounds/Hybrid'
    try:
        data=json.loads((folder/'bank.json').read_text(encoding='utf-8'))
        if data.get('version')!=1:raise ValueError('Unsupported hybrid bank version')
        entries={item['path']:item['sha256'] for item in data['files']}
        entries['intake.wav']=data['intake']['sha256']
        required=[family+'/rpm_'+str(i)+'_'+str(bank)+'.wav' for i in range(8) for bank in range(2)]+['intake.wav']
        for relative in required:
            path=folder/relative
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest()!=entries.get(relative):
                raise ValueError('Missing or modified hybrid sound: '+relative)
    except (OSError,KeyError,TypeError,json.JSONDecodeError,ValueError) as error:
        raise ValueError('Install this branch\'s complete base GameData first; the shared hybrid audio bank is missing, modified or incompatible. '+str(error)) from error
