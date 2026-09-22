import json, sys, os, math, statistics
FOOD = {'AlgaeRation','Berries','Bread','Carrot','CattailCracker','CornRation','EggplantRation','FermentedCassava','FermentedMushroom','FermentedSoybean','GrilledChestnut','GrilledPotato','GrilledSpadderdock','Kohlrabi','MangroveFruit','MaplePastry','SunflowerSeeds'}
def coords(e):
    for k, v in e['Components'].items():
        if isinstance(v, dict):
            c = v.get('Coordinates') or v.get('Position')
            if isinstance(c, dict) and 'X' in c: return (c['X'], c['Z'] if 'Z' in c else c['Y'])
    return None
def run(label, path):
    w = json.load(open(os.path.abspath(path), encoding='utf-8-sig')); ents = w['Entities']
    byid = {e['Id']: e for e in ents}
    stores = []
    for e in ents:
        inv = e['Components'].get('Inventory:Stockpile')
        if not inv: continue
        goods = {g['Good']: g['Amount'] for g in inv.get('Storage', {}).get('Goods', []) if g['Amount'] > 0}
        c = coords(e)
        if c: stores.append((e['Id'], c, goods))
    print(f"=== {label}: {len(stores)} stockpiles with stock; food in {sum(1 for s in stores if set(s[2]) & FOOD)}, water in {sum(1 for s in stores if 'Water' in s[2])}")
    rows = []
    for e in ents:
        if e.get('Template') != 'BeaverAdult': continue
        bm = e['Components'].get('BehaviorManager', {})
        if 'InventoryNeedBehavior' not in bm.get('RunningBehavior', ''): continue
        res = e['Components'].get('GoodReserver', {}).get('StockReservation')
        pos = coords(e)
        if not res or not pos: continue
        good = res['GoodAmount']['Good']; target = res['Inventory'].split(':')[0]
        te = byid.get(target); tc = coords(te) if te else None
        if not tc: continue
        d_target = math.dist(pos, tc)
        want = (lambda g: bool(set(g) & FOOD)) if good in FOOD else (lambda g: 'Water' in g)
        options = [(math.dist(pos, c), sid) for sid, c, goods in stores if want(goods)]
        d_near = min(options)[0] if options else float('nan')
        rows.append((good, d_target, d_near))
    if not rows: print('  nobody eating'); return
    dt = [r[1] for r in rows]; extra = [r[1] - r[2] for r in rows]
    print(f"  beavers on an eating trip: {len(rows)}; straight-line tiles to their storage: median {statistics.median(dt):.0f}, max {max(dt):.0f}")
    print(f"  going to the nearest stocked storage (within 5 tiles of it): {sum(1 for x in extra if x <= 5)} of {len(rows)}; passing a closer one by more than 20 tiles: {sum(1 for x in extra if x > 20)}")
    for good, a, b in sorted(rows, key=lambda r: -(r[1]-r[2]))[:6]:
        print(f"    {good:16s} to storage {a:5.0f} tiles away, nearest with the same kind {b:5.0f}")
for label, path in zip(sys.argv[1::2], sys.argv[2::2]): run(label, path)
