"""Substation 128's art (docs/arenas/CLUB.md, docs/arenas/STAGE-ART.md): the nightclub in the old power substation modelled in
Blender on the map's own blocks and its Core kit's parts, into the stage bank Resources/ChipArenaStageClub (stagekit.py's format
and bake). Runtime NativeStageArt draws it in place of the boxes; without the bank the box kit draws as before.

usage (from unity/ChipArenaConversion):
  dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout club LAYOUT.json
  blender --background --factory-startup --python tools/stages/club_art.py -- --layout LAYOUT.json --out ASSETS/LocalAssets
          [--preview DIR] [--tiers 0,1,2] [--no-ao] [--static-only]

This file: the hall's structure and furniture on their blocks: the mezzanine, VIP and catwalk decks on riveted joists, the
glass rails, the crowd barriers, the stairs, the cast-iron columns, the truss towers and the lighting truss (the kit's own
members as round tubes), the video cube's frame, the stage, the DJ booth with its decks and mixer, the speaker stacks (baffles
with their wells, horns), the bar (reeded walnut, a black top, the brass rail), the back bar's bottles, the pillar, the
podiums, the velvet sofas and booths, the planters. club_hall.py: the shell, the hall above the play space, the galleries and
the pit. club_props.py: the breakable props and the rig (the cones, the moving heads, the laser posts, the mirror balls, the
Grey DJ and the crowd of Greys). Every other kit part is rebuilt from its own box as a finished piece (club_kit.kit_part).
"""
import argparse
import math
import os
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stagekit as sk  # noqa: E402
from stagekit import Geo, box, sweep, lathe, grid_face  # noqa: E402
import club_kit as ck  # noqa: E402
from club_kit import F, ext, rod, rbox, rivets, ibeam, glow, kit_part, dome, ball  # noqa: E402
import club_hall as hall  # noqa: E402

BANK = 'ChipArenaStageClub'


# ---------------------------------------------------------------------------------------------------------------------------
# Structure
# ---------------------------------------------------------------------------------------------------------------------------
def deck(g, b, T):
    """A steel deck: boards on top, a channel fascia round it, a dark soffit on riveted joists."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    i = b['id']
    along_x = (x1 - x0) > (z1 - z0)
    top = F['steeldark'] if (i == 'club-catwalk' or i.endswith('-roof')) else F['wooddark' + ('x' if along_x else 'z')]
    grid_face(g, (x0, y1, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, top, ck.Y, T.cell * 1.5)
    grid_face(g, (x0, y0, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), 0, 0, F['slab'], (0, -1, 0), T.cell * 3)
    box(g, x0, x1, y0, y1, z0, z1, F['steel'], T, bevel=.015, cell=4, skip=('+y', '-y'))
    if not T.detail or i.endswith('-roof'):
        return
    # joists across the short span every 2.4 m, a lip under the fascia
    if along_x:
        n = max(1, int((x1 - x0) / 2.4))
        for k in range(n + 1):
            x = x0 + .2 + (x1 - x0 - .4) * k / n
            ibeam(g, [(x, y0 - .11, z0 + .05), (x, y0 - .11, z1 - .05)], .22, .14, F['steeldark'], up=ck.Y)
    else:
        n = max(1, int((z1 - z0) / 2.4))
        for k in range(n + 1):
            z = z0 + .2 + (z1 - z0 - .4) * k / n
            ibeam(g, [(x0 + .05, y0 - .11, z), (x1 - .05, y0 - .11, z)], .22, .14, F['steeldark'], up=ck.Y)


def rail(g, b, T):
    """A rail: chrome posts, a smoked-glass pane (it is cover: it reads solid), a round handrail."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    along_x = (x1 - x0) > (z1 - z0)
    L = (x1 - x0) if along_x else (z1 - z0)
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    n = max(1, int(round(L / 1.6)))
    sides = (6, 4, 3)[T.i]

    def P(u, y):
        return (x0 + u, y, cz) if along_x else (cx, y, z0 + u)
    for k in range(n + 1):
        u = min(L - .03, max(.03, L * k / n))
        rod(g, P(u, y0), P(u, y1 + .02), .026, F['chrome'], sides, caps=False)
    rod(g, P(0, y1 + .03), P(L, y1 + .03), .036, F['chrome'], sides, caps=T.i == 0)
    if along_x:
        box(g, x0 + .02, x1 - .02, y0 + .06, y1 - .05, cz - .012, cz + .012, F['glass'], None, 0, cell=T.cell * 2)
        box(g, x0, x1, y0, y0 + .06, z0 + .01, z1 - .01, F['gunmetal'], None, 0, cell=0)
    else:
        box(g, cx - .012, cx + .012, y0 + .06, y1 - .05, z0 + .02, z1 - .02, F['glass'], None, 0, cell=T.cell * 2)
        box(g, x0 + .01, x1 - .01, y0, y0 + .06, z0, z1, F['gunmetal'], None, 0, cell=0)


