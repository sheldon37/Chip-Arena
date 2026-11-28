"""The five Meridian size-B layouts. Each is authored by hand: tiers, then the four routes as turtles, then
connectors, structures, doors, the court and the lot zones. Run build_all.py to check and export them."""
from meridian_plan import Layout

GOLD, RED, CYAN, VIOLET = '#ffc83d', '#ff5a5f', '#38d0e8', '#b98cff'


# ------------------------------------------------------------------------------------------------------
# 1. CROWN HILL: the camp on a long shelf low on the hill's south face; everything climbs north to the Crown.
# ------------------------------------------------------------------------------------------------------
def crown_hill():
    L = Layout('meridian-b-crown-hill', 'Crown Hill', 'A hill town climbing in switchbacks to the Crown',
               (-300, 300, -190, 290), default_y=7, seed=647)
    L.tier_names = {-7: 'the Quay', 0: 'the Belvedere Shelf', 7: 'the lower hill', 14: 'the upper hill',
                    21: 'the Shoulders', 28: 'the Crown'}
    L.paint((-300, -190, 300, -50), -7)            # the Quay along the river wall
    L.paint((-110, -50, 300, 45), 0)               # the Belvedere Shelf, running out east
    L.paint((170, 20, 300, 45), 7)
    L.paint((-60, 75, 60, 110), 14)                # the Belvedere Terrace at the top of the Hundred Steps
    L.paint((-300, 110, 300, 290), 14)             # the upper hill
    L.paint((199, 20, 300, 110), 14)               # Belvedere Heights, the east terrace
    L.paint((-160, 115, 160, 290), 21)             # the Shoulders
    L.paint((-90, 129, 90, 290), 28)               # the Crown
    L.hub('Belvedere Shelf: a long park terrace with the Quay below and the hill above', [(-60, -46, 60, 42)])

    # N: the Hundred Steps (14 m in four flights), Clock Street, the two switchbacks of the Zigzag, the Crown Walk
    n = L.route('n', 'FINAL COURT', 'N', 'court', GOLD)
    n.go(13, 10).stairs(14, 12, 'balustraded').go(35, 14, 'open')
    n.turn('L').slope(7, 56, 10, 'edge').go(8, 10).turn('R').go(14, 10).turn('R')
    n.slope(7, 56, 10, 'edge').go(8, 10).turn('L').go(68, 12)
    L.city_hall(0, 224, 28, gate='S')

    # E: the Promenade along the shelf's edge, then two climbs to Belvedere Heights (raised door)
    e = L.route('e', 'DOOR A', 'E', 'door-a', RED)
    e.go(30, 20, 'edge').stairs(-3.5, 20, 'edge').go(78, 20, 'edge').turn('L').slope(10.5, 63, 10, 'walled').turn('R').slope(7, 49, 10, 'edge').go(70, 14)
    L.door('a', 'Belvedere Heights hall', 259, 63, 14, 'W', 'raised')

    # S: the Chute, a narrow stair between tenements down to the Quay, then Quay Road west (ground door)
    s = L.route('s', 'DOOR B', 'S', 'door-b', CYAN)
    s.go(8, 6).stairs(-7, 5, 'walled').go(20, 10).turn('R').go(245, 15, 'open')
    L.door('b', 'Vale Cold Store', -245, -70, -7, 'E', 'ground', hall_w=44)

    # W: the Hill Cutting, a ramp down into a deepening trench to the tunnel mouth (sunk door)
    w = L.route('w', 'DOOR C', 'W', 'door-c', VIOLET)
    w.go(3, 8).slope(-7, 56, 8, 'cutting').go(196, 8, 'cutting')
    L.door('c', 'Hill Cutting tunnel mouth', -280, 0, -7, 'E', 'sunk', hall_d=18)

    # ---- connectors and structures -------------------------------------------------------------
    L.ramp('quay-haul-ramp', 'ramp', (115, -106, 125, -50), 'N', -7, 0)                 # Quay up to the shelf
    L.ramp('cutting-stair-n-1', 'stair', (-143, 4, -137, 11), 'N', -7, -3.5)
    L.paint((-143, 11, -137, 14), -3.5); L.reserve((-143, 11, -137, 14), 1)
    L.ramp('cutting-stair-n-2', 'stair', (-143, 14, -137, 21), 'N', -3.5, 0)
    L.paint((-143, 21, -137, 24), 0); L.reserve((-143, 21, -137, 24), 1)
    L.ramp('cutting-stair-n-3', 'stair', (-143, 24, -137, 31), 'N', 0, 3.5)
    L.paint((-143, 31, -137, 34), 3.5); L.reserve((-143, 31, -137, 34), 1)
    L.ramp('cutting-stair-n-4', 'stair', (-143, 34, -137, 41), 'N', 3.5, 7)
    for bx in (-232, -172):                                                               # footbridges at +7
        L.deck('cutting-footbridge-%d' % -bx, (bx - 2, -8, bx + 2, 8), 7, along='NS')
    L.paint((-204, -50, -196, -4), -7)                                                    # the arch to the Quay
    L.block('cutting-arch-roof', 'tunnel-roof', (-206, -46, -194, -8), -1, 7, roof='walkable')
    L.reserve((-204, -50, -196, -4), 2)
    for x in (-230, 230):                                                                 # +7 to +14 stairs
        L.ramp('hill-stair-%d-1' % x, 'stair', (x - 4, 93, x + 4, 100), 'N', 7, 10.5)
        L.paint((x - 4, 100, x + 4, 103), 10.5); L.reserve((x - 4, 100, x + 4, 103), 1)
        L.ramp('hill-stair-%d-2' % x, 'stair', (x - 4, 103, x + 4, 110), 'N', 10.5, 14)
    L.ramp('shoulder-ramp-e', 'ramp', (160, 140, 209, 150), 'W', 14, 21)
    L.ramp('crown-ramp-e', 'ramp', (90, 200, 139, 210), 'W', 21, 28)
    L.ramp('shoulder-ramp-w', 'ramp', (-209, 140, -160, 150), 'E', 14, 21)
    L.ramp('west-hill-stair-1', 'stair', (-275, -50, -267, -43), 'N', -7, -3.5)          # Quay to the west hill
    L.paint((-275, -43, -267, -40), -3.5); L.reserve((-275, -43, -267, -40), 1)
    L.ramp('west-hill-stair-2', 'stair', (-275, -40, -267, -33), 'N', -3.5, 0)
    L.paint((-275, -33, -267, -30), 0); L.reserve((-275, -33, -267, -30), 1)
    L.ramp('west-hill-stair-3', 'stair', (-275, -30, -267, -23), 'N', 0, 3.5)
    L.paint((-275, -23, -267, -20), 3.5); L.reserve((-275, -23, -267, -20), 1)
    L.ramp('west-hill-stair-4', 'stair', (-275, -20, -267, -13), 'N', 3.5, 7)
    # the Quay's cranes and the bandstand
    L.block('quay-crane-1', 'landmark', (-128, -170, -118, -160), -7, 24, roof='walkable')
    L.block('quay-crane-2', 'landmark', (52, -176, 62, -166), -7, 24, roof='walkable')
    L.block('park-bandstand', 'landmark', (36, -40, 48, -30), 0, 4, roof='pitched')
    # tenements that wall the Chute
    L.block('chute-tenement-w', 'lot', (-26, -50, -3, -31), 0, 19, roof='walkable', style='tenements', district='chute')
    L.block('chute-tenement-e', 'lot', (3, -50, 26, -31), 0, 16, roof='walkable', style='tenements', district='chute')

    L.landmark('steps', 'The Hundred Steps (14 m)', 9, 56, 7, 'stair')
    L.landmark('zigzag', 'The Zigzag (two switchbacks)', -32, 117, 21, 'ramp')
    L.landmark('cutting', 'The Hill Cutting and its footbridges', -200, 0, -7, 'cut')
    L.landmark('cranes', 'The Quay cranes', -123, -165, 24, 'crane')
    L.landmark('bandstand', 'Belvedere bandstand', 42, -35, 0, 'park')
    L.landmark('promenade', 'The Promenade balustrade', 90, -10, 0, 'edge')

    # ---- lots --------------------------------------------------------------------------------
    L.zone('shelf-n', (60, 14, 170, 45), 'rows', 0.9, along='EW')
    L.zone('shelf-s', (60, -50, 300, -14), 'rows', 0.75)
    L.zone('shelf-w', (-110, -50, -60, 45), 'shops', 0.7)
    L.zone('shelf-e', (170, 0, 300, 20), 'rows', 0.8)
    L.zone('lower-hill', (-300, 45, 300, 110), 'rows', 0.65)
    L.zone('west-hill', (-300, -50, -110, 45), 'rows', 0.6)
    L.zone('upper-hill-w', (-300, 110, -160, 290), 'tenements', 0.55)
    L.zone('upper-hill-e', (160, 110, 300, 290), 'tenements', 0.55)
    L.zone('heights', (199, 20, 300, 110), 'villas', 0.5)
    L.zone('shoulders', (-160, 129, 160, 290), 'shops', 0.6)
    L.zone('crown', (-90, 147, 90, 290), 'civic', 0.6)
    L.zone('quay-s', (-300, -190, 300, -82), 'warehouses', 0.6)
    L.description = (
        "You start on the Belvedere Shelf, a long park terrace low on the hill, with the Quay and the river wall "
        "below you and the whole city climbing away to the north. Every route reads by height: the final court is "
        "the top of the hill: 14 m up the Hundred Steps from the gate, then Clock Street and back and forth up the two switchbacks "
        "of the Zigzag, so the horde chases you uphill and drops on you from the balustrades above. East runs "
        "the open Promenade along the shelf's edge, down a broad flight to the Lower Promenade, then two climbs to a raised hall on Belvedere Heights. "
        "South, the Chute drops you down a narrow stair between tenements to the Quay, where a ground-level "
        "warehouse door waits at the end of a long open quayside with cranes overhead. West, a ramp sinks into "
        "the Hill Cutting, a trench that deepens to 14 m under two footbridges, ending at a sunk tunnel mouth. "
        "Rows built against each terrace wall have roofs level with the street above, so the roofs are a second "
        "street all over the hill.")
    return L


from layouts_2to5 import navigation, arcades, stride, yards
ALL = [crown_hill, navigation, arcades, stride, yards]
