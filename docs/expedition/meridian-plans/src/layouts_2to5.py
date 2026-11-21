"""Layouts 2-5 (see layouts.py for Crown Hill and the shared colours)."""
from meridian_plan import Layout

GOLD, RED, CYAN, VIOLET = '#ffc83d', '#ff5a5f', '#38d0e8', '#b98cff'


# ------------------------------------------------------------------------------------------------------
# 2. THE NAVIGATION: a sunken canal splits the city; the camp is on Mill Island inside a square moat.
# ------------------------------------------------------------------------------------------------------
def navigation():
    L = Layout('meridian-b-navigation', 'The Navigation', 'A canal city split by a sunken channel, crossed by bridges',
               (-280, 320, -220, 260), default_y=0, seed=1874)
    L.tier_names = {-14: 'the Navigation (drained bed)', 0: 'the banks and Mill Island', 7: 'the Mall terrace and the Wharf',
                    14: 'Wharf Heights'}
    for r in ((-125, 45, 125, 85), (-125, -85, 125, -45), (-125, -85, -85, 85), (85, -85, 125, 85),
              (-280, -20, -125, 20), (125, -20, 320, 20)):
        L.paint(r, -14)
    L.paint((-125, 120, 320, 260), 7)
    L.paint((-280, 20, -125, 100), 7)
    L.paint((-280, 100, -125, 260), 14)
    L.hub('Mill Island: a long island inside a square moat, every gate a crossing', [(-85, -45, 85, 45)])

    n = L.route('n', 'FINAL COURT', 'N', 'court', GOLD)
    n.go(20, 10).go(40, 8, 'bridge', deck=True, deck_id='iron-bridge').go(15, 12).turn('R').go(110, 12, 'edge')
    n.turn('L').slope(7, 56, 16, 'open').go(30, 16)
    L.city_hall(110, 218, 7, gate='S')

    e = L.route('e', 'DOOR A', 'E', 'door-a', RED)
    e.go(22, 12).slope(-14, 56, 12, 'open').go(195, 12, 'channel')
    L.door('a', 'Navigation Tunnel portal', 298, 0, -14, 'W', 'sunk', hall_d=20, hall_w=36)

    s_ = L.route('s', 'DOOR B', 'S', 'door-b', CYAN)
    s_.go(6, 8).stairs(-7, 6, 'open').go(37, 6, 'causeway', deck=True, deck_id='low-causeway').stairs(7, 6, 'walled')
    s_.go(40, 10).turn('R').go(140, 12)
    L.door('b', 'The Canal Exchange', -140, -142, 0, 'E', 'ground')

    w = L.route('w', 'DOOR C', 'W', 'door-c', VIOLET)
    w.go(6, 10).turn('R').go(30, 10).turn('L').go(8, 10).stairs(7, 8, 'walled').go(35, 10)
    w.go(40, 10, 'covered bridge', deck=True, deck_id='mill-bridge-house', lid=(5, 12)).go(40, 10).turn('R').go(28, 10)
    w.slope(7, 42, 8, 'walled').go(40, 10)
    L.door('c', 'Wharf Heights hall', -171, 140, 14, 'S', 'raised')

    # crossings and ways out of the bed (no pits)
    L.deck('navigation-road-bridge', (194, -26, 206, 26), 0, along='NS')
    L.deck('east-footbridge', (263, -24, 267, 24), 0, along='NS')
    L.deck('north-arm-footbridge', (58, 41, 62, 89), 0, along='NS')
    L.deck('south-arm-footbridge', (-52, -89, -48, -41), 0, along='NS')
    L.deck('west-channel-bridge', (-224, -24, -216, 20), 0, along='NS')
    L.stairway('west-bridge-stair', -220, 20, 'N', 0, 7, 8)
    L.stairway('east-bed-stair', 240, -20, 'S', -14, 0, 6)
    L.stairway('north-arm-stair', -60, 85, 'N', -14, 0, 6)
    L.stairway('south-arm-stair', 60, -85, 'S', -14, 0, 6)
    L.stairway('west-bed-stair', -160, -20, 'S', -14, 0, 6)
    L.stairway('mall-east-stair', 230, 103, 'N', 0, 7, 6)
    L.stairway('heights-stair', -230, 83, 'N', 7, 14, 6)
    for k, r in enumerate(((150, -8, 176, -1), (-205, 5, -180, 12), (-25, 57, 2, 64), (25, -74, 52, -67))):
        L.block('barge-%d' % k, 'landmark', r, -14, -11, roof='walkable', reserve=True, margin=1)
    L.block('mill', 'lot', (-80, -40, -52, -10), 0, 13, roof='walkable', style='warehouses', district='island')
    L.block('lock-cottage', 'lot', (52, -40, 76, -26), 0, 7, roof='pitched', style='rows', district='island')
    L.landmark('iron-bridge', 'The Iron Bridge', 0, 65, 0, 'bridge')
    L.landmark('causeway', 'The Low Causeway, 7 m under the banks', 0, -66, -7, 'bridge')
    L.landmark('mill', 'The Mill bridge-house, a covered bridge at +7', -108, 30, 7, 'bridge')
    L.landmark('lock', 'The East Lock ramp into the bed', 75, 0, -7, 'ramp')
    L.landmark('barges', 'Stranded barges in the drained bed', 163, -4, -14, 'cover')
    L.landmark('road-bridge', 'Navigation Road bridge', 200, 0, 0, 'bridge')
    L.landmark('mall', 'The Mall, a ramped avenue', 110, 128, 3.5, 'ramp')

    L.zone('south-w', (-280, -220, -125, -20), 'warehouses', 0.8)
    L.zone('south-c', (-125, -220, 125, -85), 'shops', 0.8)
    L.zone('south-e', (125, -220, 320, -20), 'warehouses', 0.8)
    L.zone('north-strip', (-125, 85, 320, 120), 'rows', 0.85)
    L.zone('north-e', (125, 20, 320, 85), 'warehouses', 0.75)
    L.zone('mall', (-125, 120, 320, 260), 'tenements', 0.7)
    L.zone('wharf', (-280, 20, -125, 100), 'warehouses', 0.75)
    L.zone('heights', (-280, 100, -125, 260), 'tenements', 0.65)
    L.description = (
        "The camp stands on Mill Island, ringed by a drained canal 14 m deep, so every exit is a crossing and the "
        "bed below is a second city of stranded barges and stairs. North, the open Iron Bridge leads straight over "
        "the moat, then along Canal Parade and up the Mall, a ramped avenue, to City Hall on the +7 terrace. East, "
        "the Lock ramp drops you 14 m into the bed itself and you run the channel to the Navigation Tunnel, a sunk "
        "door where the canal goes under the Cordon. South, a stair takes you down to the Low Causeway on arches "
        "halfway down the moat and up the far side to the Canal Exchange, a ground door on the south bank. West, a "
        "dog-leg climbs to the Mill, a covered bridge-house at +7, and on up to a raised hall on Wharf Heights. "
        "Zombies pour along the bed under the bridges; five bridges and five stairs out of the bed keep every "
        "fight one decision from a crossing.")
    return L


