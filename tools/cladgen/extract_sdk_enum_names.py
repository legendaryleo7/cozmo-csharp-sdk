#!/usr/bin/env python3
"""Extract the Python SDK's explicit enum-wrapper and object-catalogue entries.

The result is the JSON name map consumed by ``emit_sdk_enums.py``.

The Python wrappers are not always mechanical PascalCase conversions of the CLAD member
they wrap (``audio.py`` maps ``Music_Global_Stop`` to the public attribute
``MusicGlobalStop``).  ``emit_sdk_enums.py`` therefore consumes this committed mapping
instead of guessing names, and verifies every mapped CLAD id against the generated
schema.

``objects.py`` uses namedtuple-style catalogues instead of ``CladEnumWrapper``
subclasses, and declares the LightCube object ids as module constants.  Both forms are
extracted here as well, so rerunning the command does not lose the catalogue mappings.

One-off regeneration from the Python SDK sources::

    python3 tools/cladgen/extract_sdk_enum_names.py \
      --python-src <path-to>/ddl-cozmo-python-sdk/src/cozmo \
      --cozmoclad <path-to>/cozmoclad/cozmoclad-3.6.6-py3-none-any.whl \
      --schema tools/cladgen/clad_schema.json \
      --out tools/cladgen/sdk_enum_names.json
"""
import argparse
import json
import os
import re
import sys

# (wrapper class name, source file)
WRAPPERS = [
    ('BehaviorTypes', 'behavior.py'),
    ('AudioEvents', 'audio.py'),
    ('NoteTypes', 'song.py'),
    ('NoteDurations', 'song.py'),
]

# objects.py's catalogues are namedtuple-style classes rather than CladEnumWrapper
# subclasses: each attribute names its CLAD member explicitly.  The Python module also
# declares the LightCube object ids as module-level constants, which the C# catalogue
# groups with the custom object types for one ObjectType lookup surface.
CATALOGUE_WRAPPERS = [
    {
        'class': 'CustomObjectTypes',
        'filename': 'objects.py',
        'entryType': '_CustomObjectType',
        'moduleConstants': ['LightCube1Id', 'LightCube2Id', 'LightCube3Id'],
    },
    {
        'class': 'CustomObjectMarkers',
        'filename': 'objects.py',
        'entryType': '_CustomObjectMarker',
        'moduleConstants': [],
    },
]

# Python has no module-level charger object id.  The world layer still needs the
# ObjectType.Charger_Basic id by name, so it is the one supplemental catalogue entry.
SUPPLEMENTAL_ENTRIES = [
    ('CustomObjectTypes', 'ChargerBasic', 'Anki.Cozmo.ObjectType', 'Charger_Basic'),
]

# Python SDK module alias -> fully qualified CLAD type prefix.
CLAD_PREFIXES = {
    '_clad_to_engine_anki': 'Anki',
    '_clad_to_engine_cozmo': 'Anki.Cozmo',
    '_clad_to_engine_iface': 'Anki.Cozmo.ExternalInterface',
    '_clad_to_game_anki': 'Anki',
    '_clad_to_game_cozmo': 'Anki.Cozmo',
    '_clad_to_game_iface': 'Anki.Cozmo.ExternalInterface',
}

ENTRY_RE = re.compile(
    r'^\s*(?P<decl>_?[A-Za-z]\w*)\s*=\s*_entry_type\('
    r'"(?P<entry>[^"]+)",\s*_clad_enum\.(?P<clad>\w+)\)',
    re.MULTILINE,
)
CLAD_ENUM_RE = re.compile(
    r'^\s*_clad_enum\s*=\s*(?P<alias>_clad_to_\w+)\.(?P<suffix>[\w.]+)\s*$',
    re.MULTILINE,
)


