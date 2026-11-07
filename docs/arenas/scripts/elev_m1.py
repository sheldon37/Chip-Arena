# Brickfall M1 sections. 1 col = 1 m; 1 row = 1.2 m.
def grid(w, rows): return [[' '] * w for _ in range(rows)]
def show(g, label_rows, foot):
    for r in range(len(g) - 1, -1, -1):
        print(f'{r*1.2:5.1f} ' + ''.join(g[r]) + ('   ' + label_rows.get(r, '') if label_rows.get(r) else ''))
    print(foot)
def R(y): return int(round(y / 1.2))
# ---- Section A: N-S at x=-7, looking west (south on the left), intact round start
W = 57; g = grid(W, 14); C = lambda z: int(z + 28)
def h(z0, z1, y, ch):
    for z in range(z0, z1): g[R(y)][C(z)] = ch
def v(z, y0, y1, ch):
    for r in range(R(y0), R(y1) + 1): g[r][C(z)] = ch
for z in (-28, -27): v(z, 0, 14.4, '#')
for z in (26, 27): v(z, 0, 14.4, '#')
v(28 - 1, 14.4, 16, 'k') if False else None
for z in (-28, -27, -26): g[R(15.6)][C(z)] = 'k'              # south stack (x -11.2..-8.8, beyond the cut) rising to 16 m above the 14 m frontage
g[R(14.4)][C(-26)] = 'k'
h(-28, 28, 0, '_')
v(-6, 0, 11.8, '|'); v(5, 0, 11.8, '|')                      # hotel S and N faces
for y in (3.6, 7.2, 10.8): h(-6, 6, y, '=')
v(0, 0, 3.3, ':')                                            # wing seam partition (ground storey)
v(-6, 0, 2.4, 'P'); v(5, 0, 2.4, 'P')                        # corner piers SW / NW (x=-8, just behind the cut)
h(6, 12, 7.2, '#'); h(6, 12, 8.4, '+')                       # Ashby skybridge deck 7.2, rails
v(12, 0, 8.2, '|'); v(20, 0, 8.2, '|')                       # Ashby S and N (lane) faces
for y in (3.6, 7.2): h(12, 21, y, '=')
v(12, 0, 2.4, 'P'); v(20, 0, 2.4, 'P')                       # R2 rear pier, F3 lane pier
for z, y in ((-20, 0), (-9, 0), (-3, 3.6), (2, 7.2), (9, 7.2), (-2, 10.8), (15, 7.2), (23, 0)):
    g[R(y) + 1][C(z)] = '@'
lab = {0: '0.0  street', 3: '3.6  hotel F1 / Ashby F1', 6: '7.2  hotel F2 / skybridge / Ashby roof', 9: '10.8 hotel roof (+1.0 parapet)', 12: '14.0 frontage tops (drawn in the 14.4 row); S stack beyond the cut, 16 m'}
print('SECTION A-A  x = -7, looking west (S left, N right). Round start, everything intact.')
show(g, lab, '      ' + ''.join('^' if z in (-26, -12, -6, 6, 12, 21, 26) else ' ' for z in range(-28, 29)) +
     '\n      z: -26 frontage | Bray Steps -12 | S St -6 | HOTEL (SW|NW) +6 | N St +12 | ASHBY +21 | lane +26')
print()
# ---- Section B: W-E at z=+3, looking north, AFTER the NW quadrant pancake (hotel NE intact)
W = 64; g = grid(W, 12); C = lambda x: int(x + 32)
def hb(x0, x1, y, ch):
    for x in range(x0, x1): g[R(y)][C(x)] = ch
def vb(x, y0, y1, ch):
    for r in range(R(y0), R(y1) + 1): g[r][C(x)] = ch
for x in (-32, -31, 30, 31): vb(x, 0, 12.6, '#')
hb(-30, 30, 0, '_')
for x in range(-30, -14, 2): g[R(6)][C(x)] = '-'             # market canopy at 6 m (visual only, no collision)
for x in range(16, 30, 2): g[R(6)][C(x)] = '-'
for x in range(24, 30): vb(x, 0, 3.5, 'O')                   # east market storeroom 3.5 m
# rubble mound: 0.6 m at x=-9.5 (apron in West St) rising to 3.4 m at the core face x=-2.4
for x in range(-10, -2):
    top = 0.6 + (x + 9.5) / 7.1 * 2.8
    vb(x, 0, top, 'r')
g[R(2.4)][C(-7)] = '/'; g[R(3.6)][C(-4)] = '/'
for x in range(-2, 3): vb(x, 0, 11.2, 'C')                   # indestructible stair core (N landing door at 3.6 on its west face)
g[R(3.6)][C(-3)] = 'D'
vb(8, 0, 11.8, '|'); vb(3, 0, 11.8, '|')
for y in (3.6, 7.2, 10.8): hb(3, 9, y, '=')
vb(8, 0, 2.4, 'P')
for x, y in ((-12, 0), (-6, 2.0), (-3, 3.6), (5, 3.6), (6, 10.8), (20, 0)):
    g[R(y) + 1][C(x)] = '@'
lab = {0: '0.0  street', 2: '2.4', 3: '3.6  core N landing door D / NE floor 1', 5: '6.0  market canopies (visual only)', 6: '7.2', 9: '10.8 NE roof'}
print('SECTION B-B  z = +3, looking north. AFTER "hotel-nw" pancakes: rubble ramp r (<=22 deg) leads from West St to door D at 3.6.')
show(g, lab, '      ' + ''.join('^' if x in (-30, -14, -8, -2, 2, 8, 14, 30) else ' ' for x in range(-32, 32)) +
     '\n      x: -30 | West Market | -14 W St -8 | NW rubble | core | NE quadrant +8 | E St +14 | East Market | +30')
