"""Makes the Hungry Pathing site's surfaces: the enamel sign plate (cream vitreous enamel in a cobalt border, chipped to iron at the corners, used as a nine-slice),
and the works-yard ground by day and night. Procedural (numpy and Pillow, fixed seeds); no source images and no
generative model. Run from this folder: python make_textures.py"""
import numpy as np
from PIL import Image, ImageFilter

def wrap_noise(rng, n, cell):
    g = rng.normal(0, 1, (n // cell, n // cell)); g = (g - g.min()) / (g.max() - g.min())
    big = np.tile(g, (3, 3))
    up = np.asarray(Image.fromarray((big * 255).astype(np.uint8)).resize((n * 3, n * 3), Image.BICUBIC), float) / 255
    return up[n:2 * n, n:2 * n] - .5

def save(img, name, **kw):
    img.save(name, **kw); print("made", name)

# No slate texture: the slate is a flat colour (#23292d). Its chalk smears read as smoke (Kyler, 2026-09-24).

# ---- enamel plate, 96x96 nine-slice (slice 24): cream field with a faint speckle, a cobalt border with a thin cream
# inner line, and dark iron showing through small chips at the corners
def enamel_plate(field_rgb, keyline_lift, name):
    rng = np.random.default_rng(4202); S = 96
    img = np.zeros((S, S, 4), float)
    yy, xx = np.mgrid[0:S, 0:S].astype(float)
    edge = np.minimum.reduce([xx, yy, S - 1 - xx, S - 1 - yy])
    field = np.array(field_rgb, float) + rng.normal(0, 2.2, (S, S, 1)) + wrap_noise(rng, S, 8)[..., None] * 6
    cobalt = np.array([29, 74, 134], float) + rng.normal(0, 3, (S, S, 1))
    img[..., :3] = np.where((edge < 9)[..., None], cobalt, field)
    img[..., :3] = np.where(((edge >= 11) & (edge < 12.5))[..., None], cobalt * .9 + keyline_lift, img[..., :3])   # the inner keyline
    img[..., 3] = 255
    iron = np.array([46, 40, 36], float)
    for cx, cy in ((3, 5), (S - 6, 3), (4, S - 5), (S - 4, S - 7), (14, 2), (S - 2, 18)):
        r = rng.uniform(2.2, 4.2)
        d = np.sqrt((xx - cx) ** 2 + ((yy - cy) * rng.uniform(.8, 1.3)) ** 2)
        chip = d < r + wrap_noise(rng, S, 4) * 2
        img[..., :3] = np.where(chip[..., None], iron + rng.normal(0, 6, (S, S, 1)), img[..., :3])
    # the plate's corners are rounded off, as pressed enamel is
    corner = 5
    for cx, cy in ((corner, corner), (S - 1 - corner, corner), (corner, S - 1 - corner), (S - 1 - corner, S - 1 - corner)):
        q = ((xx - cx) * np.sign(cx - S / 2) > 0) & ((yy - cy) * np.sign(cy - S / 2) > 0)
        img[..., 3] = np.where(q & (np.hypot(xx - cx, yy - cy) > corner + .5), 0, img[..., 3])
    save(Image.fromarray(img.clip(0, 255).astype(np.uint8), "RGBA"), name, optimize=True)

enamel_plate([239, 232, 214], 20, "enamel.png")          # day: cream enamel
enamel_plate([30, 38, 52], 60, "enamel-night.png")       # night: dark enamel, a lighter keyline (Kyler, 2026-09-24)

# ---- the works yard: sawdust-grey by day, a sooty timber shed by night. Short chips of wood at random angles, drawn
# antialiased at four times the size and wrapped at the edges so the tile repeats; low contrast, so text sits on it easily
from PIL import ImageDraw
for name, seed, base, tones, amp in (("yard-day.webp", 4203, (227, 221, 208), ((214, 204, 184), (236, 231, 220)), 5),
                                     ("yard-night.webp", 4204, (25, 27, 29), ((19, 20, 22), (33, 33, 32)), 3)):
    rng = np.random.default_rng(seed); N, K = 512, 4
    v = wrap_noise(rng, N, 64) * amp * 1.6 + rng.normal(0, amp * .18, (N, N))
    img = Image.fromarray((np.array(base, float) + v[..., None]).clip(0, 255).astype(np.uint8)).resize((N * K, N * K), Image.BILINEAR)
    d = ImageDraw.Draw(img)
    for _ in range(2600):
        x, y = rng.uniform(0, N * K, 2); a = rng.uniform(0, np.pi); ln = rng.uniform(2.5, 7) * K; w = int(rng.uniform(.8, 1.6) * K)
        col = tones[int(rng.random() < .5)]
        dx, dy = np.cos(a) * ln / 2, np.sin(a) * ln / 2
        for ox in (-N * K, 0, N * K):
            for oy in (-N * K, 0, N * K):
                d.line([(x - dx + ox, y - dy + oy), (x + dx + ox, y + dy + oy)], fill=col, width=w)
    img = img.resize((N, N), Image.LANCZOS)
    save(img, name, quality=88, method=6)
