"""Optional external source-export fixtures for integration tests."""
import os
from pathlib import Path
from unittest import SkipTest


def export_fixture(name):
    root = Path(os.environ.get('KDA_TEST_EXPORTS', Path(__file__).resolve().parents[1] / '.build/exports'))
    path = root / name / 'extracted'
    if not path.is_dir() or not any(path.rglob('info.json')):
        raise SkipTest('Set KDA_TEST_EXPORTS to extracted integration exports: ' + name)
    return path