# ------------------------------------------------------------------------------------------------------
# 3. THE ARCADES: the covered market. The camp is in the Market Square, the lowest street, ringed by arcades.
# ------------------------------------------------------------------------------------------------------
def arcades():
    L = Layout('meridian-b-arcades', 'The Arcades', 'A covered-market district of arcades, roofs and an undercroft',
               (-300, 300, -230, 250), default_y=0, seed=1906)
    L.tier_names = {-7: 'the Undercroft', 0: 'the market streets', 7: "the Leads (roofs), Weavers' Rise and the Mound",
                    14: 'the Castle Mound'}
    L.paint((-300, -120, -190, 40), 7)          # Weavers' Rise
    L.paint((150, 120, 300, 250), 7)            # the Castle Mound
    L.paint((200, 170, 300, 250), 14)
    L.hub('Market Square: a cloister of arcades round the camp, the lowest street in the district', [(-45, -45, 45, 45)])
    # the arcade ring: posts on the square side, a walkable roof at +7 joining the ring buildings' roofs
    L.arcade('arcade-n', (-55, 45, 55, 55), 0, 'EW', height=6, sides='s')
    L.arcade('arcade-s', (-55, -55, 55, -45), 0, 'EW', height=6, sides='n')
    L.arcade('arcade-w', (-55, -45, -45, 45), 0, 'NS', height=6, sides='e')
    L.arcade('arcade-e', (45, -45, 55, 45), 0, 'NS', height=6, sides='w')
    for k, r in enumerate(((-70, 55, -8, 70), (8, 55, 70, 70), (-70, -70, -23, -55), (-7, -70, 70, -55),
                           (-70, -55, -55, 55), (55, -55, 70, 14), (55, 26, 70, 55))):
        L.block('ring-%d' % k, 'lot', r, 0, 7, roof='walkable', style='market', district='ring', roof_link='arcade')
    L.reserve((-72, -72, 72, 72), 0)

    n = L.route('n', 'FINAL COURT', 'N', 'court', GOLD)
    n.go(20, 12).go(105, 12, 'galleria', lid=(11, 11.5), margin=0.5).turn('L').go(100, 12, 'galleria', lid=(11, 11.5), margin=0.5)
    n.turn('R').go(30, 14)
    L.city_hall(-100, 212, 0, gate='S')
    for k, r in enumerate(((-21, 72, -6.5, 100), (6.5, 72, 21, 100), (-21, 110, -6.5, 140), (6.5, 110, 21, 168),
                           (-90, 156.5, -20, 170), (-90, 128, -30, 143.5))):
        L.block('galleria-wing-%d' % k, 'lot', r, 0, 13, roof='walkable', style='civic', district='galleria')
    L.block('rotunda-dome', 'lid', (-8, 142, 8, 158), 14, 15, roof='walkable', reserve=False)

    e = L.route('e', 'DOOR A', 'E', 'door-a', RED)
    e.go(22, 10).turn('L').go(20, 5, 'arcade', paint=False, margin=0.5).turn('R').go(70, 5, 'jettied lane', margin=0.5)
    e.turn('R').go(15, 5, 'jettied lane', margin=0.5).turn('L').go(135, 8, 'open', margin=1.5)
    L.door('a', 'The Wool Hall', 252, 5, 0, 'W', 'ground')
    for k, r in enumerate(((71, 23, 90, 40), (92, 23, 112, 40), (71, -8, 90, 17), (92, -8, 111.5, 17))):
        L.block('shambles-%d' % k, 'lot', r, 0, 10, roof='walkable', style='market', district='shambles')
    for x in (84, 104):
        L.block('shambles-jetty-%d' % x, 'lid', (x - 3, 17, x + 3, 23), 4.5, 10, roof='walkable', reserve=False)

    s_ = L.route('s', 'DOOR B', 'S', 'door-b', CYAN)
    s_.go(20, 10).turn('R').go(15, 10, 'arcade', paint=False, margin=0.5).turn('L').slope(-7, 42, 10, 'undercroft')
    s_.go(40, 10, 'undercroft', lid=(6, 7)).turn('L')
    s_.go(50, 10, 'undercroft', lid=(6, 7)).go(10, 10, 'light well').go(50, 10, 'undercroft', lid=(6, 7))
    s_.turn('R').go(36, 10)
    L.block('undercroft-ramp-lid', 'lid', (-21, -87, -9, -71), -1, 0, roof='walkable', reserve=False)
    L.door('b', 'The Bonded Vaults', 95, -163, -7, 'N', 'sunk')
    L.stairway('vaults-stair', 120, -148, 'E', -7, 0, 6)

    w = L.route('w', 'DOOR C', 'W', 'door-c', VIOLET)
    w.go(8, 8).turn('L').slope(3.5, 7, 8, 'stair tower', kind='stair').go(3, 8).turn('R').go(8, 8).turn('R')
    w.slope(3.5, 7, 8, 'stair tower', kind='stair').go(3, 8).turn('L').go(4, 8).go(10, 8, 'leads', paint=False).go(15, 8, 'leads', paint=False)
    w.go(30, 12, 'leads', roof=True).go(8, 5, 'bridge', deck=True).go(40, 12, 'leads', roof=True).turn('L')
    w.go(8, 5, 'bridge', deck=True).go(30, 12, 'leads', roof=True).turn('R').go(8, 5, 'bridge', deck=True)
    w.go(34, 12, 'leads', roof=True).go(40, 12)
    L.door('c', 'The Exchange Loft', -230, -38, 7, 'E', 'raised')

    L.stairway('rise-ramp-n', -205, 57, 'S', 0, 7, 6)
    L.ramp('rise-ramp-s', 'ramp', (-246, -162, -234, -120), 'N', 0, 7)
    L.stairway('mound-stair', 170, 103, 'N', 0, 7, 6)
    L.stairway('mound-stair-2', 230, 153, 'N', 7, 14, 6)
    L.block('mound-keep', 'landmark', (250, 205, 270, 225), 14, 40, roof='walkable')
    L.landmark('galleria', 'The Galleria, a glass-roofed arcade', 0, 100, 0, 'covered')
    L.landmark('rotunda', 'The Rotunda crossing', 0, 150, 0, 'covered')
    L.landmark('shambles', 'The Shambles, a jettied lane', 85, 20, 0, 'lane')
    L.landmark('undercroft', 'The Undercroft, a vaulted street under Market Row', 40, -127, -7, 'tunnel')
    L.landmark('leads', 'The Leads: roofs and bridges at +7', -120, 0, 7, 'roofs')
    L.landmark('keep', 'The Castle Mound keep', 260, 215, 40, 'tower')
    L.landmark('ring', 'The market ring and its arcades', 62, -62, 7, 'arcade')

    L.zone('market-w', (-190, -120, -72, 120), 'market', 0.9)
    L.zone('market-e', (72, -120, 150, 120), 'market', 0.85)
    L.zone('market-n', (-72, 72, 72, 120), 'market', 0.85)
    L.zone('market-s', (-72, -120, 72, -72), 'market', 0.85)
    L.zone('north', (-190, 120, 150, 250), 'shops', 0.75)
    L.zone('south', (-190, -230, 150, -120), 'tenements', 0.65)
    L.zone('rise', (-300, -120, -190, 40), 'rows', 0.8)
    L.zone('west-s', (-300, -230, -190, -120), 'warehouses', 0.75)
    L.zone('west-n', (-300, 40, -190, 250), 'villas', 0.7)
    L.zone('mound', (150, 120, 300, 250), 'tenements', 0.6)
    L.zone('east', (150, -230, 300, 120), 'warehouses', 0.75)
    L.post_fill = [lambda L: L.skybridges('market', 6.0, 3.0, 45)]
    L.description = (
        "Here the camp is at the bottom: the Market Square is the lowest street, ringed by arcades whose roofs, "
        "with the market halls around them, form the Leads, a second street of flat roofs and footbridges at +7 "
        "that runs right across the district. North, the Galleria carries you under glass to the Rotunda and "
        "along its west arm to City Hall's court, a level approach where zombies drop in from the roofs on both "
        "sides. East, you turn left under the arcade and thread the Shambles, a 5 m jettied lane with a dog-leg, to the "
        "Wool Hall, a ground-level door. South, you jog right under the arcade and a covered ramp sinks into the Undercroft, a vaulted street under "
        "Market Row lit by one light well, to the Bonded Vaults in a sunken yard. West, a two-flight stair tower "
        "lifts you onto the Leads, and you cross the roofs and their bridges to the Exchange Loft, raised on "
        "Weavers' Rise. Sight lines are short everywhere: the danger is above and below you, not down the street.")
    return L


