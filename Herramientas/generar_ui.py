# -*- coding: utf-8 -*-
"""
Genera los graficos chiquitos de los menus de Callejones Cruzados (Paso 17).

Todo sale en BLANCO sobre transparente: el color lo pone Unity (Image.color), asi el
mismo dibujo sirve en papel, en rojo o en negro sin tener que regenerarlo.

    UI_Farol.png            farolito que marca la opcion elegida
    UI_MiJin_V.png          迷津 en vertical (titulo del inicio)
    UI_ShiZong.png          失蹤 "desaparecido" (afiche de la pausa)
    UI_GuiTu.png            歸途 "el camino de vuelta" (boleto del final)
    UI_GuiTu_V.png          歸途 en vertical y chico (tiras para arrancar del afiche)
    UI_SheDing.png          設定 "opciones"
    UI_Segmento_Lleno.png   un casillero de las barras de opciones (se repite)
    UI_Segmento_Vacio.png   un casillero vacio
    UI_Punto.png            puntitos que unen el dato con su valor en el boleto
    UI_Guion_H.png          linea cortada horizontal (se repite)
    UI_Guion_V.png          linea cortada vertical
    UI_Circulo.png          circulo (las muescas del boleto)
    UI_Vineta.png           bordes oscuros de la ventana del inicio
    UI_CodigoBarras.png     codigo de barras del boleto

Las letras se dibujan 4 veces mas grandes y se achican con promedio (como los carteles):
asi los trazos finos no desaparecen. Despues se pasan a blanco o transparente, sin grises.

Uso (desde la carpeta del proyecto):
    pip install pillow
    python Herramientas/generar_ui.py
"""
import os
import random
from PIL import Image, ImageDraw, ImageFont

SALIDA = os.path.join("Assets", "_Project", "Textures", "UI")
BLANCO = (255, 255, 255, 255)
NADA = (0, 0, 0, 0)

# Fuentes serif con chino tradicional: Linux (Noto Serif CJK) o Windows (MingLiU).
FUENTES = [
    ("/usr/share/fonts/opentype/noto/NotoSerifCJK-Bold.ttc", "TC"),
    ("C:/Windows/Fonts/mingliub.ttc", None),
    ("C:/Windows/Fonts/mingliu.ttc", None),
    ("C:/Windows/Fonts/msjhbd.ttc", None),
]


def fuente(tam):
    for ruta, cara in FUENTES:
        if not os.path.exists(ruta):
            continue
        if cara is None:
            return ImageFont.truetype(ruta, tam)
        # En un .ttc hay varias caras: se busca la de chino tradicional.
        for i in range(10):
            try:
                f = ImageFont.truetype(ruta, tam, index=i)
            except OSError:
                break
            if cara in " ".join(f.getname()):
                return f
    raise SystemExit("No encuentro una fuente con chino tradicional.")


def letra(ch, tam):
    """Una letra de tam x tam pixeles, blanca o transparente."""
    grande = Image.new("L", (tam * 4, tam * 4), 0)
    d = ImageDraw.Draw(grande)
    f = fuente(int(tam * 4 * 0.92))
    caja = d.textbbox((0, 0), ch, font=f)
    x = (tam * 4 - (caja[2] - caja[0])) / 2 - caja[0]
    y = (tam * 4 - (caja[3] - caja[1])) / 2 - caja[1]
    d.text((x, y), ch, fill=255, font=f)
    chica = grande.resize((tam, tam), Image.BOX)
    return chica.point(lambda v: 255 if v > 140 else 0)


def texto(txt, tam, vertical=False, sep=2):
    n = len(txt)
    largo = tam * n + sep * (n - 1)
    im = Image.new("RGBA", (tam, largo) if vertical else (largo, tam), NADA)
    for i, ch in enumerate(txt):
        pos = (0, i * (tam + sep)) if vertical else (i * (tam + sep), 0)
        im.paste(Image.new("RGBA", (tam, tam), BLANCO), pos, letra(ch, tam))
    return im