def barrier(g, b, T):
    """A stage barricade: a ribbed aluminium front and back, a step plate, a round top rail."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0 + .05, y1 - .04, z0 + .04, z1 - .04, F['alu'], T, bevel=.015, cell=2)
    box(g, x0, x1, y0, y0 + .05, z0, z1, F['steeldark'], T, bevel=.01, cell=0)
    rod(g, (x0, y1 - .04, (z0 + z1) / 2), (x1, y1 - .04, (z0 + z1) / 2), .045, F['alu'], T.small if T.i < 2 else 4)
    if T.detail:
        n = int((x1 - x0) / .62)
        for k in range(n + 1):
            x = x0 + .05 + (x1 - x0 - .1) * k / n
            for (za, zb) in ((z0 + .01, z0 + .04), (z1 - .04, z1 - .01)):
                box(g, x - .03, x + .03, y0 + .05, y1 - .08, za, zb, F['alu'], None, 0, cell=0)


def step(g, b, T, lead):
    """One step of a flight: a dark riser, a ribbed tread with a round chrome nosing on its leading edge."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1 - .03, z0, z1, F['steeldark'], None, 0, cell=0, skip=('-y', '+y'))
    box(g, x0, x1, y1 - .03, y1, z0, z1, F['gunmetal'], T, bevel=.008, cell=0, skip=('-y',))
    if T.detail:
        if lead in ('-z', '+z'):
            z = z0 if lead == '-z' else z1
            rod(g, (x0 + .01, y1 - .012, z), (x1 - .01, y1 - .012, z), .014, F['chrome'], 4, caps=False)
        else:
            x = x0 if lead == '-x' else x1
            rod(g, (x, y1 - .012, z0 + .01), (x, y1 - .012, z1 - .01), .014, F['chrome'], 4, caps=False)


def column(g, b, T):
    """A cast-iron column: a square plinth, a moulded base, a shaft with a slight swell, collars, a flared capital with a
    square abacus; the tall ones (to the roof) carry brackets."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    cx, cz, w = (x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2
    r = w * .62
    tall = y1 - y0
    box(g, x0 - .02, x1 + .02, y0, y0 + .3, z0 - .02, z1 + .02, F['steel'], T, bevel=.03, cell=0, skip=('-y',))
    prof = [(w * 1.0, .3), (w * 1.0, .36), (r * 1.25, .42), (r * 1.3, .47), (r * 1.12, .53), (r * 1.02, .6), (r, .7)]
    n = 6 if T.i == 0 else 3
    for k in range(1, n + 1):
        t = k / n
        prof.append((r * (1 + .05 * math.sin(math.pi * t)) * (1 - .08 * t), .7 + (tall - 1.5) * t))
    top = tall - .8
    prof += [(r * .95, top + .05), (r * 1.08, top + .1), (r * .95, top + .16), (r * .98, top + .3), (r * 1.25, top + .5), (w * 1.02, top + .62)]
    lathe(g, prof, (cx, y0, cz), F['steel'], T.round if T.i < 2 else 6)
    box(g, x0 - .03, x1 + .03, y1 - .18, y1, z0 - .03, z1 + .03, F['steel'], T, bevel=.025, cell=0, skip=('+y',))
    lathe(g, [(r * 1.34, .465), (r * 1.4, .49), (r * 1.34, .515)], (cx, y0, cz), F['brass'], T.round if T.i < 2 else 6)
    if T.detail:
        for yy in ([3.0, 6.0, 9.0] if tall > 8 else [2.0]):
            lathe(g, [(r * 1.0, yy - .05), (r * 1.1, yy - .03), (r * 1.1, yy + .03), (r * 1.0, yy + .05)], (cx, y0, cz), F['steel'], T.round)
            if T.tiny:
                for k in range(8):
                    a = k * math.pi / 4
                    rivets(g, [(cx + math.cos(a) * r * 1.1, y0 + yy, cz + math.sin(a) * r * 1.1)], (math.cos(a), 0, math.sin(a)), .02, F['steel'])


def tower(g, b, T):
    """A truss tower (solid in the simulation): a black scrim sock inside the lattice, so it reads as the cover it is."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0 + .1, x1 - .1, y0 + .05, y1 - .45, z0 + .1, z1 - .1, F['rubber'], T, bevel=.03, cell=3)


