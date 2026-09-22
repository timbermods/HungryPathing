import json, sys, collections, re, os
def load(p):
    return json.load(open(os.path.abspath(p), encoding='utf-8-sig'))
def need(e, n):
    for x in e['Components'].get('NeedManager', {}).get('Needs', []):
        if x['Name'] == n: return x['Points']
    return None
def stats(label, path):
    w = load(path); ents = w['Entities']; dn = w['Singletons']['DayNightCycle']
    hour = dn['DayProgress'] * 24
    adults = [e for e in ents if e.get('Template') == 'BeaverAdult']
    out = {'label': label, 'day': dn['DayNumber'], 'hour': round(hour, 1), 'adults': len(adults)}
    for n in ['Hunger', 'Thirst']:
        pts = [need(e, n) for e in adults]; pts = [p for p in pts if p is not None]
        out[n] = {'penalty': sum(1 for p in pts if p <= 0), 'under3h': sum(1 for p in pts if 0 < p <= 0.1),
                  'under9h': sum(1 for p in pts if 0.1 < p <= 0.3), 'mean': round(sum(pts)/len(pts), 2)}
    work_tr, off_tr = [], []; starts = collections.Counter(); eating_now = 0
    for e in adults:
        bm = e['Components'].get('BehaviorManager', {})
        if 'InventoryNeedBehavior' in bm.get('RunningBehavior', ''): eating_now += 1
        parsed = []
        for s in bm.get('TimestampedBehaviorLog', []):
            m = re.match(r'(\S+) ([\d.]+)', s)
            if m: parsed.append((m.group(1), float(m.group(2))))
        for i, (name, t) in enumerate(parsed):
            if name != 'InventoryNeedBehavior' or i + 1 >= len(parsed): continue
            hod = (t % 1) * 24; dur = (parsed[i+1][1] - t) * 24
            starts[int(hod // 4) * 4] += 1
            (work_tr if hod < 16 else off_tr).append(dur)
    def q(v):
        if not v: return None
        v = sorted(v); n = len(v)
        return {'n': n, 'median': round(v[n//2], 2), 'p75': round(v[int(n*.75)], 2), 'p90': round(v[min(n-1, int(n*.9))], 2), 'over1h': sum(1 for x in v if x > 1)}
    out['work_trips'] = q(work_tr); out['offduty_trips'] = q(off_tr); out['eating_now'] = eating_now
    out['trip_starts_by_4h'] = dict(sorted(starts.items()))
    return out
for label, path in zip(sys.argv[1::2], sys.argv[2::2]):
    r = stats(label, path)
    print(f"=== {r['label']}: day {r['day']} hour {r['hour']}, {r['adults']} adults, eating right now: {r['eating_now']}")
    for n in ['Hunger', 'Thirst']:
        print(f"  {n}: mean {r[n]['mean']}, in penalty {r[n]['penalty']}, under 3h left {r[n]['under3h']}, 3-9h left {r[n]['under9h']}")
    print(f"  trips started in working hours: {r['work_trips']}")
    print(f"  trips started off duty:         {r['offduty_trips']}")
    print(f"  trip starts by hour-of-day bucket: {r['trip_starts_by_4h']}")