# ------------------------------------------------------------------------------------------------------
# 4. THE STRIDE: the railway viaduct crosses the whole map at +7; streets run beneath and beside it.
# ------------------------------------------------------------------------------------------------------
def stride():
    L = Layout('meridian-b-stride', 'The Stride', 'A viaduct spine across the city, streets beneath it',
               (-330, 270, -230, 250), default_y=0, seed=1868)
    L.tier_names = {-14: 'the Goods Cut', -7: 'the Vale and Arch Valley', 0: 'the Arch Yard level', 7: 'the upper town and the deck',
                    14: 'the Crag'}
    L.paint((-330, -230, 270, -60), -7)
    L.paint((-330, -60, -150, 130), -7)
    L.paint((-150, 90, 270, 250), 7)
    L.paint((-330, 130, -150, 250), 7)
    L.paint((150, -60, 270, 90), 7)
    L.paint((30, 150, 200, 250), 14)
    L.paint((96, -110, 270, -76), -14)
    L.hub('Arch Yard: a yard tucked under the viaduct, the Vale dropping away south', [(-50, -45, 70, 40)])

    n = L.route('n', 'FINAL COURT', 'N', 'court', GOLD)
    n.go(14, 8).go(24, 8, 'under the arch', margin=0.5).slope(7, 56, 8, 'walled').turn('R').go(100, 12).turn('L')
    n.slope(7, 42, 10, 'walled').go(25, 12)
    L.city_hall(100, 218, 14, gate='S')

    e = L.route('e', 'DOOR A', 'E', 'door-a', RED)
    e.go(15, 10).turn('L').stairs(7, 6, 'open').go(34, 6).turn('R').go(200, 12, 'deck', paint=False)
    L.door('a', 'The Terminus', 240, 51, 7, 'W', 'raised', hall_w=36)

    s_ = L.route('s', 'DOOR B', 'S', 'door-b', CYAN)
    s_.slope(-7, 28, 10, 'open').go(40, 10).turn('L').go(40, 10).slope(-7, 56, 10, 'cutting').go(110, 10, 'cutting')
    L.door('b', 'The Goods Cut sheds', 206, -93, -14, 'W', 'sunk', hall_w=30)

    w = L.route('w', 'DOOR C', 'W', 'door-c', VIOLET)
    w.go(40, 12).stairs(-7, 12, 'walled').go(38, 12).turn('R').go(80, 8, 'under the arch', margin=0.5).turn('L').go(115, 12)
    L.door('c', 'Arches Market hall', -235, 80, -7, 'E', 'ground')

    # the viaduct: a 600 m deck at +7 on piers every 10 m, with gaps in its parapets where stairs and bridges land
    L.block('viaduct-deck', 'viaduct', (-330, 45, 270, 57), 6, 7, roof='walkable')
    def parapet(side, z0, z1, gaps):
        x = -330.0
        for g0, g1 in sorted(gaps) + [(270, 270)]:
            if g0 > x: L.block('viaduct-parapet-%s-%d' % (side, int(x)), 'parapet', (x, z0, g0, z1), 7, 8.1, reserve=False)
            x = g1
    parapet('s', 45, 45.5, [(36, 44), (-205, -195)]); parapet('n', 56.5, 57, [(-62, -58)])
    x = -325.0
    while x < 270:
        if not (-6 < x < 6):
            gy = L.ground_at(x, 51)
            if gy < 5: L.block('viaduct-pier-%d' % int(x), 'pier', (x - 1, 45, x + 1, 57), gy, 6, reserve=False)
        x += 10
    L.reserve((-330, 44, 270, 58), 0)
    for k in range(7):                                       # the stalled 6:47 on the deck
        x0 = -150 + k * 21.5
        L.block('train-car-%d' % k, 'wagon', (x0, 52.5, x0 + 20, 55.5), 7, 11, roof='walkable', reserve=False)
    L.deck('signal-bridge', (-62, 57, -58, 90), 7, along='NS')
    L.stairway('valley-deck-stair', -240, 40, 'E', -7, 7, 6)
    L.paint((-203, 37, -195, 45), 7)
    L.stairway('valley-north-stair', -250, 93, 'N', -7, 7, 6)
    L.stairway('vale-stair', -120, -77, 'N', -7, 0, 6)
    L.ramp('vale-ramp', 'ramp', (-46, -116, -34, -60), 'N', -7, 0)
    L.stairway('upper-stair', 120, 73, 'N', 0, 7, 6)
    L.ramp('east-rise-ramp', 'ramp', (94, 4, 150, 16), 'E', 0, 7)
    L.stairway('cut-stair', 180, -110, 'S', -14, -7, 6)
    L.stairway('crag-stair', 180, 133, 'N', 7, 14, 6)
    L.landmark('stride', 'The Stride: a 600 m viaduct deck at +7', -100, 51, 7, 'viaduct')
    L.landmark('train', 'The stalled 6:47 train on the deck', -80, 54, 7, 'cover')
    L.landmark('arch', "Arch Nought, the camp's north arch", 0, 51, 0, 'arch')
    L.landmark('signal', 'The Signal Bridge, deck to upper town', -60, 73, 7, 'bridge')
    L.landmark('valley', 'Arch Valley, piers 14 m tall', -250, 51, -7, 'viaduct')
    L.landmark('crag-road', 'Crag Road and Crag Street', 50, 119, 7, 'street')
    L.landmark('haul', 'The Vale haul ramp', 0, -40, -3.5, 'ramp')

    L.zone('vale-w', (-330, -230, -30, -60), 'rows', 0.8)
    L.zone('vale-c', (-30, -230, 96, -60), 'tenements', 0.65)
    L.zone('vale-e', (96, -230, 270, -110), 'warehouses', 0.75)
    L.zone('mid', (-150, -60, 150, 44), 'shops', 0.75)
    L.zone('under', (-150, 58, 150, 90), 'rows', 0.85)
    L.zone('rise', (150, -60, 270, 44), 'warehouses', 0.7)
    L.zone('rise-n', (150, 58, 270, 90), 'rows', 0.8)
    L.zone('upper', (-150, 90, 270, 250), 'tenements', 0.65)
    L.zone('valley', (-330, -60, -150, 44), 'warehouses', 0.7)
    L.zone('valley-n', (-330, 58, -150, 250), 'rows', 0.75)
    L.description = (
        "One structure organises the whole map: the Stride, a 600 m railway viaduct on brick arches, 7 m up, "
        "running edge to edge just north of the camp. The deck is a long open fighting line with a stalled train "
        "for cover, and the streets pass under it through the arches. North, you go straight under Arch Nought and "
        "up Crag Road and Crag Street to City Hall on the Crag. East, you climb a short stair onto the deck itself "
        "and run it to the Terminus, a raised door where the line lands on the upper town. South, a steep haul "
        "ramp drops you into the Vale and a cutting takes you down again to a sunk door in the Goods Cut. West, "
        "the yard runs out flat to the Valley Steps, then a sunken lane turns north under the viaduct into Arch Valley, where the piers stand 14 m tall, to the Arches "
        "Market hall, a door on the lowest street. The horde moves under the deck while you fight on it; the "
        "Signal Bridge and four stairs tie the deck, the valley and the upper town together.")
    return L


