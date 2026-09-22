import json, sys, os, math, statistics, collections
FOOD = {'AlgaeRation','Berries','Bread','Carrot','CattailCracker','CornRation','EggplantRation','FermentedCassava','FermentedMushroom','FermentedSoybean','GrilledChestnut','GrilledPotato','GrilledSpadderdock','Kohlrabi','MangroveFruit','MaplePastry','SunflowerSeeds'}
def coords(e):
    for k, v in e['Components'].items():
        if isinstance(v, dict):
            c = v.get('Coordinates') or v.get('Position')
            if isinstance(c, dict) and 'X' in c: return (c['X'], c['Z'] if 'Z' in c else c['Y'])
    return None
w = json.load(open(os.path.abspath(sys.argv[1]), encoding='utf-8-sig')); ents = w['Entities']
food, water = [], []
for e in ents:
    inv = e['Components'].get('Inventory:Stockpile')
    if not inv: continue
    goods = {g['Good']: g['Amount'] for g in inv.get('Storage', {}).get('Goods', []) if g['Amount'] > 0}
    c = coords(e)
    if not c: continue
    if set(goods) & FOOD: food.append((c, sum(v for k, v in goods.items() if k in FOOD), e.get('Template')))
    if 'Water' in goods: water.append((c, goods['Water'], e.get('Template')))
print(f"stocked food storages: {len(food)} ({sum(f[1] for f in food)} units), water storages: {len(water)} ({sum(x[1] for x in water)} units)")
print("food storages by template:", dict(collections.Counter(f[2] for f in food)))
adults = [e for e in ents if e.get('Template') == 'BeaverAdult']
rows = []
for e in adults:
    p = coords(e)
    if not p: continue
    df = min(math.dist(p, c) for c, _, _ in food) if food else float('nan')
    dw = min(math.dist(p, c) for c, _, _ in water) if water else float('nan')
    rows.append((p, df, dw))
for name, idx in (('food', 1), ('water', 2)):
    d = sorted(r[idx] for r in rows)
    print(f"adults' distance to nearest stocked {name}: median {statistics.median(d):.0f} tiles, p75 {d[int(len(d)*.75)]:.0f}, p90 {d[int(len(d)*.9)]:.0f}, over 60 tiles: {sum(1 for x in d if x > 60)} of {len(d)}")
# where are the beavers that are far from food right now? 24-tile grid cells
cells = collections.Counter(); far = collections.Counter()
for p, df, dw in rows:
    cell = (int(p[0] // 24) * 24, int(p[1] // 24) * 24)
    cells[cell] += 1
    if df > 60 or dw > 60: far[cell] += 1
print("cells (x,y origin, 24x24 tiles) with the most beavers more than 60 tiles from stocked food or water:")
for cell, n in far.most_common(6):
    print(f"   x {cell[0]:3d}-{cell[0]+24:3d}, y {cell[1]:3d}-{cell[1]+24:3d}: {n} far beavers of {cells[cell]} there")