def farol():
    """El mismo farolito de los mockups, en sus colores (este no se tine)."""
    def c(h):
        h = h.lstrip("#")
        return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)
    im = Image.new("RGBA", (16, 22), NADA)
    d = ImageDraw.Draw(im)
    tapa, cuerpo, costilla, centro = c("#2A1208"), c("#FF6A35"), c("#B8401A"), c("#FFC07A")
    d.line([(7, 0), (8, 0)], fill=tapa)
    d.rectangle([5, 1, 10, 2], fill=tapa)
    anchos = [6, 8, 10, 12, 12, 12, 12, 12, 12, 12, 12, 10, 8, 6]
    for i, w in enumerate(anchos):
        d.line([(8 - w // 2, 3 + i), (8 - w // 2 + w - 1, 3 + i)], fill=cuerpo)
    for y in range(6, 14):
        d.line([(6, y), (9, y)], fill=centro)
    for y in (6, 10, 14):
        w = anchos[y - 3]
        d.line([(8 - w // 2, y), (8 - w // 2 + w - 1, y)], fill=costilla)
    d.rectangle([5, 17, 10, 18], fill=tapa)
    d.line([(8, 19), (8, 21)], fill=c("#B3261E"))
    return im


def segmento(lleno):
    """7 x 12 de casillero + 2 de espacio: al repetirse quedan separados."""
    im = Image.new("RGBA", (9, 12), NADA)
    d = ImageDraw.Draw(im)
    if lleno:
        d.rectangle([0, 0, 6, 11], fill=BLANCO)
    else:
        d.rectangle([0, 0, 6, 11], outline=BLANCO, width=2)
    return im


def punto():
    im = Image.new("RGBA", (4, 2), NADA)
    ImageDraw.Draw(im).rectangle([0, 0, 1, 1], fill=BLANCO)
    return im


def guion(horizontal):
    im = Image.new("RGBA", (6, 2) if horizontal else (2, 6), NADA)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 2, 1] if horizontal else [0, 0, 1, 2], fill=BLANCO)
    return im


def circulo():
    grande = Image.new("L", (128, 128), 0)
    ImageDraw.Draw(grande).ellipse([2, 2, 125, 125], fill=255)
    chico = grande.resize((32, 32), Image.BOX).point(lambda v: 255 if v > 127 else 0)
    im = Image.new("RGBA", (32, 32), NADA)
    im.paste(Image.new("RGBA", (32, 32), BLANCO), (0, 0), chico)
    return im


def vineta():
    """Transparente en el centro y opaco en los bordes (en escalones, como la PS1)."""
    n = 64
    im = Image.new("RGBA", (n, n), NADA)
    for y in range(n):
        for x in range(n):
            dx = (x + 0.5) / n * 2 - 1
            dy = (y + 0.5) / n * 2 - 1
            r = (dx * dx * 0.9 + dy * dy) ** 0.5
            a = max(0.0, min(1.0, (r - 0.55) / 0.6))
            a = round(a * a * 6) / 6
            im.putpixel((x, y), (255, 255, 255, int(a * 255)))
    return im


def codigo_barras():
    rnd = random.Random(127)
    im = Image.new("RGBA", (99, 17), NADA)
    d = ImageDraw.Draw(im)
    x = 0
    while x < 99:
        w = rnd.choice([1, 1, 1, 2, 2, 3])
        d.rectangle([x, 0, min(98, x + w - 1), 16], fill=BLANCO)
        x += w + rnd.choice([1, 1, 2, 2, 3])
    return im


def main():
    os.makedirs(SALIDA, exist_ok=True)
    archivos = {
        "UI_Farol.png": farol(),
        "UI_MiJin_V.png": texto("迷津", 32, vertical=True),
        "UI_ShiZong.png": texto("失蹤", 30),
        "UI_GuiTu.png": texto("歸途", 24),
        "UI_GuiTu_V.png": texto("歸途", 16, vertical=True, sep=1),
        "UI_SheDing.png": texto("設定", 20),
        "UI_Segmento_Lleno.png": segmento(True),
        "UI_Segmento_Vacio.png": segmento(False),
        "UI_Punto.png": punto(),
        "UI_Guion_H.png": guion(True),
        "UI_Guion_V.png": guion(False),
        "UI_Circulo.png": circulo(),
        "UI_Vineta.png": vineta(),
        "UI_CodigoBarras.png": codigo_barras(),
    }
    for nombre, im in archivos.items():
        im.save(os.path.join(SALIDA, nombre))
        print(nombre, im.size)


if __name__ == "__main__":
    main()