# ------------------------------------------------------------------------------------------------------
# 5. THE YARDS: rail yards stepping down west to east; the camp on the Junction Wedge in the Goods Yard.
# ------------------------------------------------------------------------------------------------------
def yards():
    L = Layout('meridian-b-yards', 'The Yards', 'Rail yards on split levels, sheds and footbridges',
               (-260, 340, -200, 280), default_y=0, seed=1890)
    L.tier_names = {-14: 'the Main Line Cut', -7: 'the Engine Yard', 0: 'the Goods Yard', 7: 'the High Yard and Station Approach',
                    14: 'Station Hill and the Coal Stage'}
    L.paint((-260, -90, -60, 140), 7)            # the High Yard
    L.paint((-60, 130, 110, 170), 7)             # Station Approach
    L.paint((-260, 140, 40, 280), 14)            # Station Hill
    L.paint((110, -200, 340, 280), -7)           # the Engine Yard
    L.paint((202, -200, 242, 280), -14)          # the Main Line Cut
    L.paint((-260, 25, -214, 65), 14)            # the Coal Stage
    L.hub('Junction Wedge: a stepped wedge of yard between diverging lines',
          [(-45, -30, 60, 45), (-30, -60, 45, -30), (-15, -85, 30, -60)])

    n = L.route('n', 'FINAL COURT', 'N', 'court', GOLD)
    n.go(3, 8).slope(7, 42, 8, 'steel ramp').go(60, 8, 'gantry', deck=True, deck_id='signal-gantry').go(20, 10)
    n.turn('L').slope(7, 56, 10, 'walled').go(40, 12).turn('R').go(40, 12)
    L.city_hall(-96, 222, 14, gate='S')
    L.piers('signal-gantry-legs', (-4, 70, 4, 130), 0, 6, 'NS', spacing=12, thick=1.2)

    e = L.route('e', 'DOOR A', 'E', 'door-a', RED)
    e.go(10, 10).turn('R').slope(-7, 56, 10, 'incline').go(10, 10).turn('L').go(75, 12).go(50, 12)
    e.slope(-7, 42, 10, 'walled').go(20, 10).turn('R').go(20, 10)
    L.door('a', 'Main Line tunnel portal', 222, -86, -14, 'N', 'sunk', hall_w=30)

    s_ = L.route('s', 'DOOR B', 'S', 'door-b', CYAN)
    s_.go(6, 5).stairs(7, 5, 'open').go(26, 5, 'covered footbridge', deck=True, deck_id='goods-footbridge', lid=(3.4, 3.9))
    s_.stairs(-7, 5, 'open').go(40, 12, 'goods shed', lid=(10, 11)).turn('R').go(110, 12).turn('L').go(40, 12)
    L.door('b', 'The Bonded Warehouse', -110, -171, 0, 'N', 'ground')

    w = L.route('w', 'DOOR C', 'W', 'door-c', VIOLET)
    w.go(20, 16).slope(7, 28, 16, 'open').go(85, 20, 'sidings').turn('R').go(45, 12).turn('L').slope(7, 56, 10, 'coal stage')
    w.go(24, 10)
    L.door('c', 'The Coal Stage hall', -238, 45, 14, 'E', 'raised', plaza=9, hall_d=20)

    L.stairway('high-yard-stair', -60, 100, 'W', 0, 7, 6)
    L.stairway('approach-stair', 80, 113, 'N', 0, 7, 6)
    L.stairway('station-hill-stair', -200, 123, 'N', 7, 14, 6)
    L.stairway('bonded-stair', -150, -107, 'N', 0, 7, 6)
    L.stairway('engine-stair', 127, 120, 'W', -7, 0, 6)
    L.stairway('engine-stair-s', 127, -150, 'W', -7, 0, 6)
    L.stairway('cut-stair-n', 242, 200, 'E', -14, -7, 6)
    L.stairway('cut-stair-s', 242, -180, 'E', -14, -7, 6)
    for z in (150, -150):
        L.deck('cut-bridge-%d' % abs(z), (198, z - 5, 246, z + 5), -7, along='EW')
    L.block('goods-shed-wall-s', 'wall', (-125, -122, -5, -121), 0, 10)
    L.block('goods-shed-wall-n', 'wall', (-125, -109, -5, -108), 0, 10)
    L.block('goods-shed-roof', 'lid', (-125, -122, -5, -108), 10, 11, roof='pitched', reserve=False)
    L.block('coaling-tower', 'landmark', (150, 30, 166, 46), -7, 25, roof='walkable')
    L.block('water-tower', 'landmark', (-120, -40, -110, -30), 7, 19, roof='walkable')
    L.block('signal-box', 'landmark', (56, 86, 66, 94), 0, 10, roof='walkable')
    L.block('roundhouse-a', 'shed', (260, 150, 320, 210), -7, 6, roof='pitched')
    L.block('roundhouse-b', 'shed', (270, 140, 310, 220), -7, 6, roof='pitched')
    L.block('roundhouse-c', 'shed', (250, 160, 330, 200), -7, 6, roof='pitched')
    L.block('carriage-shed', 'shed', (-200, 60, -90, 100), 7, 17, roof='pitched')

    def rakes(L):
        for k, (x, z0, z1) in enumerate(((70, -60, 120), (78, -60, 120), (-50, 50, 125), (-40, 50, 125))):
            L.rake('rake-%d' % k, x - 1.5, z0, x + 1.5, z1, 'NS')
        for k, z in enumerate((-46, -52, -70, -76)):
            L.rake('rake-s-%d' % k, -40, z - 1.5, 40, z + 1.5, 'EW', car=12)
        for k, z in enumerate((-20, 20, 60)):
            L.rake('rake-e-%d' % k, 120, z - 1.5, 195, z + 1.5, 'EW')
        for k, z in enumerate((10, 30, -30)):
            L.rake('rake-h-%d' % k, -170, z - 1.5, -80, z + 1.5, 'EW')
    L.pre_fill = rakes
    L.landmark('gantry', 'The Signal Gantry, a walkway at +7', 0, 100, 7, 'bridge')
    L.landmark('footbridge', 'The covered Goods footbridge', 0, -61, 7, 'bridge')
    L.landmark('shed', 'The Goods Shed', -65, -115, 0, 'shed')
    L.landmark('coaling', 'The coaling tower, 32 m', 158, 38, 25, 'tower')
    L.landmark('roundhouse', 'The Roundhouse', 290, 180, 6, 'shed')
    L.landmark('cut', 'The Main Line Cut and its bridges', 222, 150, -14, 'cut')
    L.landmark('carriage', 'The Carriage Shed', -145, 80, 17, 'shed')

    L.zone('goods-e', (60, -200, 110, 130), 'warehouses', 0.7)
    L.zone('high', (-260, -90, -60, 140), 'sheds', 0.55)
    L.zone('bonded', (-260, -200, -60, -90), 'warehouses', 0.75)
    L.zone('goods-s', (-60, -200, 60, -125), 'warehouses', 0.6)
    L.zone('engine-w', (110, -200, 202, 280), 'sheds', 0.55)
    L.zone('engine-e', (242, -200, 340, 280), 'warehouses', 0.6)
    L.zone('hill', (-260, 140, 40, 280), 'tenements', 0.6)
    L.zone('approach', (-60, 130, 110, 170), 'shops', 0.5)
    L.zone('ne', (40, 170, 110, 280), 'rows', 0.75)
    L.description = (
        "The ground steps down from west to east in broad yards: Station Hill and the Coal Stage at +14, the High "
        "Yard at +7, the Goods Yard at 0 where the camp sits on the Junction Wedge, the Engine Yard at -7 and the "
        "Main Line Cut at -14. Wide flat yards give long views, but every view ends at a drop or a shed, and rakes "
        "of parked wagons are the cover. North, a steel ramp lifts you onto the Signal Gantry, a walkway on legs "
        "over the yard throat, and on up to City Hall on Station Hill. East, an incline runs down beside the camp "
        "and out across the Engine Yard into the Cut, to a sunk tunnel portal. South, a covered footbridge hops "
        "over the wagon rakes, then the Goods Shed takes you under its roof to the Bonded Warehouse, a ground "
        "door. West, a broad haul ramp climbs to the High Yard's sidings and the Coal Stage incline to a raised "
        "hall at +14. The coaling tower and the Roundhouse mark the east.")
    return L
