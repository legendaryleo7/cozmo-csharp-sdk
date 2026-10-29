#!/usr/bin/env python3
"""Generate C# oracle tests that compare the C# CLAD layer with cozmoclad 3.6.6.

For every message in the two wire unions this builds a deterministic value in
Python, packs it, and emits the equivalent C# construction plus the expected
bytes.  The C# test then re-packs and compares, and round-trips the bytes.
"""
import argparse
import json
import os
import sys

MAX_LITERAL_ARRAY = 64


class Values:
    """Deterministic value generator keyed on a counter."""

    INTS = {
        'b': [1, -2, 7], 'B': [1, 200, 3],
        'h': [2, -3, 300], 'H': [2, 600, 3],
        'i': [3, -4, 70000], 'I': [3, 70000, 4],
        'q': [4, -5, 5000000000], 'Q': [4, 5000000000, 5],
    }
    FLOATS = {'f': [0.5, -1.25, 3.75], 'd': [0.5, -1.25, 3.75]}
    STRINGS = ['ab', 'z', 'hello']

    def __init__(self):
        self.n = 0

    def pick(self, seq):
        value = seq[self.n % len(seq)]
        self.n += 1
        return value


def build_field(field, types, values, depth):
    op = field['op']
    if op == 'scalar':
        if field['code'] in values.FLOATS:
            return values.pick(values.FLOATS[field['code']])
        return values.pick(values.INTS[field['code']])
    if op == 'bool':
        return values.pick([True, False])
    if op == 'string':
        return values.pick(values.STRINGS)
    if op in ('array', 'bool_array'):
        if field['count'] > MAX_LITERAL_ARRAY:
            fill = False if op == 'bool_array' else 0
            return [fill] * field['count']
        return [build_elem(field, types, values, depth) for _ in range(field['count'])]
    if op in ('varray', 'bool_varray', 'string_varray'):
        return [build_elem(field, types, values, depth) for _ in range(2)]
    if op == 'object':
        return build_type(field['type'], types, values, depth + 1)
    if op == 'object_array':
        return [build_type(field['type'], types, values, depth + 1) for _ in range(field['count'])]
    if op == 'object_varray':
        return [build_type(field['type'], types, values, depth + 1) for _ in range(2)]
    raise ValueError(op)


def build_elem(field, types, values, depth):
    op = field['op']
    if op in ('array', 'varray'):
        if field['code'] in values.FLOATS:
            return values.pick(values.FLOATS[field['code']])
        return values.pick(values.INTS[field['code']])
    if op in ('bool_array', 'bool_varray'):
        return values.pick([True, False])
    if op == 'string_varray':
        return values.pick(values.STRINGS)
    if op in ('object_array', 'object_varray'):
        return build_type(field['type'], types, values, depth + 1)
    raise ValueError(op)


def build_payload(payload, types, values, depth):
    if payload['op'] == 'object':
        return build_type(payload['type'], types, values, depth + 1)
    if payload['op'] == 'scalar':
        if payload['code'] in values.FLOATS:
            return values.pick(values.FLOATS[payload['code']])
        return values.pick(values.INTS[payload['code']])
    if payload['op'] == 'string':
        return values.pick(values.STRINGS)
    raise ValueError(payload['op'])


def build_type(fqn, types, values, depth):
    if depth > 8:
        raise ValueError('value nesting too deep at %s' % fqn)
    t = types[fqn]
    if t['kind'] == 'union':
        alt = t['alternatives'][0]
        return ('union', fqn, alt, build_payload(alt['payload'], types, values, depth))
    return ('record', fqn, {f['name']: build_field(f, types, values, depth) for f in t['fields']})


def py_build(tree, resolve):
    kind = tree[0]
    if kind == 'union':
        _, fqn, alt, inner = tree
        value = resolve(fqn)()
        setattr(value, alt['name'], py_to_python(inner))
        return value
    _, fqn, fields = tree
    return resolve(fqn)(**{name: py_to_python(sub) for name, sub in fields.items()})


def py_to_python(value):
    if isinstance(value, list):
        return [py_to_python(v) for v in value]
    if isinstance(value, tuple):
        return py_build(value, resolve_global)
    return value


_ROOTS = []


def resolve_global(fqn):
    """Resolve a CLAD name through the union modules' own merged namespaces.

    Both union modules import the generated `clad.*` modules, so resolving here
    keeps class identity identical to the classes the union validates against.
    """
    if not _ROOTS:
        from cozmoclad.clad.externalInterface.messageGameToEngine import Anki as g2e
        from cozmoclad.clad.externalInterface.messageEngineToGame import Anki as e2g
        _ROOTS.extend([g2e, e2g])
    for root in _ROOTS:
        obj = root
        for part in fqn.split('.')[1:]:
            try:
                obj = getattr(obj, part)
            except AttributeError:
                obj = None
                break
        if obj is not None:
            return obj
    raise KeyError('cannot resolve CLAD type %s' % fqn)


def cs_expr(tree):
    kind = tree[0]
    if kind == 'union':
        _, fqn, alt, inner = tree
        return 'new global::%s { %s = %s }' % (fqn, alt['name'], cs_payload(alt['payload'], inner))
    _, fqn, fields = tree
    args = ', '.join(cs_value(t, v) for t, v in ((field_types[fqn][name], v) for name, v in fields.items()))
    return 'new global::%s(%s)' % (fqn, args)


