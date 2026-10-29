#!/usr/bin/env python3
"""Extract the CLAD schema from generated cozmoclad Python modules.

The generated Python encodes the exact wire layout in its own unpack code, so we
read the operations instead of re-deriving types from comments.
"""
import argparse
import json
import os
import re
import sys
import zipfile

CLASS_RE = re.compile(r'^class (\w+)\(object\):\n  "(.*?)"', re.M)
FQN_RE = re.compile(r'^(Anki(?:\.\w+)+) = (\w+)$', re.M)
TAG_RE = re.compile(r'^\s{4}(\w+)\s+=\s+(0x[0-9a-fA-F]+|\d+)\s*(?:#\s*(\S+))?\s*$', re.M)
UNTAG_RE = re.compile(r'_tag_unpack_methods\[(\d+)\] = lambda reader: (.+)')
ENUM_RE = re.compile(r'^\s{2}(\w+)\s+=\s+(-?(?:0x[0-9a-fA-F]+|\d+))\s*$', re.M)
OP_RE = re.compile(r'^\s*_(\w+) = (.+)$')


class ExtractError(Exception):
    pass


def kind_of(doc):
    if doc.startswith('Automatically-generated ') and doc.endswith(' enumeration.'):
        return 'enum', doc.split()[1]
    if doc.startswith('Generated message-passing union'):
        return 'union', None
    if doc.startswith('Generated message-passing structure'):
        return 'struct', None
    if doc.startswith('Generated message-passing message'):
        return 'message', None
    return None, None


def body_of(text, start):
    """Return the class body starting at start up to the next top-level class."""
    nxt = text.find('\nclass ', start)
    return text[start:] if nxt < 0 else text[start:nxt]


def method_body(body, name):
    m = re.search(r'^  def %s\(.*?\):(.*?)(?=^  @|^  def |\Z)' % name, body, re.M | re.S)
    return m.group(1) if m else None


def parse_ops(body, fqn, enums):
    """Parse an unpack_from body into ordered wire operations."""
    src = method_body(body, 'unpack_from')
    if src is None:
        raise ExtractError('%s: no unpack_from' % fqn)
    ops = []
    for line in src.splitlines():
        m = OP_RE.match(line)
        if not m:
            continue
        name, rhs = m.group(1), m.group(2).strip()
        op = parse_rhs(rhs, fqn, enums)
        if 'type' in op and op['type'].endswith('.unpack_from'):
            op['type'] = op['type'][:-len('.unpack_from')]
        ops.append({'name': name, **op})
    return ops


def resolve_count(expr, fqn, enums):
    expr = expr.strip()
    if re.fullmatch(r'\d+', expr):
        return int(expr)
    # e.g. Anki.Vision.FacialExpression.Count
    owner, _, member = expr.rpartition('.')
    if owner in enums and member in enums[owner]['values']:
        return enums[owner]['values'][member]
    raise ExtractError('%s: cannot resolve array size %r' % (fqn, expr))


def parse_rhs(rhs, fqn, enums):
    m = re.fullmatch(r"reader\.read\('(\w)'\)", rhs)
    if m:
        return {'op': 'scalar', 'code': m.group(1)}
    m = re.fullmatch(r"bool\(reader\.read\('(\w)'\)\)", rhs)
    if m:
        return {'op': 'bool', 'code': m.group(1)}
    m = re.fullmatch(r"reader\.read_string\('(\w)'\)", rhs)
    if m:
        return {'op': 'string', 'len_code': m.group(1)}
    m = re.fullmatch(r"list\(map\(bool, reader\.read_farray\('(\w)', (.+)\)\)\)", rhs)
    if m:
        return {'op': 'bool_array', 'code': m.group(1), 'count': resolve_count(m.group(2), fqn, enums)}
    m = re.fullmatch(r"list\(map\(bool, reader\.read_varray\('(\w)', '(\w)'\)\)\)", rhs)
    if m:
        return {'op': 'bool_varray', 'code': m.group(1), 'len_code': m.group(2)}
    m = re.fullmatch(r"reader\.read_farray\('(\w)', (.+)\)", rhs)
    if m:
        return {'op': 'array', 'code': m.group(1), 'count': resolve_count(m.group(2), fqn, enums)}
    m = re.fullmatch(r"reader\.read_varray\('(\w)', '(\w)'\)", rhs)
    if m:
        return {'op': 'varray', 'code': m.group(1), 'len_code': m.group(2)}
    m = re.fullmatch(r"reader\.read_string_varray\('(\w)', '(\w)'\)", rhs)
    if m:
        return {'op': 'string_varray', 'len_code': m.group(1), 'count_code': m.group(2)}
    m = re.fullmatch(r"reader\.read_object\((\w[\w\.]*)\.unpack_from\)", rhs)
    if m:
        return {'op': 'object', 'type': m.group(1)}
    m = re.fullmatch(r"reader\.read_object_farray\((\w[\w\.]*), (.+)\)", rhs)
    if m:
        return {'op': 'object_array', 'type': m.group(1), 'count': resolve_count(m.group(2), fqn, enums)}
    m = re.fullmatch(r"reader\.read_object_varray\((\w[\w\.]*), '(\w)'\)", rhs)
    if m:
        return {'op': 'object_varray', 'type': m.group(1), 'len_code': m.group(2)}
    raise ExtractError('%s: unsupported field expression %r' % (fqn, rhs))



