"""Run converter tests with explicit imports and an ignored writable temp root."""
import argparse
import os
from pathlib import Path
import sys
import tempfile
import unittest


def main():
    source = Path(__file__).resolve().parents[1]
    os.chdir(source)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--temp-root', type=Path, default=source / '.build/test-tmp')
    args = parser.parse_args()
    args.temp_root.mkdir(parents=True, exist_ok=True)
    tempfile.tempdir = str(args.temp_root.resolve())
    sys.path.insert(0, str(source / 'converter'))
    suite = unittest.defaultTestLoader.discover(str(source / 'converter'), pattern='test_*.py')
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    return 0 if result.wasSuccessful() else 1


if __name__ == '__main__':
    raise SystemExit(main())