def cs_payload(payload, value):
    if payload['op'] == 'object':
        return cs_expr(value)
    if payload['op'] == 'scalar':
        return cs_scalar(payload['code'], value)
    if payload['op'] == 'string':
        return '"%s"' % value
    raise ValueError(payload['op'])


def cs_scalar(code, value):
    if code == 'f':
        return repr(value) + 'f'
    if code == 'd':
        return repr(value) + 'd'
    if code == 'q':
        return str(value) + 'L'
    if code == 'Q':
        return str(value) + 'UL'
    if code == 'I':
        return str(value) + 'u'
    return str(value)


def cs_value(field, value):
    op = field['op']
    if op == 'scalar':
        return cs_scalar(field['code'], value)
    if op == 'bool':
        return 'true' if value else 'false'
    if op == 'string':
        return '"%s"' % value
    if op in ('array', 'varray'):
        if value and all(v == 0 for v in value):
            return 'new %s[%d]' % (field_cs_scalar(field), len(value))
        return 'new %s[] { %s }' % (field_cs_scalar(field), ', '.join(cs_scalar(field['code'], v) for v in value))
    if op in ('bool_array', 'bool_varray'):
        return 'new bool[] { %s }' % ', '.join('true' if v else 'false' for v in value)
    if op == 'string_varray':
        return 'new string[] { %s }' % ', '.join('"%s"' % v for v in value)
    if op == 'object':
        return cs_expr(value)
    if op in ('object_array', 'object_varray'):
        return 'new global::%s[] { %s }' % (field['type'], ', '.join(cs_expr(v) for v in value))
    raise ValueError(op)


def field_cs_scalar(field):
    mapping = {'b': 'sbyte', 'B': 'byte', 'h': 'short', 'H': 'ushort', 'i': 'int',
               'I': 'uint', 'q': 'long', 'Q': 'ulong', 'f': 'float', 'd': 'double'}
    return mapping[field['code']]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--schema', required=True)
    ap.add_argument('--cozmoclad', required=True, help='extracted cozmoclad package dir')
    ap.add_argument('--out', required=True)
    args = ap.parse_args()

    schema = json.load(open(args.schema, encoding='utf-8'))
    global types, field_types
    types = schema['types']
    field_types = {}
    for fqn, t in types.items():
        if t['kind'] in ('struct', 'message'):
            field_types[fqn] = {f['name']: f for f in t['fields']}

    # Accept either an extracted package directory or the cozmoclad wheel.
    root = args.cozmoclad
    if os.path.isfile(root):
        import tempfile
        import zipfile
        extract = tempfile.mkdtemp(prefix='cladgen-')
        with zipfile.ZipFile(root) as archive:
            archive.extractall(extract)
        root = extract
    sys.path.insert(0, os.path.join(root, 'cozmoclad'))
    sys.path.insert(0, root)


    lines = ['''// Copyright (c) 2016-2017 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License in the file LICENSE.txt or at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.''',
             '',
             '// <auto-generated>',
             '// Generated by tools/cladgen/emit_oracle_tests.py from cozmoclad %s.' % schema['cozmoclad']['version'],
             '// Each case builds the same value the Python SDK builds, then compares the',
             '// packed bytes with what cozmoclad produced.',
             '// </auto-generated>',
             '',
             'using Xunit;',
             '',
             'namespace Cozmo.Tests;',
             '',
             'public static class CladOracleTests',
             '{']
    count = 0
    for union_fqn in ('Anki.Cozmo.ExternalInterface.MessageGameToEngine',
                      'Anki.Cozmo.ExternalInterface.MessageEngineToGame'):
        union = types[union_fqn]
        is_game_to_engine = union_fqn.endswith('MessageGameToEngine')
        union_short = union_fqn.rsplit('.', 1)[1]
        for alt in union['alternatives']:
            if alt['payload']['op'] != 'object':
                continue
            round_type = alt['payload']['type']
            values = Values()
            tree = build_type(round_type, types, values, 0)
            python_value = py_build(tree, resolve_global)
            body = python_value.pack()
            union_value = resolve_global(union_fqn)()
            setattr(union_value, alt['name'], python_value)
            union_bytes = union_value.pack()
            method = 'Oracle_%s_%s' % (union_short, alt['name'])
            lines.append('    [Fact]')
            lines.append('    public static void %s()' % method)
            lines.append('    {')
            lines.append('        var value = %s;' % cs_expr(tree))
            lines.append('        var bytes = CladTestBytes.Pack(value.Pack);')
            lines.append('        Assert.Equal("%s", CladTestBytes.Hex(bytes));' % body.hex())
            lines.append('        var round = global::%s.UnpackFrom(CladTestBytes.Reader(bytes));' % round_type)
            lines.append('        Assert.Equal(CladTestBytes.Hex(bytes), CladTestBytes.Hex(CladTestBytes.Pack(round.Pack)));')
            lines.append('        var unionValue = new global::%s { %s = value };' % (union_fqn, alt['name']))
            lines.append('        Assert.Equal("%s", CladTestBytes.Hex(CladTestBytes.Pack(unionValue.Pack)));' % union_bytes.hex())
            lines.append('    }')
            lines.append('')
            count += 1
    lines.append('}')
    with open(args.out, 'w', encoding='utf-8') as fh:
        fh.write('\n'.join(lines) + '\n')
    print('emitted %d oracle cases to %s' % (count, args.out))


if __name__ == '__main__':
    main()
