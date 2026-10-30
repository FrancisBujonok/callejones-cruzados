# -*- coding: utf-8 -*-
"""
Genera las texturas de los carteles de Callejones Cruzados, con texto de verdad.

Cada cartel es un PNG chiquito (letras de 24 x 24 pixeles, sin suavizado) para que se vea
como en la PS1. El nombre del archivo le dice a Unity como usarlo:

    {formato}_{idioma}_{numero}_{estilo}.png      por ejemplo  V5_tw_03_rojo.png

    formato  V5 = vertical, hasta 5 letras (carteles de pared)
             V2 = vertical, hasta 2 letras (carteles colgantes)
             H5 = horizontal, hasta 5 letras (pancartas)
             P  = afiche (titulo de 2 letras, foto y letra chica)
             T  = placa del torii (fija, no se sortea)
    idioma   tw = chino tradicional (Taiwan), jp = japones, kr = coreano
    estilo   papel, rojo o negro

La herramienta de Unity (Callejones > Fachadas, carteles y lugares) lee la carpeta, arma un
material por cartel y los sortea; los de Taiwan pesan el doble, asi predominan.

Uso (desde la carpeta del proyecto):
    pip install pillow
    python Herramientas/generar_carteles.py

Para agregar o cambiar un cartel: editar la lista CARTELES y volver a correr el script.
Las fuentes se buscan en Windows (JhengHei, Yu Gothic, Malgun Gothic) o en Linux (Noto CJK).
"""
import csv
import os
from PIL import Image, ImageDraw, ImageFont

SALIDA = os.path.join("Assets", "_Project", "Textures", "Carteles")
CELDA = 24          # cada letra mide 24 x 24 pixeles (con 16 no se leian las letras complicadas)
SEP = 2             # separacion y borde

# (formato, idioma, estilo, texto, significado)
CARTELES = [
    # --- Taiwan (chino tradicional): la mayoria
    ("V5", "tw", "rojo",  "牛肉麵",   "Fideos con carne"),
    ("V5", "tw", "papel", "雜貨店",   "Almacen"),
    ("V5", "tw", "papel", "中藥行",   "Herboristeria china"),
    ("V5", "tw", "rojo",  "營業中",   "Abierto"),
    ("V5", "tw", "papel", "禁止停車", "Prohibido estacionar"),
    ("V5", "tw", "papel", "請勿回頭", "Por favor, no mires atras"),
    ("V5", "tw", "rojo",  "此路不通", "Este camino no tiene salida"),
    ("V5", "tw", "rojo",  "九份老街", "Calle vieja de Jiufen"),
    ("V2", "tw", "rojo",  "涼茶",     "Te de hierbas"),
    ("V2", "tw", "papel", "當舖",     "Casa de empeno"),
    ("V2", "tw", "papel", "旅社",     "Pension"),
    ("V2", "tw", "rojo",  "理髮",     "Peluqueria"),
    ("V2", "tw", "rojo",  "平安",     "Paz (que estes a salvo)"),
    ("V2", "tw", "papel", "出租",     "Se alquila"),
    ("V2", "tw", "rojo",  "出口",     "Salida"),
    ("H5", "tw", "rojo",  "歡迎光臨", "Bienvenidos"),
    ("H5", "tw", "rojo",  "中元普渡", "Fiesta de los fantasmas (mes de los espiritus)"),
    ("H5", "tw", "papel", "老街夜市", "Mercado nocturno de la calle vieja"),
    ("P",  "tw", "papel", "尋人",     "Se busca a una persona"),
    ("P",  "tw", "papel", "失蹤",     "Desaparecido"),
    ("T",  "tw", "negro", "歸途",     "El camino de vuelta (placa del torii)"),
    # --- Japon
    ("V5", "jp", "rojo",  "ラーメン", "Ramen"),
    ("V5", "jp", "papel", "居酒屋",   "Taberna"),
    ("V5", "jp", "papel", "振り返るな", "No te des vuelta"),
    ("V2", "jp", "papel", "薬局",     "Farmacia"),
    ("V2", "jp", "rojo",  "喫茶",     "Cafe"),
    ("H5", "jp", "papel", "迷子に注意", "Cuidado, es facil perderse"),
    ("P",  "jp", "papel", "迷子",     "Persona perdida"),
    # --- Corea
    ("V5", "kr", "rojo",  "노래방",   "Karaoke"),
    ("V5", "kr", "papel", "영업중",   "Abierto"),
    ("V2", "kr", "rojo",  "식당",     "Comedor"),
    ("V2", "kr", "papel", "출구",     "Salida"),
    ("H5", "kr", "rojo",  "돌아가세요", "Por favor, volve"),
    ("P",  "kr", "papel", "실종",     "Desaparecido"),
]

ESTILOS = {  # (fondo, tinta)
    "papel": ((232, 214, 176), (42, 20, 16)),
    "rojo":  ((184, 40, 26), (242, 226, 194)),
    "negro": ((20, 12, 10), (217, 180, 90)),
}

FUENTES = {
    "tw": [(r"C:\Windows\Fonts\msjhbd.ttc", 0), (r"C:\Windows\Fonts\msjh.ttc", 0),
           ("/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc", 3)],
    "jp": [(r"C:\Windows\Fonts\YuGothB.ttc", 0), (r"C:\Windows\Fonts\meiryob.ttc", 0),
           ("/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc", 0)],
    "kr": [(r"C:\Windows\Fonts\malgunbd.ttf", 0), (r"C:\Windows\Fonts\malgun.ttf", 0),
           ("/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc", 1)],
}