def cube(g, b, L, T):
    """The video cube: its dark body behind the rig's panels and the mullions between them."""
    C = L['club']
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, F['screenoff'], None, 0, cell=0)
    if T.i == 2:
        return
    W, Hn = int(C['cubeW']), int(C['cubeH'])
    h, hy, cy = C['cubeHalf'], C['cubeHalfY'], C['cubeY']
    o = h + .024
    for c in range(1, W):
        u = -h + c * 2 * h / W
        for s in (-1, 1):
            box(g, s * o - .004, s * o + .004, cy - hy, cy + hy, u - .03, u + .03, F['black'], None, 0, cell=0)
            box(g, u - .03, u + .03, cy - hy, cy + hy, s * o - .004, s * o + .004, F['black'], None, 0, cell=0)
        box(g, u - .03, u + .03, cy - hy - .028, cy - hy - .02, -h, h, F['black'], None, 0, cell=0)
        box(g, -h, h, cy - hy - .028, cy - hy - .02, u - .03, u + .03, F['black'], None, 0, cell=0)
    for r in range(1, Hn):
        y = cy - hy + r * 2 * hy / Hn
        for s in (-1, 1):
            box(g, s * o - .004, s * o + .004, y - .03, y + .03, -h, h, F['black'], None, 0, cell=0)
            box(g, -h, h, y - .03, y + .03, s * o - .004, s * o + .004, F['black'], None, 0, cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# The stage end
# ---------------------------------------------------------------------------------------------------------------------------
def stage(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, F['black'], T, bevel=.02, cell=T.cell * 2, skip=('-y',))
    if T.detail:
        # the front's panels in their frames
        n = 16
        for k in range(n):
            xa, xb = x0 + (x1 - x0) * k / n + .06, x0 + (x1 - x0) * (k + 1) / n - .06
            box(g, xa, xb, y0 + .2, y1 - .16, z0 - .016, z0 + .0, F['grille'], T, bevel=.012, cell=0, skip=('+z',))


def footlight(g, c, T):
    """A floor can on the stage's lip, tipped back at the LED wall."""
    rbox(g, c[0] - .13, c[0] + .13, c[1] - .05, c[1] + .06, c[2] - .13, c[2] + .13, .03, F['black'], 1)
    if T.detail:
        lathe(g, [(.085, 0.0), (.085, .03)], (c[0], c[1] + .055, c[2]), F['gunmetal'], T.small, cap0=False)
        lathe(g, [(.07, .032), (1e-4, .032)], (c[0], c[1] + .055, c[2]), glow(0xFF9AE8, 1.3), T.small)


def speaker(g, b, L, T):
    """A stack of four cabinets: rounded shells with a seam between, the three woofer baffles with their wells and bezels
    (the rig's cones sit in them), the horn cabinet's two flared mouths."""
    C = L['club']
    x0, x1, y0, y1, z0, z1 = ext(b)
    cab = (y1 - y0) / 4
    cx = (x0 + x1) / 2
    blk, well = F['black'], F['cone']
    n = T.round + 2 if T.i < 2 else 8
    for k in range(4):
        ya, yb = y0 + k * cab + .012, y0 + (k + 1) * cab - .012
        # the shell: sides, top, bottom and back; the front is the baffle
        box(g, x0, x1, ya, yb, z0 + .03, z1, blk, T, bevel=.03, cell=T.cell * 2, skip=('-z',))
        for (xa, xb) in ((x0, x0 + .05), (x1 - .05, x1)):
            box(g, xa, xb, ya, yb, z0, z0 + .03, blk, None, 0, cell=0, skip=('+z',))
        for (yc, yd) in ((ya, ya + .05), (yb - .05, yb)):
            box(g, x0 + .05, x1 - .05, yc, yd, z0, z0 + .03, blk, None, 0, cell=0, skip=('+z',))
        zb = z0 + .022                                        # the baffle's face
        yc = (ya + yb) / 2
        if k < 3:
            R = .42
            for dx in (-.66, .66):
                ox = cx + dx
                xa, xb = (x0 + .05, cx) if dx < 0 else (cx, x1 - .05)
                # the baffle round the hole: a fan from the hole's rim to the patch's edge
                rim = [(ox + R * math.cos(2 * math.pi * i / n), yc + R * math.sin(2 * math.pi * i / n)) for i in range(n)]

                def edge(p):
                    dx_, dy_ = p[0] - ox, p[1] - yc
                    s = min((xb - ox) / dx_ if dx_ > 1e-9 else ((xa - ox) / dx_ if dx_ < -1e-9 else 1e9), (yb - .05 - yc) / dy_ if dy_ > 1e-9 else ((ya + .05 - yc) / dy_ if dy_ < -1e-9 else 1e9))
                    return (ox + dx_ * s, yc + dy_ * s)
                out = [edge(p) for p in rim]
                for i in range(n):
                    j = (i + 1) % n
                    g.face(((rim[i][0], rim[i][1], zb), (rim[j][0], rim[j][1], zb), (out[j][0], out[j][1], zb), (out[i][0], out[i][1], zb)), F['grille'], (0, 0, -1))
                    # square the patch's corners
                    if abs(out[i][0] - out[j][0]) > 1e-6 and abs(out[i][1] - out[j][1]) > 1e-6:
                        corner = (xa if min(out[i][0], out[j][0]) < ox else xb, (ya + .05) if min(out[i][1], out[j][1]) < yc else (yb - .05))
                        g.face(((out[i][0], out[i][1], zb), (out[j][0], out[j][1], zb), (corner[0], corner[1], zb)), F['grille'], (0, 0, -1))
                # the well behind the hole and the bezel round it
                lathe(g, [(1e-4, .24), (R, .24), (R, 0.0)], (ox, yc, zb), well, n, axis=(0, 0, 1))
                lathe(g, [(R + .0, -.0), (R + .012, -.022), (R + .05, -.03), (R + .07, -.012), (R + .075, .0)], (ox, yc, zb), F['gunmetal'], n, axis=(0, 0, 1))
                if T.tiny:
                    rivets(g, [(ox + (R + .04) * math.cos(a), yc + (R + .04) * math.sin(a), zb - .028) for a in [math.pi / 8 + i * math.pi / 4 for i in range(8)]], (0, 0, -1), .014, F['chrome'])
        else:
            grid_face(g, (x0 + .05, ya + .05, zb), (x1 - x0 - .1, 0, 0), (0, yb - ya - .1, 0), 0, 0, F['grille'], (0, 0, -1), 99)
            for dx in (-.62, .62):
                ox = cx + dx
                mouth = [(ox - .42, yc - .28), (ox + .42, yc - .28), (ox + .42, yc + .28), (ox - .42, yc + .28)]
                throat = [(ox - .07, yc - .1), (ox + .07, yc - .1), (ox + .07, yc + .1), (ox - .07, yc + .1)]
                zm, zt = zb - .012, zb + .2
                for i in range(4):
                    j = (i + 1) % 4
                    mid = [(mouth[i][0] * .45 + throat[i][0] * .55, mouth[i][1] * .45 + throat[i][1] * .55), (mouth[j][0] * .45 + throat[j][0] * .55, mouth[j][1] * .45 + throat[j][1] * .55)]
                    zc = zb + .06
                    g.face(((mouth[i][0], mouth[i][1], zm), (mouth[j][0], mouth[j][1], zm), (mid[1][0], mid[1][1], zc), (mid[0][0], mid[0][1], zc)), F['gunmetal'], (ox - (mouth[i][0] + mouth[j][0]) / 2, yc - (mouth[i][1] + mouth[j][1]) / 2, -.6))
                    g.face(((mid[0][0], mid[0][1], zc), (mid[1][0], mid[1][1], zc), (throat[j][0], throat[j][1], zt), (throat[i][0], throat[i][1], zt)), well, (ox - (mouth[i][0] + mouth[j][0]) / 2, yc - (mouth[i][1] + mouth[j][1]) / 2, -.2))
                g.face(tuple((p[0], p[1], zt) for p in throat), well, (0, 0, -1))
                # the mouth's lip
                for (xa, xb, yc0, yc1) in ((ox - .46, ox + .46, yc - .32, yc - .28), (ox - .46, ox + .46, yc + .28, yc + .32), (ox - .46, ox - .42, yc - .28, yc + .28), (ox + .42, ox + .46, yc - .28, yc + .28)):
                    box(g, xa, xb, yc0, yc1, zb - .03, zb, F['gunmetal'], None, 0, cell=0, skip=('+z',))
        # recessed bar handles on the sides
        if T.detail:
            for (xs, s) in ((x0, -1), (x1, 1)):
                for zz in (z0 + 1.0, z1 - 1.0):
                    rod(g, (xs + s * .012, yc - .12, zz), (xs + s * .012, yc + .12, zz), .016, F['gunmetal'], 4)


def dj_booth(g, b, L, T):
    """The DJ's console: a black body with chrome corner posts, two decks with their platters and tone arms, the mixer with
    its knobs, a laptop; the kit's screen and LEDs glow on it."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    cz = (z0 + z1) / 2
    box(g, x0, x1, y0, y1, z0, z1, F['black'], T, bevel=.03, cell=T.cell * 2, skip=('-y',))
    if T.detail:
        for x in (x0 + .03, x1 - .03):
            rod(g, (x, y0, z0 - .0), (x, y1, z0 - .0), .03, F['chrome'], T.small, caps=False)
        box(g, x0 + .06, x1 - .06, y0 + .06, y0 + .1, z0 - .014, z0, F['gunmetal'], None, 0, cell=0)
    for dx in (-1.55, 1.55):
        rbox(g, dx - .3, dx + .3, y1, y1 + .06, z0 + .25, z1 - .25, .02, F['gunmetal'], 1)
        n = T.round if T.i < 2 else 8
        lathe(g, [(.215, .06), (.215, .078), (.2, .084)], (dx, y1, cz - .04), F['chrome'], n)
        lathe(g, [(.2, .084), (.07, .086), (.065, .09), (1e-4, .09)], (dx, y1, cz - .04), F['cone'], n)
        lathe(g, [(.12, .0875), (.1, .0875)], (dx, y1, cz - .04), glow(ck.CYAN, 2.0), n)
        if T.detail:
            rod(g, (dx + .24, y1 + .06, cz + .2), (dx + .24, y1 + .1, cz + .2), .02, F['chrome'], T.small)
            rod(g, (dx + .24, y1 + .1, cz + .2), (dx + .13, y1 + .09, cz - .0), .008, F['chrome'], 4)
            for k in range(3):
                box(g, dx - .26 + k * .07, dx - .21 + k * .07, y1 + .06, y1 + .068, z1 - .31, z1 - .28, glow((ck.CYAN, ck.MAGENTA, 0xFFFFFF)[k], 1.6), None, 0, cell=0)
    rbox(g, -.32, .32, y1, y1 + .08, z0 + .3, z1 - .3, .02, F['gunmetal'], 1)
    if T.detail:
        for i in range(4):
            for j in range(3):
                rod(g, (-.24 + i * .16, y1 + .08, cz + .12 + j * .07), (-.24 + i * .16, y1 + .1, cz + .12 + j * .07), .016, F['chrome'] if (i + j) % 2 else F['black'], 5)
    # the laptop: its base and the lid behind the kit's glowing screen
    box(g, .55, 1.0, y1, y1 + .02, z1 - .45, z1 - .15, F['alu'], T, bevel=.006, cell=0)
    box(g, .55, 1.0, y1 + .02, y1 + .245, z1 - .149, z1 - .138, F['alu'], T, bevel=.004, cell=0)


# ---------------------------------------------------------------------------------------------------------------------------
# The bar and the furniture
# ---------------------------------------------------------------------------------------------------------------------------
def counter(g, b, T, fronts):
    """A counter: a black plinth, a walnut body reeded on its public faces between pilasters, a thick black top that oversails."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0 + .04, x1 - .04, y0, y0 + .12, z0 + .04, z1 - .04, F['black'], None, 0, cell=0, skip=('-y', '+y'))
    box(g, x0, x1, y0 + .12, y1 - .07, z0, z1, F['wooddarky'], T, bevel=.012, cell=T.cell * 2, skip=('-y', '+y'))
    rbox(g, x0 - .04, x1 + .04, y1 - .07, y1, z0 - .04, z1 + .04, .028, F['blackgloss'], 1 if T.i else 2)
    if T.i == 2:
        return
    step_ = .13 if T.i == 0 else .39
    sides = 5 if T.i == 0 else 3
    for side in fronts:
        if side in ('+z', '-z'):
            z = z1 if side == '+z' else z0
            n = max(1, int(round((x1 - x0) / 1.6)))
            for k in range(n + 1):
                x = x0 + (x1 - x0) * k / n
                xa, xb = max(x0, x - .07), min(x1, x + .07)
                box(g, xa, xb, y0 + .12, y1 - .07, z - .03, z + .03, F['woody'], T, bevel=.012, cell=0)
            u = x0 + .1
            while u < x1 - .08:
                if min(abs(u - (x0 + (x1 - x0) * k / n)) for k in range(n + 1)) > .1:
                    rod(g, (u, y0 + .2, z), (u, y1 - .15, z), .034, F['woody'], sides, caps=False)
                u += step_
            for yy in (y0 + .16, y1 - .12):
                box(g, x0, x1, yy - .035, yy + .035, z - .022, z + .022, F['woody'], None, 0, cell=0)
        else:
            x = x1 if side == '+x' else x0
            n = max(1, int(round((z1 - z0) / 1.6)))
            for k in range(n + 1):
                z = z0 + (z1 - z0) * k / n
                za, zb = max(z0, z - .07), min(z1, z + .07)
                box(g, x - .03, x + .03, y0 + .12, y1 - .07, za, zb, F['woody'], T, bevel=.012, cell=0)
            u = z0 + .1
            while u < z1 - .08:
                if min(abs(u - (z0 + (z1 - z0) * k / n)) for k in range(n + 1)) > .1:
                    rod(g, (x, y0 + .2, u), (x, y1 - .15, u), .034, F['woody'], sides, caps=False)
                u += step_
            for yy in (y0 + .16, y1 - .12):
                box(g, x - .022, x + .022, yy - .035, yy + .035, z0, z1, F['woody'], None, 0, cell=0)


def backbar(g, b, T):
    """The back bar: a dark carcass with panelled base cupboards, pilasters and a cornice round the kit's mirror and shelves."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1 - .02, F['wooddarky'], T, bevel=.012, cell=T.cell * 2, skip=('-y',))
    box(g, x0, x1, y0, .9, z1 - .02, z1 + .0, F['wooddarkx'], T, bevel=.01, cell=0)
    box(g, x0 - .0, x1 + .0, .86, .92, z1 - .02, z1 + .1, F['blackgloss'], T, bevel=.012, cell=0)
    box(g, x0, x1, 3.5, y1, z1 - .02, z1 + .06, F['wooddarkx'], T, bevel=.02, cell=0)
    for x in (x0, x1 - .1):
        box(g, x, x + .1, .92, 3.5, z1 - .02, z1 + .05, F['wooddarky'], T, bevel=.012, cell=0)
    if T.detail:
        n = 8
        for k in range(n):
            xa, xb = x0 + (x1 - x0) * k / n + .08, x0 + (x1 - x0) * (k + 1) / n - .08
            box(g, xa, xb, .14, .78, z1 - .005, z1 + .012, F['woodx'], T, bevel=.01, cell=0, skip=('-z',))
            ball(g, ((xa + xb) / 2 + (.3 if k % 2 == 0 else -.3), .5, z1 + .02), .018, F['brass'], 5, 3)


def bottle(g, p, f, T):
    c, h = p['c'], p['h']
    y0, tall, r = c[1] - h[1], 2 * h[1], h[0] * .95
    prof = [(r, 0.0), (r, tall * .56), (r * .8, tall * .68), (r * .34, tall * .8), (r * .34, tall)]
    if T.i == 2:
        lathe(g, [prof[0], prof[1], prof[4]], (c[0], y0, c[2]), f, 3)
        return
    lathe(g, prof if T.i == 0 else [prof[0], prof[1], prof[3], prof[4]], (c[0], y0, c[2]), f, 5 if T.i == 0 else 4, cap1=T.i == 0)


def pillar(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1, z0, z1, F['brick'], T, bevel=.015, skip=('-y', '+y'))
    box(g, x0 - .04, x1 + .04, y0, y0 + .3, z0 - .04, z1 + .04, F['stone'], T, bevel=.03, cell=0, skip=('-y',))
    box(g, x0 - .04, x1 + .04, y1 - .28, y1 - .02, z0 - .04, z1 + .04, F['stone'], T, bevel=.03, cell=0)
    if T.detail:
        for (xa, xb, za, zb) in ((x0 + .06, x1 - .06, z0 - .02, z0), (x0 + .06, x1 - .06, z1, z1 + .02), (x0 - .02, x0, z0 + .06, z1 - .06), (x1, x1 + .02, z0 + .06, z1 - .06)):
            box(g, xa, xb, .94, 3.46, za, zb, F['steeldark'], None, 0, cell=0)


def podium(g, b, T):
    x0, x1, y0, y1, z0, z1 = ext(b)
    rbox(g, x0 + .012, x1 - .012, y0, y1 - .004, z0 + .012, z1 - .012, .1, F['blackgloss'], 1 if T.i else 2)


def facing(b):
    """The way a seat faces: away from the hall's wall at its back, else toward the middle of the hall."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    if (x1 - x0) < (z1 - z0):
        if x0 < -25.3:
            return '+x'
        if x1 > 25.3:
            return '-x'
        return '+x' if (x0 + x1) < 0 else '-x'
    if z0 < -19.3:
        return '+z'
    if z1 > 19.3:
        return '-z'
    return '+z' if (z0 + z1) < 0 else '-z'


def sofa(g, b, T, vel):
    """A velvet sofa: a plinth, seat cushions, a deep buttoned back and rolled arms; the buttons brass."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    face = facing(b)
    t = Geo()
    along = (z1 - z0) if face in ('+x', '-x') else (x1 - x0)
    depth = (x1 - x0) if face in ('+x', '-x') else (z1 - z0)
    tall = y1 - y0
    # local frame: u along the sofa (0..along), d from the back (0) to the front (depth), y up
    segs = 1
    if T.i == 2:
        box(t, 0, along, 0, tall, 0, .3, vel, None, 0, cell=0, skip=('-y',))
        box(t, 0, along, 0, tall * .52, .3, depth, vel, None, 0, cell=0, skip=('-y', '-z'))
        for (ua, ub) in ((0, .2), (along - .2, along)):
            box(t, ua, ub, tall * .52, tall * .8, .3, depth, vel, None, 0, cell=0, skip=('-y', '-z'))
    else:
        box(t, .03, along - .03, 0, .1, .03, depth - .03, F['black'], None, 0, cell=0, skip=('-y', '+y'))
    arm = .2
    if T.i < 2:
        sofa_soft(t, along, depth, tall, arm, vel, segs, T)
    m = {'+z': [1, 0, 0, x0, 0, 1, 0, y0, 0, 0, 1, z0], '-z': [-1, 0, 0, x1, 0, 1, 0, y0, 0, 0, -1, z1],
         '+x': [0, 0, 1, x0, 0, 1, 0, y0, -1, 0, 0, z1], '-x': [0, 0, -1, x1, 0, 1, 0, y0, 1, 0, 0, z0]}[face]
    g.extend(t.transformed(m))


def sofa_soft(t, along, depth, tall, arm, vel, segs, T):
    rbox(t, 0, along, .1, tall, 0, .3, .07, vel, segs)                                   # the back
    for (ua, ub) in ((0, arm), (along - arm, along)):
        rbox(t, ua, ub, .1, tall * .8, .2, depth, .08, vel, segs)                       # the arms
    n = max(1, int(round((along - 2 * arm) / .95)))
    for k in range(n):
        ua, ub = arm + (along - 2 * arm) * k / n, arm + (along - 2 * arm) * (k + 1) / n
        rbox(t, ua + .004, ub - .004, .12, tall * .52, .26, depth - .0, .07, vel, segs)   # the seat cushions
    if T.i == 0:
        rows = 2
        cols = max(2, int(along / .4))
        for r in range(rows):
            for c in range(cols):
                u = .22 + (along - .44) * (c + (.5 if r % 2 else 0)) / cols
                if u > along - .2:
                    continue
                yy = tall * .64 + (tall * .24) * r / (rows - 1)
                dome(t, (u, yy, .298), .022, F['brass'], 4, 1, axis=(0, 0, 1), squash=.6)


def booth(g, b, T):
    """A booth's partition: a dark timber frame, padded velvet panels on both faces with brass buttons, a capping rail."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    along_x = (x1 - x0) > (z1 - z0)
    box(g, x0 + .02, x1 - .02, y0, y1 - .05, z0 + .02, z1 - .02, F['wooddarky'], None, 0, cell=0, skip=('-y', '+y'))
    rbox(g, x0 - .01, x1 + .01, y1 - .06, y1, z0 - .01, z1 + .01, .02, F['woodx' if along_x else 'woodz'], 1)
    box(g, x0 - .005, x1 + .005, y0, y0 + .1, z0 - .005, z1 + .005, F['black'], None, 0, cell=0, skip=('-y',))
    L = (x1 - x0) if along_x else (z1 - z0)
    n = max(1, int(round(L / .55)))
    segs = 1
    if T.i == 2:
        box(g, x0 - .012, x1 + .012, y0 + .14, y1 - .09, z0 - .012, z1 + .012, F['velvet'], None, 0, cell=0, skip=('-y', '+y'))
        return

    def pad(xa, xb, ya, yb, za, zb):
        if T.i == 0:
            rbox(g, xa, xb, ya, yb, za, zb, .035, F['velvet'], segs)
        else:
            box(g, xa, xb, ya, yb, za, zb, F['velvet'], T, bevel=.03, cell=0)
    for k in range(n):
        a, c = L * k / n + .015, L * (k + 1) / n - .015
        if along_x:
            for (za, zb) in ((z0 - .012, z0 + .06), (z1 - .06, z1 + .012)):
                pad(x0 + a, x0 + c, y0 + .14, y1 - .09, za, zb)
        else:
            for (xa, xb) in ((x0 - .012, x0 + .06), (x1 - .06, x1 + .012)):
                pad(xa, xb, y0 + .14, y1 - .09, z0 + a, z0 + c)
        if T.i == 0:
            for yy in (y0 + .42, y0 + .76):
                mid = (a + c) / 2
                if along_x:
                    dome(g, (x0 + mid, yy, z0 - .012), .02, F['brass'], 4, 1, axis=(0, 0, -1), squash=.6)
                    dome(g, (x0 + mid, yy, z1 + .012), .02, F['brass'], 4, 1, axis=(0, 0, 1), squash=.6)
                else:
                    dome(g, (x0 - .012, yy, z0 + mid), .02, F['brass'], 4, 1, axis=(-1, 0, 0), squash=.6)
                    dome(g, (x1 + .012, yy, z0 + mid), .02, F['brass'], 4, 1, axis=(1, 0, 0), squash=.6)


def planter(g, b, T):
    """A steel planter: a rimmed trough of soil with low succulents (they stay within the block's reach)."""
    x0, x1, y0, y1, z0, z1 = ext(b)
    box(g, x0, x1, y0, y1 - .05, z0, z1, F['gunmetal'], T, bevel=.02, cell=T.cell * 2, skip=('-y', '+y'))
    box(g, x0 - .02, x1 + .02, y1 - .07, y1, z0 - .02, z1 + .02, F['steeldark'], T, bevel=.015, cell=0)
    grid_face(g, (x0 + .05, y1 - .02, z0 + .05), (x1 - x0 - .1, 0, 0), (0, 0, z1 - z0 - .1), 0, 0, F['soil'], ck.Y, 99)
    if T.i == 2:
        return
    k = 0
    for ix in range(2):
        for iz in range(4):
            k += 1
            cx = x0 + .3 + ix * (x1 - x0 - .6) + (.07 if iz % 2 else -.05)
            cz = z0 + .32 + iz * (z1 - z0 - .64) / 3
            leaves = 7 if T.i == 0 else 5
            for j in range(leaves):
                a = 2 * math.pi * j / leaves + k
                lean = .5 + .25 * ((j * 7 + k) % 3)
                tip = (cx + math.cos(a) * .2 * lean, y1 + .2 - .08 * lean, cz + math.sin(a) * .2 * lean)
                base = (cx + math.cos(a) * .03, y1 - .02, cz + math.sin(a) * .03)
                mid = (cx + math.cos(a) * .12 * lean, y1 + .11, cz + math.sin(a) * .12 * lean)
                ck.limb(g, [base, mid, tip], [.03, .034, .004], F['leaf'], 4, flat=.35, side=(-math.sin(a), 0, math.cos(a)))


# ---------------------------------------------------------------------------------------------------------------------------
def hazard_row(g, p, T):
    """A drop zone's hatching row: slanted yellow bars on black."""
    c, h = p['c'], p['h']
    x0, x1, z0, z1, y = c[0] - h[0], c[0] + h[0], c[2] - h[2], c[2] + h[2], c[1] + h[1]
    g.face(((x0, y - .001, z0), (x1, y - .001, z0), (x1, y - .001, z1), (x0, y - .001, z1)), F['black'], ck.Y)
    w = (z1 - z0)
    u = x0
    while u < x1 - .02:
        a, b = u, min(x1, u + w * .9)
        g.face(((a, y, z0), (b, y, z0), (min(x1, b + w), y, z1), (min(x1, a + w), y, z1)), F['hazard'], ck.Y)
        u += w * 1.8


SKIP = {('side-wall', 'steel'), ('side-wall', 'black'), ('pier', 'steel'), ('pier', 'brick'), ('south-wall', 'steel'), ('led-wall', 'black'),
        ('column', 'steel'), ('column', 'brass'), ('speaker', 'black'), ('bar-counter', 'black'), ('rail', 'chrome'), ('window', 'steel'),
        ('dj-booth', 'black'), ('dj-booth', 'chrome')}


STATS = {}


def tally(g, key, mark):
    """Counts the corners added since `mark` under `key` (--stats prints where the bank's vertices go)."""
    n = len(g.p) // 3
    if n > mark and COUNT:
        P = np.frombuffer(g.p, dtype=np.float32).reshape(-1, 3)[mark:n]
        N = np.frombuffer(g.n, dtype=np.float32).reshape(-1, 3)[mark:n]
        fc = np.repeat(np.frombuffer(g.fin, dtype=np.uint16)[-(len(g.size) - tally.faces):], np.frombuffer(g.size, dtype=np.uint8)[-(len(g.size) - tally.faces):]) if len(g.size) > tally.faces else np.zeros(0)
        key_ = np.concatenate([np.round(P * 2000), np.round(N * 200), fc[:, None]], axis=1)
        STATS[key] = STATS.get(key, 0) + len(np.unique(key_, axis=0))
    tally.faces = len(g.size)
    return n


tally.faces = 0
COUNT = False


def static_scene(L, T):
    g = Geo()
    tally.faces = 0
    blocks = {b['id']: b for b in L['blocks']}
    m = 0
    for (name, fn) in (('floors', lambda: hall.floors(g, L, T, blocks)), ('side-walls', lambda: hall.side_walls(g, L, T)), ('south-wall', lambda: hall.south_wall(g, L, T, blocks)),
                       ('led-wall', lambda: hall.led_wall(g, L, T, blocks)), ('upper-hall', lambda: hall.upper_hall(g, L, T)), ('galleries', lambda: hall.galleries(g, L, T)), ('pit', lambda: hall.pit(g, L, T))):
        fn()
        m = tally(g, name, m)
    for b in L['blocks']:
        i = b['id']
        m = tally(g, 'block', m)
        if b['role'] != 'static' or i.startswith('club-floor-') or i.startswith('club-wall-'):
            continue
        if '-wall-' in i:
            hall.room_wall(g, b, T)
        elif i.startswith('club-barrier-'):
            barrier(g, b, T)
        elif i.startswith('club-rail-'):
            rail(g, b, T)
        elif i.endswith('-landing') or i.startswith('club-mezz-') or i == 'club-catwalk' or i in ('club-vip-c', 'club-vip-w-0', 'club-vip-w-1', 'club-vip-e-0', 'club-vip-e-1') or i.endswith('-roof'):
            deck(g, b, T)
        elif i.startswith('club-stair-grand-') or i.startswith('club-stage-step-'):
            step(g, b, T, '-z')
        elif i.startswith('club-stair-bar-w') or i.startswith('club-stage-back-e'):
            step(g, b, T, '+x')
        elif i.startswith('club-stair-bar-e') or i.startswith('club-stage-back-w'):
            step(g, b, T, '-x')
        elif i.startswith('club-column-'):
            column(g, b, T)
        elif i.startswith('club-tower-'):
            tower(g, b, T)
        elif i == 'club-cube':
            cube(g, b, L, T)
        elif i == 'club-stage':
            stage(g, b, T)
        elif i.startswith('club-speaker-'):
            speaker(g, b, L, T)
        elif i == 'club-dj-booth':
            dj_booth(g, b, L, T)
        elif i == 'club-bar-counter':
            counter(g, b, T, ('+z',))
        elif i.startswith('club-island-') or i.endswith('-counter'):
            counter(g, b, T, ('+z', '-z', '+x', '-x'))
        elif i == 'club-backbar':
            backbar(g, b, T)
        elif i == 'club-bar-pillar':
            pillar(g, b, T)
        elif i.startswith('club-podium-'):
            podium(g, b, T)
        elif i.startswith('club-booth-'):
            booth(g, b, T)
        elif i.endswith('-sofa') or '-sofa-' in i or i == 'club-vip-booth':
            vel = F['velvetgreen'] if '-green-' in i else F['velvetblue'] if ('-lounge-' in i or '-sofa-mezz-' in i) else F['velvetdark'] if i == 'club-vip-booth' else F['velvet']
            sofa(g, b, T, vel)
        elif i.startswith('club-planter-'):
            planter(g, b, T)
        else:
            x0, x1, y0, y1, z0, z1 = ext(b)
            box(g, x0, x1, y0, y1, z0, z1, F['gunmetal'], T, bevel=.02)
        m = tally(g, 'block ' + (i.split('-')[1] if not i.startswith('club-stair') else 'stair'), m)
    # the kit: what is sculpted above is left out; the rest is rebuilt from its own parts
    import club_props as props
    lastfam = '-'
    for p in L['parts']:
        fam, kind = p['f'], ck.KIND[p['k']]
        m = tally(g, 'kit ' + lastfam, m)
        lastfam = fam
        if fam == 'upholstery' or (fam, kind) in SKIP:
            continue
        if fam == 'south-wall' and kind is None:
            continue
        if fam == 'south-wall' and kind == 'black' and p['h'][1] > .5:
            continue
        if fam == 'speaker' and kind == 'grille' and p['h'][2] < .05:
            continue
        if fam == 'stage' and kind == 'black' and abs(p['h'][1] - .05) < 1e-3:
            footlight(g, p['c'], T)
            continue
        if fam == 'backbar' and p['k'] == 10:
            bottle(g, p, ck.kit_fin(p, L), T)
            continue
        if fam == 'mirror' and kind is None:
            if T.i < 2:
                ball(g, p['c'], .04, ck.kit_fin(p, L), 6, 4)
            continue
        if fam == 'floor' and kind == 'hazard':
            hazard_row(g, p, T)
            continue
        if fam == 'hanger' and kind == 'black' and p['h'][0] > .1:
            c, h = p['c'], p['h']
            rbox(g, c[0] - h[0] * .8, c[0] + h[0] * .8, c[1] - h[1], c[1] + h[1], c[2] - h[2] * .8, c[2] + h[2] * .8, .05, F['crane'], 1)
            continue
        f = None
        if fam == 'tower' and kind == 'chrome':
            f = F['truss']
        if fam == 'hanger' and kind == 'black':
            f = F['steeldark']
        kit_part(g, p, T, L, f)
    m = tally(g, 'kit ' + lastfam, m)
    props.fittings(g, L, T)
    tally(g, 'fittings', m)
    return g


def views():
    return [('hall-north', (0, 1.8, -11), (0, 4, 17), 75), ('hall-south', (0, 2.8, 14), (0, 5, -19), 75), ('floor-truss', (-7, 1.6, -6), (2, 9, 3), 75),
            ('dj', (2.2, 2.7, 13.4), (0, 2.6, 17), 60), ('speaker', (-4.5, 1.6, 9.5), (-9.4, 2.4, 14.5), 60), ('gallery', (-17.6, 5.8, 1), (-27.6, 9.6, -3), 60),
            ('pit', (-13, 1.7, 18.4), (-13.6, 1.2, 22), 70), ('bar', (-3, 1.7, -10.3), (2, 1.5, -16), 65), ('booth', (-18.6, 1.7, -8.6), (-12, 1.0, -10.6), 65),
            ('mezz', (-18.2, 5.8, -13.5), (-23.5, 4.9, -4), 70), ('roof', (-12, 1.7, -4), (6, 16, 10), 75), ('arch', (-14, 5.8, 0), (-26, 10, 6), 55),
            ('column', (-12.5, 1.6, -5.5), (-15.2, 1.0, -7.6), 55), ('sofa', (-22.5, 5.8, 1.2), (-25.2, 4.7, -.6), 60), ('window', (0, 5.6, -8), (0, 8.5, -19.6), 70),
            ('greys', (-25.0, 9.9, 2.5), (-27.9, 9.6, -1.5), 50), ('dj-close', (1.1, 2.9, 15.9), (0, 2.75, 17), 50)]


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument('--layout', required=True)
    ap.add_argument('--out', help='Assets/LocalAssets to write the bank into')
    ap.add_argument('--preview', help='render views of the baked Fine tier')
    ap.add_argument('--views', help='only these views (comma separated)')
    ap.add_argument('--tiers', default='0,1,2')
    ap.add_argument('--no-ao', action='store_true')
    ap.add_argument('--static-only', action='store_true')
    ap.add_argument('--stats', action='store_true', help='print where the vertices of each tier go')
    args = ap.parse_args(argv)
    L = sk.load_layout(args.layout)
    global COUNT
    COUNT = args.stats
    ck.finishes(L)
    import club_props as props
    sb = sk.StageBank()
    chunks = sk.Chunks(12.0, (-36.0, -24.0), (-1.0, 9.0))
    floors = (0.0, 4.2)
    t0 = time.time()
    for ti in [int(x) for x in args.tiers.split(',')]:
        T = sk.Tier(ti)
        t = time.time()
        g = static_scene(L, T)
        scene = props.rest_scene(L, T)
        print('tier %s: static %d faces, %.1f s' % (T.name, g.faces, time.time() - t))
        if args.stats:
            print('  ' + ', '.join('%s %d' % kv for kv in sorted(STATS.items(), key=lambda kv: -kv[1]) if kv[1] > 0))
        STATS.clear()
        ao = None
        if not args.no_ao:
            ao = sk.bake_ao([g], distance=(1.4, 1.4, 1.6)[ti], samples=(32, 16, 12)[ti], occluders=[scene])[0]
        bk = sk.Bake(g, ao, soot=None, floors=floors)
        ids = chunks.ids(g)
        for cid in np.unique(ids):
            sb.add('static/%d/%d' % (cid, ti), 'static', sk.to_mesh('static/%d/%d' % (cid, ti), g, bk, ids == cid))
        if args.preview and ti == 0:
            vs = views()
            if args.views:
                vs = [v for v in vs if v[0] in args.views.split(',')]
            sk.preview([(g, bk), (scene, sk.Bake(scene, None))], vs, args.preview)
        if not args.static_only:
            props.bank_props(sb, L, T, floors, args.no_ao)
        del g
    if args.out:
        for b in L['blocks']:
            sb.cover(b['id'])
        sb.write(args.out, BANK)
    print('done in %.1f s' % (time.time() - t0))


if __name__ == '__main__':
    main()
