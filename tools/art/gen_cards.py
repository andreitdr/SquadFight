"""Finished hero cards (600x800) composed from the generated layers: back, bust, frame, rarity gem, class icon, name, stars.

The game builds the same card live from the layers (localised name, real star count); these baked PNGs are for the
collection showcase, store screenshots and marketing.
"""
import csv
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

from paths import CARD_DIR, CHAR_DIR, FONT, ICON_DIR, ROOT, UI_DIR

STARS = {'Common': 1, 'Uncommon': 2, 'Rare': 3, 'Epic': 4, 'Legendary': 5}


def heroes():
    with open(os.path.join(ROOT, 'Assets/_Game/Content/Source/heroes.csv')) as f:
        rows = [r for r in csv.reader(l for l in f if not l.startswith('#'))]
    head = rows[0]
    return [dict(zip(head, r)) for r in rows[1:] if r]


def text_outlined(img, xy, txt, size, fill, anchor='mm', stroke=6):
    font = ImageFont.truetype(FONT, size)
    d = ImageDraw.Draw(img)
    while d.textlength(txt, font=font) > 500 and size > 30:
        size -= 2
        font = ImageFont.truetype(FONT, size)
    d.text(xy, txt, font=font, fill=fill, anchor=anchor, stroke_width=stroke, stroke_fill=(20, 12, 24, 255))


def card(h):
    r = h['rarity'].lower()
    img = Image.open(os.path.join(UI_DIR, 'card_back_%s.png' % r)).convert('RGBA')
    bust = Image.open(os.path.join(CHAR_DIR, h['id'], 'card_bust.png')).convert('RGBA').resize((560, 747), Image.LANCZOS)
    glow = bust.split()[3].filter(ImageFilter.GaussianBlur(14))
    halo = Image.new('RGBA', bust.size, (255, 255, 255, 0))
    halo.putalpha(glow.point(lambda v: int(v * 0.45)))
    img.alpha_composite(halo, (20, 30))
    img.alpha_composite(bust, (20, 30))
    img.alpha_composite(Image.open(os.path.join(UI_DIR, 'card_frame_%s.png' % r)).convert('RGBA'))
    img.alpha_composite(Image.open(os.path.join(UI_DIR, 'gem_%s.png' % r)).convert('RGBA').resize((92, 92), Image.LANCZOS), (46, 52))
    img.alpha_composite(Image.open(os.path.join(ICON_DIR, 'class_%s.png' % h['class'].lower())).convert('RGBA').resize((92, 92), Image.LANCZOS), (462, 52))
    text_outlined(img, (300, 650), h['name'], 54, (255, 246, 220, 255))
    star = Image.open(os.path.join(UI_DIR, 'star_full.png')).convert('RGBA').resize((52, 52), Image.LANCZOS)
    n = STARS[h['rarity']]
    x0 = 300 - n * 27
    for i in range(n):
        img.alpha_composite(star, (x0 + i * 54, 556))
    text_outlined(img, (300, 728), h['rarity'].upper() + ' · ' + h['class'].upper(), 26, (230, 230, 240, 255), stroke=4)
    return img


def main():
    os.makedirs(CARD_DIR, exist_ok=True)
    hs = heroes()
    cards = []
    for h in hs:
        c = card(h)
        c.save(os.path.join(CARD_DIR, h['id'] + '_card.png'), optimize=True)
        cards.append(c)
    # collection sheet (5 per row) for quick review / store assets
    cols = 5
    rows = (len(cards) + cols - 1) // cols
    sheet = Image.new('RGBA', (cols * 300 + 20, rows * 400 + 20), (24, 26, 36, 255))
    for i, c in enumerate(cards):
        sheet.alpha_composite(c.resize((290, 387), Image.LANCZOS), (10 + (i % cols) * 300, 10 + (i // cols) * 400))
    sheet.convert('RGB').save(os.path.join(CARD_DIR, 'collection_sheet.jpg'), quality=90)


if __name__ == '__main__':
    main()
