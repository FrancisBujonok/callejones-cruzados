# Paso 32: texturas con letras de la feria (capitulo 1).
#   Feria_Arco.png       placa del arco de la entrada: 燈會 ("feria de los faroles"), dorado sobre rojo.
#   Feria_Escenario.png  telon del escenario: 祭 ("fiesta") en rojo sobre papel, con un sol.
# Uso: python generar_feria.py   (escribe en ../Textures/Feria)
# Necesita Pillow y una fuente con kanji (en Windows usa Microsoft JhengHei; si no, Noto CJK).
import os
from PIL import Image, ImageDraw, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__))
SALIDA = os.path.join(AQUI, "..", "Textures", "Feria")
FUENTES = [(r"C:\Windows\Fonts\msjhbd.ttc", 0), ("/usr/share/fonts/opentype/noto/NotoSerifCJK-Bold.ttc", 3),
           ("/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc", 3)]


def fuente(tam):
    for ruta, i in FUENTES:
        if os.path.exists(ruta):
            return ImageFont.truetype(ruta, tam, index=i)
    raise SystemExit("No encuentro una fuente con kanji")


def centrado(d, caja, texto, f, color):
    x0, y0, x1, y1 = caja
    b = d.textbbox((0, 0), texto, font=f)
    w, h = b[2] - b[0], b[3] - b[1]
    d.text((x0 + (x1 - x0 - w) / 2 - b[0], y0 + (y1 - y0 - h) / 2 - b[1]), texto, font=f, fill=color)


def arco():
    img = Image.new("RGB", (128, 40), (120, 22, 16))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, 127, 39), outline=(40, 10, 8), width=2)
    d.rectangle((3, 3, 124, 36), outline=(210, 160, 60), width=1)
    centrado(d, (6, 2, 122, 38), "燈 會", fuente(30), (242, 193, 78))
    img.save(os.path.join(SALIDA, "Feria_Arco.png"))


def escenario():
    img = Image.new("RGB", (96, 64), (226, 208, 170))
    d = ImageDraw.Draw(img)
    d.ellipse((58, 6, 90, 38), fill=(196, 52, 34))
    for y in range(48, 64, 4):
        d.line((0, y, 95, y), fill=(180, 40, 28), width=2)
    centrado(d, (4, 2, 60, 50), "祭", fuente(44), (40, 16, 12))
    d.rectangle((0, 0, 95, 63), outline=(60, 30, 20), width=2)
    img.save(os.path.join(SALIDA, "Feria_Escenario.png"))


if __name__ == "__main__":
    os.makedirs(SALIDA, exist_ok=True)
    arco()
    escenario()
    print("Listo:", SALIDA)
