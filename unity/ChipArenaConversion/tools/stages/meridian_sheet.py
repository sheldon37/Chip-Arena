"""Meridian's dressing sheet (the MERIDIAN art bible 11.2): what each of the five candidate-3 street plans fixes for the dress
that the walk's Core geometry does not carry, written as Core/NativeMeridianDressSheet.cs for Core NativeMeridianDress.

From docs/expedition/meridian-plans/<layout>.json (the same specs tools/bake_meridian_walks.py builds the walk from):
- each lot's style (rows, shops, tenements, warehouses, villas, civic, market, sheds) and roof (walkable or pitched), by block id;
- the plan's lamp points (about one every 12 m along each route, alternating sides: art data);
- the five layouts' street names (one per route), fixed for the layout and never mirrored.

Presentation only: nothing here is a collider, a rule or a spawn. Run after a spec changes (CoreTests MeridianDressChecks
fails while a walk lot has no sheet entry):

    python tools/stages/meridian_sheet.py
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..'))
SPECS = os.path.join(ROOT, 'docs', 'expedition', 'meridian-plans')
OUT = os.path.join(ROOT, 'unity', 'ChipArenaConversion', 'Assets', 'ChipArena', 'Core', 'NativeMeridianDressSheet.cs')
PLANS = [('crown-hill', 647), ('navigation', 784), ('arcades', 1058), ('stride', 1332), ('yards', 1606)]
STYLE = {'rows': 'r', 'shops': 's', 'tenements': 't', 'warehouses': 'w', 'villas': 'v', 'civic': 'c', 'market': 'm', 'sheds': 'h'}
# One street name per route (north, east, south, west), cream on green enamel plates (bible 6.7); fixed per layout.
STREETS = {
    647: ('HUNDRED STEPS', 'BELVEDERE RISE', 'QUAY LANE', 'CUTTING ROAD'),
    784: ('LOCK STREET', 'TOWPATH ROW', 'EXCHANGE WALK', 'WHARF HILL'),
    1058: ('GALLERIA ROW', 'SHAMBLES LANE', 'BONDED WAY', 'THE LEADS'),
    1332: ('ARCH STREET', 'STRIDE TERRACE', 'GOODS CUT', 'ALBION ROW'),
    1606: ('SIGNAL HILL', 'ENGINE LANE', 'MAIN LINE ROAD', 'BONDED ROW'),
}


def num(v):
    s = ('%.3f' % v).rstrip('0').rstrip('.')
    return (s if s not in ('-0', '') else '0') + 'f'


def main():
    cases_lots, cases_lamps, cases_streets = [], [], []
    for name, seed in PLANS:
        spec = json.load(open(os.path.join(SPECS, name + '.json'), encoding='utf-8'))
        lots = []
        for b in spec['blocks']:
            if b['role'] != 'lot':
                continue
            lots.append('%s=%s%s' % (b['id'], STYLE[b['style']], 'w' if b.get('roof') == 'walkable' else 'p'))
        cases_lots.append('                case %d: return "%s";' % (seed, ';'.join(lots)))
        lamps = [num(c) for p in spec['lamps'] for c in p]
        cases_lamps.append('                case %d: return new[]{%s};' % (seed, ','.join(lamps)))
        cases_streets.append('                case %d: return new[]{%s};' % (seed, ','.join('"%s"' % s for s in STREETS[seed])))
    text = '\n'.join([
        '// Generated from docs/expedition/meridian-plans by tools/stages/meridian_sheet.py. Never hand-edit.',
        'namespace ChipArena.UnityConversion',
        '{',
        '    public static partial class NativeMeridianDress',
        '    {',
        '        /// <summary>The layout\'s lots: "id=StyleRoof;..." (style r rows, s shops, t tenements, w warehouses, v villas, c civic,',
        '        /// m market, h sheds; roof w walkable, p pitched). Null for a layout without a sheet.</summary>',
        '        public static string SheetLots(uint layout)',
        '        {',
        '            switch(layout)',
        '            {',
        *cases_lots,
        '                default: return null;',
        '            }',
        '        }',
        '        /// <summary>The plan\'s lamp points (x, y, z: the route\'s edge at its floor), about one every 12 m along each route.</summary>',
        '        public static float[] SheetLamps(uint layout)',
        '        {',
        '            switch(layout)',
        '            {',
        *cases_lamps,
        '                default: return null;',
        '            }',
        '        }',
        '        /// <summary>The street names on the plates, one per route (north, east, south, west).</summary>',
        '        public static string[] SheetStreets(uint layout)',
        '        {',
        '            switch(layout)',
        '            {',
        *cases_streets,
        '                default: return null;',
        '            }',
        '        }',
        '    }',
        '}',
        ''])
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('wrote', OUT, len(text), 'bytes')


if __name__ == '__main__':
    main()