def parse_class(source, class_name):
    """Return the wrapper's CLAD enum, entries and _init_class flags."""
    header = re.search(r'^class %s\(CladEnumWrapper\):' % re.escape(class_name), source, re.MULTILINE)
    if header is None:
        raise SystemExit('class %s was not found' % class_name)

    start = header.end()
    tail = source[start:]
    next_class = re.search(r'^(class |%s\._init_class)' % re.escape(class_name), tail, re.MULTILINE)
    if next_class is None:
        block = tail
    else:
        block = tail[:next_class.start()]

    clad_match = CLAD_ENUM_RE.search(block)
    if clad_match is None:
        raise SystemExit('class %s has no _clad_enum' % class_name)
    alias = clad_match.group('alias')
    if alias not in CLAD_PREFIXES:
        raise SystemExit('unknown CLAD alias %s in class %s' % (alias, class_name))
    enum_fqn = '%s.%s' % (CLAD_PREFIXES[alias], clad_match.group('suffix'))

    init_re = re.compile(
        r'^\s*%s\._init_class\((?P<args>[^)]*)\)\s*$' % re.escape(class_name),
        re.MULTILINE)
    init_match = init_re.search(source)
    if init_match is None:
        raise SystemExit('class %s has no _init_class call' % class_name)
    add_missing = 'add_missing_definitions=False' not in init_match.group('args')

    entries = []
    for match in ENTRY_RE.finditer(block):
        decl = match.group('decl')
        entry_name = match.group('entry')
        clad = match.group('clad')
        public = decl[1:] if decl.startswith('_') else decl
        item = {'public': public, 'clad': clad}
        if entry_name != public:
            # The Python wrapper's display name differs from its attribute name
            # (audio.py lines 292 and 300 use "Music_Fun_Loop" for MusicFunLoop).
            item['pythonEntryName'] = entry_name
        if decl.startswith('_'):
            item['pythonAttribute'] = decl
            item['internal'] = True
        entries.append(item)

    if not entries:
        raise SystemExit('class %s has no explicit entries' % class_name)
    return enum_fqn, entries, add_missing


def split_clad_reference(alias, path, class_name):
    """Split a ``alias.Enum.Member`` reference into its enum FQN and member name."""
    if alias not in CLAD_PREFIXES:
        raise SystemExit('unknown CLAD alias %s in class %s' % (alias, class_name))
    if '.' not in path:
        raise SystemExit('invalid CLAD reference %s.%s in class %s' % (alias, path, class_name))
    suffix, member = path.rsplit('.', 1)
    return '%s.%s' % (CLAD_PREFIXES[alias], suffix), member


def parse_catalogue(source, class_name, entry_type, module_constants):
    """Return a namedtuple-style catalogue's CLAD enum and explicit entries."""
    header = re.search(r'^class %s:' % re.escape(class_name), source, re.MULTILINE)
    if header is None:
        raise SystemExit('class %s was not found' % class_name)

    tail = source[header.end():]
    next_class = re.search(r'^class ', tail, re.MULTILINE)
    block = tail if next_class is None else tail[:next_class.start()]

    entry_re = re.compile(
        r'^\s*(?P<decl>\w+)\s*=\s*%s\(\s*"(?P<entry>[^"]+)"\s*,\s*'
        r'(?P<alias>_clad_to_\w+)\.(?P<path>[\w.]+)\s*\)\s*$' % re.escape(entry_type),
        re.MULTILINE)

    enum_fqn = None
    entries = []
    for match in entry_re.finditer(block):
        entry_enum, clad = split_clad_reference(
            match.group('alias'), match.group('path'), class_name)
        if enum_fqn is None:
            enum_fqn = entry_enum
        elif entry_enum != enum_fqn:
            raise SystemExit(
                'class %s mixes CLAD enums %s and %s' % (class_name, enum_fqn, entry_enum))
        public = match.group('decl')
        item = {'public': public, 'clad': clad}
        if match.group('entry') != public:
            item['pythonEntryName'] = match.group('entry')
        entries.append(item)

    if enum_fqn is None:
        raise SystemExit('class %s has no explicit entries' % class_name)

    for constant in module_constants:
        constant_re = re.compile(
            r'^%s\s*=\s*(?P<alias>_clad_to_\w+)\.(?P<path>[\w.]+)\s*$' % re.escape(constant),
            re.MULTILINE)
        match = constant_re.search(source)
        if match is None:
            raise SystemExit('module constant %s was not found' % constant)
        entry_enum, clad = split_clad_reference(
            match.group('alias'), match.group('path'), class_name)
        if entry_enum != enum_fqn:
            raise SystemExit(
                'module constant %s uses CLAD enum %s, expected %s' %
                (constant, entry_enum, enum_fqn))
        entries.append({'public': constant, 'clad': clad})

    return enum_fqn, entries