_IDENT_RE = re.compile(r'(?<![0-9A-Za-z_.])[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*')


def evaluate_enum_value(expr, current_values, enums, fqn):
    """Evaluates a CLAD enum value, which may reference other enums and members.

    Generated CLAD enums express some members as expressions such as
    ``ActionResultCategory.ABORT * ARCBitShift.SHIFT + 1``.
    """
    expr = expr.strip()
    if re.fullmatch(r'-?(?:0x[0-9a-fA-F]+|\d+)', expr):
        return int(expr, 0)

    def substitute(match):
        token = match.group(0)
        if '.' in token:
            owner, member = token.rsplit('.', 1)
            candidates = [key for key in enums if key.rsplit('.', 1)[1] == owner]
            if not candidates:
                raise ExtractError('%s: unknown enum reference %r' % (fqn, token))
            namespace = fqn.rsplit('.', 1)[0]
            preferred = [key for key in candidates if key.rsplit('.', 1)[0] == namespace]
            key = (preferred or candidates)[0]
            if member not in enums[key]['values']:
                raise ExtractError('%s: unknown member %r of %s' % (fqn, member, key))
            return str(enums[key]['values'][member])

        if token not in current_values:
            raise ExtractError('%s: unknown member reference %r' % (fqn, token))
        return str(current_values[token])

    substituted = _IDENT_RE.sub(substitute, expr)
    substituted = re.sub(r'0x[0-9a-fA-F]+', lambda m: str(int(m.group(0), 16)), substituted)
    if not re.fullmatch(r'[-+*/%()<>|&^~ \t0-9]+', substituted):
        raise ExtractError('%s: unsafe enum expression %r' % (fqn, expr))
    return int(eval(substituted, {'__builtins__': {}}, {}))  # noqa: S307 - digits and operators only


def collect_enums(path, types, enums, module):
    text = open(path, encoding='utf-8').read()
    fqns = {name: fqn for fqn, name in FQN_RE.findall(text)}
    for m in CLASS_RE.finditer(text):
        name, doc = m.group(1), m.group(2)
        kind, underlying = kind_of(doc)
        if kind != 'enum':
            continue
        fqn = fqns.get(name)
        if fqn is None:
            raise ExtractError('%s: class %s has no Anki assignment' % (path, name))
        body = body_of(text, m.start())
        values = {}
        for line in body.splitlines():
            vm = re.match(r'^  ([A-Za-z_]\w*)\s+=\s+(.+?)\s*$', line)
            if not vm:
                continue
            values[vm.group(1)] = evaluate_enum_value(
                vm.group(2).split('#', 1)[0].strip(), values, enums, fqn)
        enums[fqn] = {'kind': 'enum', 'underlying': underlying, 'values': values, 'module': module}
        types[fqn] = enums[fqn]


def parse_module(path, types, enums, module):
    text = open(path, encoding='utf-8').read()
    fqns = {name: fqn for fqn, name in FQN_RE.findall(text)}
    for m in CLASS_RE.finditer(text):
        name, doc = m.group(1), m.group(2)
        kind, underlying = kind_of(doc)
        if kind is None or kind == 'enum':
            continue
        fqn = fqns.get(name)
        if fqn is None:
            raise ExtractError('%s: class %s has no Anki assignment' % (path, name))
        body = body_of(text, m.start())
        if kind == 'union':
            tag_src = method_body(body, 'unpack_from')
            tm = re.search(r"reader\.read\('(\w)'\)", tag_src)
            if not tm:
                raise ExtractError('%s: union without tag read' % fqn)
            tag_block = re.search(r'\n  class Tag\(object\):(.*?)(?=\n  @|\n  def |\n  _tag_)', body, re.S)
            if not tag_block:
                raise ExtractError('%s: union without Tag block' % fqn)
            unpackers = {}
            for um in UNTAG_RE.finditer(body):
                unpackers[int(um.group(1))] = parse_rhs(um.group(2).strip(), fqn, enums)
            tags = []
            for n, v, comment in TAG_RE.findall(tag_block.group(1)):
                value = int(v, 0)
                payload = unpackers.get(value)
                if payload is None:
                    raise ExtractError('%s: no unpack method for tag %d' % (fqn, value))
                if 'type' in payload and payload['type'].endswith('.unpack_from'):
                    payload['type'] = payload['type'][:-len('.unpack_from')]
                tags.append({'name': n, 'value': value, 'payload': payload})
            types[fqn] = {'kind': 'union', 'tag_code': tm.group(1), 'alternatives': tags, 'module': module}
        else:
            types[fqn] = {'kind': kind, 'fields': parse_ops(body, fqn, enums), 'module': module}


