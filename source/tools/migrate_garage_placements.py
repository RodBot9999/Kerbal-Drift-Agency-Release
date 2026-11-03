"""Write a separate KK placement file using the current KDA garage catalog ID.

Supply the source catalog identifier explicitly. Position, rotation, scale,
UUIDs and all unrelated settings remain byte-for-byte unchanged.
"""
from pathlib import Path
import argparse
import re

CATALOG = 'KDAGarage'


def migrate(data: bytes, source_catalog: str) -> tuple[bytes, int]:
    if not source_catalog or any(c in source_catalog for c in '\r\n='):
        raise ValueError('Supply one source catalog identifier.')
    text = data.decode('utf-8-sig')
    pattern = r'(?m)^([ \t]*pointername[ \t]*=[ \t]*)' + re.escape(source_catalog) + r'([ \t]*\r?$)'
    text, count = re.subn(pattern, lambda m: m[1] + CATALOG + m[2], text)
    if not count:
        raise ValueError('No matching garage catalog entry was found.')
    encoded = text.encode('utf-8')
    return (b'\xef\xbb\xbf' + encoded if data.startswith(b'\xef\xbb\xbf') else encoded), count


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--from-catalog', required=True)
    args = parser.parse_args()
    if args.input.resolve() == args.output.resolve() or args.output.exists():
        parser.error('Output must be a new file, separate from the input.')
    data, count = migrate(args.input.read_bytes(), args.from_catalog)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('xb') as stream:
        stream.write(data)
    print(f'Migrated {count} garage catalog entry/entries; input preserved: {args.output}')


if __name__ == '__main__':
    main()