def record_wrapper(wrappers, types, class_name, filename, enum_fqn, entries, add_missing,
                   cozmoclad_path):
    """Validate one wrapper's entries and add it to the JSON document."""
    enum = types.get(enum_fqn)
    if enum is None:
        raise SystemExit('%s: %s is not in the schema' % (class_name, enum_fqn))
    values = enum['values']
    python_enum = resolve_enum(enum_fqn, cozmoclad_path) if cozmoclad_path else None

    seen = set()
    for entry in entries:
        clad = entry['clad']
        if clad not in values:
            raise SystemExit('%s: CLAD member %s is not in the schema' % (class_name, clad))
        if entry['public'] in seen:
            raise SystemExit('%s: duplicate public name %s' % (class_name, entry['public']))
        seen.add(entry['public'])
        schema_id = values[clad]
        if python_enum is not None:
            python_id = getattr(python_enum, clad)
            if python_id != schema_id:
                raise SystemExit(
                    '%s: %s has id %d in the Python SDK but %d in the schema' %
                    (class_name, clad, python_id, schema_id))
            entry['id'] = python_id

    wrappers[class_name] = {
        'enum': enum_fqn,
        'source': filename,
        'addMissing': add_missing,
        'entries': entries,
    }


def resolve_enum(enum_fqn, cozmoclad_path):
    """Return the Python CLAD enum object, importing cozmoclad from a wheel or directory."""
    if cozmoclad_path:
        sys.path.insert(0, cozmoclad_path)
    from cozmoclad.clad.externalInterface import messageGameToEngine  # noqa: E402

    value = messageGameToEngine
    for part in enum_fqn.split('.'):
        value = getattr(value, part)
    return value


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--python-src', required=True, help='path to cozmo/ in the Python SDK')
    parser.add_argument('--cozmoclad', default=None,
                        help='cozmoclad wheel or extracted directory, used to verify ids')
    parser.add_argument('--schema', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()

    schema = json.load(open(args.schema, encoding='utf-8'))
    types = schema['types']

    wrappers = {}
    for class_name, filename in WRAPPERS:
        path = os.path.join(args.python_src, filename)
        source = open(path, encoding='utf-8').read()
        enum_fqn, entries, add_missing = parse_class(source, class_name)
        record_wrapper(wrappers, types, class_name, filename, enum_fqn, entries, add_missing,
                       args.cozmoclad)

    for spec in CATALOGUE_WRAPPERS:
        class_name = spec['class']
        filename = spec['filename']
        path = os.path.join(args.python_src, filename)
        source = open(path, encoding='utf-8').read()
        enum_fqn, entries = parse_catalogue(
            source, class_name, spec['entryType'], spec['moduleConstants'])
        for owner, public, supplemental_enum, clad in SUPPLEMENTAL_ENTRIES:
            if owner != class_name:
                continue
            if supplemental_enum != enum_fqn:
                raise SystemExit(
                    '%s: supplemental %s uses CLAD enum %s, expected %s' %
                    (class_name, public, supplemental_enum, enum_fqn))
            entries.append({'public': public, 'clad': clad})
        record_wrapper(wrappers, types, class_name, filename, enum_fqn, entries, False,
                       args.cozmoclad)

    document = {
        '_comment': (
            'Explicit CladEnumWrapper and objects.py catalogue entries extracted from the '
            'Python SDK 1.4.12 sources. Do not edit by hand; run '
            'tools/cladgen/extract_sdk_enum_names.py.'),
        'cozmocladVersion': (schema.get('cozmoclad') or {}).get('version'),
        'wrappers': wrappers,
    }
    os.makedirs(os.path.dirname(args.out) or '.', exist_ok=True)
    with open(args.out, 'w', encoding='utf-8') as handle:
        json.dump(document, handle, indent=2)
        handle.write('\n')

    total = sum(len(wrapper['entries']) for wrapper in wrappers.values())
    print('wrote %d explicit entries across %d wrappers to %s' % (total, len(wrappers), args.out))
    for class_name, wrapper in wrappers.items():
        print('  %s: %d entries (%s)' % (class_name, len(wrapper['entries']), wrapper['enum']))


if __name__ == '__main__':
    main()