def read_metadata(root):
    """Read package version, app build version and the two CLAD hashes."""
    pkg = os.path.join(root, 'cozmoclad')
    if not os.path.isdir(pkg):
        pkg = root
    meta = {'version': None, 'build_version': None, 'hashes': {}}
    init = os.path.join(pkg, '__init__.py')
    if os.path.isfile(init):
        text = open(init, encoding='utf-8').read()
        for key, field in (('__version__', 'version'), ('__build_version__', 'build_version')):
            m = re.search(r'^%s = "([^"]+)"' % key, text, re.M)
            if m:
                meta[field] = m.group(1)
    for name, field in (('messageEngineToGame_hash', 'to_game'),
                        ('messageGameToEngine_hash', 'to_engine')):
        path = os.path.join(pkg, 'clad', 'externalInterface', name + '.py')
        if os.path.isfile(path):
            m = re.search(r'message\w+Hash = 0x([0-9a-fA-F]+)', open(path, encoding='utf-8').read())
            if m:
                meta['hashes'][field] = m.group(1).lower()
    return meta


def load_package(path, workdir):
    if os.path.isdir(path):
        return path
    if not zipfile.is_zipfile(path):
        raise ExtractError('%s is neither a directory nor a zip/wheel' % path)
    target = os.path.join(workdir, 'cozmoclad')
    with zipfile.ZipFile(path) as zf:
        zf.extractall(workdir)
    return workdir


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--cozmoclad', required=True, help='extracted package dir or .whl')
    ap.add_argument('--workdir', default=None)
    ap.add_argument('--out', required=True)
    args = ap.parse_args()

    workdir = args.workdir
    if workdir is None and os.path.isfile(args.cozmoclad):
        import tempfile
        workdir = tempfile.mkdtemp(prefix='cladgen-')
    root = load_package(args.cozmoclad, workdir) if workdir else args.cozmoclad
    clad_root = os.path.join(root, 'cozmoclad', 'clad')
    if not os.path.isdir(clad_root):
        clad_root = os.path.join(root, 'clad')
    if not os.path.isdir(clad_root):
        raise ExtractError('no clad package under %s' % root)

    modules = []
    roots = [clad_root, os.path.join(root, 'cozmoclad', 'util')]
    for walk_root in roots:
        if not os.path.isdir(walk_root):
            continue
        for base, _, files in os.walk(walk_root):
            for f in sorted(files):
                if f.endswith('.py') and f != '__init__.py':
                    modules.append(os.path.join(base, f))

    def module_name(path):
        rel = os.path.relpath(path, root)
        return rel[:-3].replace(os.sep, '.')

    types, enums = {}, {}
    # Enums first: array sizes may reference their constants.
    for path in modules:
        collect_enums(path, types, enums, module_name(path))
    for path in modules:
        parse_module(path, types, enums, module_name(path))

    # Resolve object references.
    missing = set()
    for fqn, t in types.items():
        if t['kind'] in ('struct', 'message'):
            for f in t['fields']:
                if 'type' in f and f['type'] not in types:
                    missing.add((fqn, f['type']))
        elif t['kind'] == 'union':
            for a in t['alternatives']:
                payload = a['payload']
                if payload['op'] == 'object' and payload['type'] not in types:
                    missing.add((fqn, payload['type']))
    if missing:
        raise ExtractError('unresolved type references: %s' % sorted(missing)[:20])

    schema = {
        'source': 'cozmoclad',
        'cozmoclad': read_metadata(root),
        'types': dict(sorted(types.items())),
    }
    with open(args.out, 'w', encoding='utf-8') as fh:
        json.dump(schema, fh, indent=1, sort_keys=False)
        fh.write('\n')
    counts = {}
    for t in types.values():
        counts[t['kind']] = counts.get(t['kind'], 0) + 1
    print('wrote %s: %d types %s' % (args.out, len(types), counts))


if __name__ == '__main__':
    try:
        main()
    except ExtractError as e:
        print('extract_schema: error: %s' % e, file=sys.stderr)
        sys.exit(1)