GRANDE = 4  # se dibuja 4 veces mas grande y despues se achica: asi no se pierden los trazos finos
UMBRAL = 140  # de 0 a 255: cuanto tiene que estar pintado un pixel chico para quedar con tinta


def fuente(idioma):
    for ruta, indice in FUENTES[idioma]:
        if os.path.exists(ruta):
            return ImageFont.truetype(ruta, CELDA * GRANDE, index=indice)
    raise SystemExit("No encuentro una fuente para '%s'. Revisa FUENTES." % idioma)


def letra(ch, f, vertical):
    """
    Una letra en una mascara de 24 x 24 con pixeles duros (sin suavizado).
    Dibujarla directo en tamano chico borra trazos finos (en coreano, 돌 quedaba como 들). Por eso
    se dibuja 4 veces mas grande, se achica promediando y cada pixel queda con tinta si supera
    el UMBRAL.
    """
    big = CELDA * GRANDE
    img = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = d.textbbox((0, 0), ch, font=f)
    d.text(((big - (x1 - x0)) // 2 - x0, (big - (y1 - y0)) // 2 - y0), ch, font=f, fill=255)
    # En vertical, la raya de las vocales largas japonesas se escribe parada.
    if vertical and ch == "ー":
        img = img.rotate(90)
    small = img.resize((CELDA, CELDA), Image.BOX)
    return small.point(lambda v: 255 if v >= UMBRAL else 0)


def lienzo(ancho, alto, estilo):
    fondo, tinta = ESTILOS[estilo]
    img = Image.new("RGB", (ancho, alto), fondo)
    ImageDraw.Draw(img).rectangle([0, 0, ancho - 1, alto - 1], outline=tinta)
    return img, tinta


def cartel_lineal(texto, f, estilo, lugares, vertical):
    """Letras en fila (vertical u horizontal), repartidas parejas en el espacio de 'lugares'."""
    largo = SEP + lugares * (CELDA + SEP)
    ancho, alto = (CELDA + 2 * SEP, largo) if vertical else (largo, CELDA + 2 * SEP)
    img, tinta = lienzo(ancho, alto, estilo)
    n = len(texto)
    hueco = (largo - n * CELDA) / (n + 1)
    for i, ch in enumerate(texto):
        p = int(round(hueco + i * (CELDA + hueco)))
        pos = (SEP, p) if vertical else (p, SEP)
        img.paste(tinta, pos, letra(ch, f, vertical))
    return img


def afiche(texto, f, estilo):
    """Afiche de persona buscada: titulo de 2 letras, foto oscura con una silueta y letra chica."""
    c = CELDA
    ancho, alto = 2 * c + 3 * SEP, 3 * c
    img, tinta = lienzo(ancho, alto, estilo)
    d = ImageDraw.Draw(img)
    for i, ch in enumerate(texto[:2]):
        img.paste(tinta, (SEP + i * (c + SEP), SEP), letra(ch, f, False))
    top = c + 2 * SEP + 2
    foto = [4, top, 4 + c - 4, top + c + 2]
    d.rectangle(foto, fill=(60, 55, 50))                                       # foto
    cx = (foto[0] + foto[2]) // 2
    d.ellipse([cx - 4, top + 4, cx + 4, top + 12], fill=(150, 140, 125))        # cabeza
    d.rectangle([cx - 6, top + 13, cx + 6, foto[3]], fill=(150, 140, 125))     # hombros
    for y in range(top + 1, foto[3] + 1, 4):                                   # letra chica
        d.line([foto[2] + 4, y, ancho - 5, y], fill=tinta)
    for y in range(foto[3] + 4, alto - 3, 4):
        d.line([4, y, ancho - 5, y], fill=tinta)
    d.rectangle([ancho - 15, alto - 13, ancho - 5, alto - 4], outline=(160, 40, 30))  # sello rojo
    return img


def main():
    os.makedirs(SALIDA, exist_ok=True)
    for viejo in os.listdir(SALIDA):
        if viejo.endswith(".png"):
            os.remove(os.path.join(SALIDA, viejo))

    contador = {}
    filas = []
    for formato, idioma, estilo, texto, significado in CARTELES:
        f = fuente(idioma)
        if formato == "V5":
            img = cartel_lineal(texto, f, estilo, 5, True)
        elif formato in ("V2", "T"):
            img = cartel_lineal(texto, f, estilo, 2, True)
        elif formato == "H5":
            img = cartel_lineal(texto, f, estilo, 5, False)
        else:
            img = afiche(texto, f, estilo)
        clave = (formato, idioma)
        contador[clave] = contador.get(clave, 0) + 1
        nombre = "%s_%s_%02d_%s.png" % (formato, idioma, contador[clave], estilo)
        img.save(os.path.join(SALIDA, nombre))
        filas.append((nombre, formato, idioma, texto, significado))

    with open(os.path.join("Herramientas", "carteles.csv"), "w", newline="", encoding="utf-8") as out:
        w = csv.writer(out)
        w.writerow(["archivo", "formato", "idioma", "texto", "significado"])
        w.writerows(filas)
    print("%d carteles en %s" % (len(filas), SALIDA))


if __name__ == "__main__":
    main()
