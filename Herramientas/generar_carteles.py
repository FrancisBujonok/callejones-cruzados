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
             S  = cartel saliente, hasta 4 letras: caja de luz o neon (Paso 18)
             N  = neon horizontal de letras sueltas, hasta 5 (fondo transparente)
             D  = placa de direccion azul (calle y numero, en dos renglones)
             F  = flecha de salida verde (出口 con una flecha que no siempre dice la verdad)
             A  = papelitos pegados en la pared (fondo transparente)
    idioma   tw = chino tradicional (Taiwan), jp = japones, kr = coreano
    estilo   papel, rojo o negro; caja-rojo, caja-azul, caja-verde; neon-rosa, neon-cian,
             neon-verde, neon-ambar, neon-rojo; placa-azul; flecha-der, flecha-izq, flecha-arr; pegado
    En D y A el texto lleva "|" para separar renglones o papelitos.

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
    # --- Paso 18: carteles salientes (cajas de luz y neones), al final para no cambiar los nombres de arriba
    ("S", "tw", "caja-rojo",  "中醫診所", "Clinica de medicina china"),
    ("S", "tw", "caja-azul",  "牙科",     "Dentista"),
    ("S", "tw", "caja-verde", "自助餐",   "Comida por peso"),
    ("S", "tw", "caja-rojo",  "旅館",     "Hotel"),
    ("S", "tw", "caja-azul",  "早餐店",   "Desayunos"),
    ("S", "tw", "neon-rosa",  "按摩",     "Masajes"),
    ("S", "tw", "neon-cian",  "網咖",     "Cibercafe"),
    ("S", "tw", "neon-verde", "當舖",     "Casa de empeno"),
    ("S", "tw", "neon-ambar", "歌廳",     "Salon de canto"),
    ("S", "jp", "caja-rojo",  "スナック", "Bar snack"),
    ("S", "jp", "neon-rosa",  "カラオケ", "Karaoke"),
    ("S", "kr", "caja-azul",  "약국",     "Farmacia"),
    ("S", "kr", "neon-cian",  "호프",     "Cerveceria"),
    # --- neones de letras sueltas (arriba de los techos y en arcos)
    ("N", "tw", "neon-rosa",  "不夜城",   "La ciudad que no duerme"),
    ("N", "tw", "neon-cian",  "夜市",     "Mercado nocturno"),
    ("N", "tw", "neon-ambar", "大吉",     "Gran suerte"),
    ("N", "tw", "neon-rojo",  "迷津",     "El vado donde uno pierde el camino"),
    ("N", "jp", "neon-verde", "パチンコ", "Pachinko"),
    ("N", "kr", "neon-rosa",  "맥주",     "Cerveza"),
    # --- placas de direccion (Taiwan las tiene en cada puerta)
    ("D", "tw", "placa-azul", "長安街|17巷3號", "Calle Chang'an, pasaje 17, numero 3"),
    ("D", "tw", "placa-azul", "民生路|5巷12號", "Calle Minsheng, pasaje 5, numero 12"),
    ("D", "tw", "placa-azul", "永和街|9號",     "Calle Yonghe, numero 9"),
    ("D", "tw", "placa-azul", "迷津巷|4號",     "Pasaje Mijin, numero 4"),
    ("D", "tw", "placa-azul", "福德街|2弄7號",  "Calle Fude, callejon 2, numero 7"),
    ("D", "tw", "placa-azul", "和平巷|13號",    "Pasaje Heping, numero 13"),
    ("D", "jp", "placa-azul", "迷町|3丁目",     "Barrio Mayoi, distrito 3"),
    ("D", "kr", "placa-azul", "미로길|12",      "Calle del laberinto 12"),
    # --- flechas de salida
    ("F", "tw", "flecha-der", "出口", "Salida (flecha a la derecha)"),
    ("F", "tw", "flecha-izq", "出口", "Salida (flecha a la izquierda)"),
    ("F", "tw", "flecha-arr", "出口", "Salida (flecha hacia adelante)"),
    ("F", "jp", "flecha-der", "出口", "Salida (flecha a la derecha)"),
    ("F", "kr", "flecha-izq", "출구", "Salida (flecha a la izquierda)"),
    # --- papelitos pegados: destapaciones, prestamos, mudanzas, cerrajero...
    ("A", "tw", "pegado", "通水管|借款|搬家",   "Destapaciones, prestamos, mudanzas"),
    ("A", "tw", "pegado", "開鎖|收購|徵人",     "Cerrajero, compramos, buscamos personal"),
    ("A", "tw", "pegado", "借款|通水管|開鎖|租", "Prestamos, destapaciones, cerrajero, se alquila"),
    ("A", "tw", "pegado", "尋貓|搬家",          "Se busca gato, mudanzas"),
    ("A", "jp", "pegado", "求人|鍵",            "Buscamos personal, llaves"),
    ("A", "kr", "pegado", "대출|이사",          "Prestamos, mudanzas"),
    # --- un afiche mas: se busca gato (斑斑 = "Manchas")
    ("P",  "tw", "papel", "尋貓",     "Se busca gato"),
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


# Colores de los estilos nuevos (Paso 18).
CAJAS = {"rojo": (192, 40, 28), "azul": (31, 78, 154), "verde": (30, 107, 58)}
NEONES = {"rosa": (255, 79, 163), "cian": (71, 230, 255), "verde": (140, 255, 106),
          "ambar": (255, 179, 71), "rojo": (255, 59, 47)}
PLACA_AZUL = (28, 72, 148)
FLECHA_VERDE = (24, 118, 64)
BLANCO = (246, 244, 236)

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


def letra_chica(ch, f, lado, umbral=110):
    """Como letra(), pero de 'lado' pixeles (para placas y papelitos). Con menos umbral, mas gruesa."""
    big = CELDA * GRANDE
    img = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = d.textbbox((0, 0), ch, font=f)
    d.text(((big - (x1 - x0)) // 2 - x0, (big - (y1 - y0)) // 2 - y0), ch, font=f, fill=255)
    small = img.resize((lado, lado), Image.BOX)
    return small.point(lambda v: 255 if v >= umbral else 0)


def ancho_letra(ch, lado):
    """Numeros y letras latinas ocupan la mitad de ancho."""
    return lado // 2 + 1 if ch.isascii() else lado


def renglon(img, texto, f, lado, y, color, umbral=110):
    """Escribe un renglon centrado; numeros a medio ancho."""
    total = sum(ancho_letra(ch, lado) for ch in texto)
    x = (img.width - total) // 2
    for ch in texto:
        w = ancho_letra(ch, lado)
        m = letra_chica(ch, f, lado, umbral)
        if w < lado:
            m = m.crop(((lado - w) // 2, 0, (lado - w) // 2 + w, lado))
        img.paste(color, (x, y), m)
        x += w


def tubo(mascara, color):
    """
    Una letra de neon a partir de su mascara: un brillo alrededor (mas oscuro), el tubo del color
    y el centro casi blanco. Devuelve una imagen RGBA con el fondo transparente.
    """
    from PIL import ImageFilter, ImageChops
    brillo = mascara.filter(ImageFilter.MaxFilter(3))
    centro = mascara.filter(ImageFilter.MinFilter(3))
    capa = Image.new("RGBA", mascara.size, (0, 0, 0, 0))
    oscuro = tuple(int(c * 0.42) for c in color) + (255,)
    claro = tuple(int(c + (255 - c) * 0.6) for c in color) + (255,)
    capa.paste(oscuro, (0, 0), ImageChops.subtract(brillo, mascara))
    capa.paste(color + (255,), (0, 0), mascara)
    capa.paste(claro, (0, 0), centro)
    return capa


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


def cartel_saliente(texto, f, estilo):
    """
    Cartel que sale de la pared, de hasta 4 letras en vertical.
    caja-*: caja de luz blanca con letras y borde de color. neon-*: tubos de neon sobre negro.
    """
    lugares = 4
    largo = SEP + lugares * (CELDA + SEP)
    ancho, alto = CELDA + 2 * SEP, largo
    tipo, color = estilo.split("-")
    n = len(texto)
    hueco = (largo - n * CELDA) / (n + 1)
    if tipo == "caja":
        tinta = CAJAS[color]
        img = Image.new("RGB", (ancho, alto), BLANCO)
        d = ImageDraw.Draw(img)
        d.rectangle([0, 0, ancho - 1, alto - 1], outline=tinta)
        d.rectangle([1, 1, ancho - 2, alto - 2], outline=tinta)
        for i, ch in enumerate(texto):
            p = int(round(hueco + i * (CELDA + hueco)))
            img.paste(tinta, (SEP, p), letra(ch, f, True))
        return img
    tono = NEONES[color]
    img = Image.new("RGBA", (ancho, alto), (14, 10, 18, 255))
    d = ImageDraw.Draw(img)
    otro = NEONES["cian" if color != "cian" else "rosa"]
    d.rectangle([1, 1, ancho - 2, alto - 2], outline=tuple(int(c * 0.8) for c in otro) + (255,))
    for i, ch in enumerate(texto):
        p = int(round(hueco + i * (CELDA + hueco)))
        capa = tubo(letra(ch, f, True), tono)
        img.alpha_composite(capa, (SEP, p))
    return img.convert("RGB")


def neon_suelto(texto, f, estilo):
    """Letras de neon sueltas, en horizontal, sobre fondo transparente (se ven contra el cielo)."""
    lugares = 5
    largo = SEP + lugares * (CELDA + SEP)
    img = Image.new("RGBA", (largo, CELDA + 2 * SEP), (0, 0, 0, 0))
    tono = NEONES[estilo.split("-")[1]]
    # Juntas y centradas (con dos letras repartidas en cinco lugares quedaban muy separadas).
    n = len(texto)
    junto = 4
    x0 = (largo - (n * CELDA + (n - 1) * junto)) // 2
    for i, ch in enumerate(texto):
        img.alpha_composite(tubo(letra(ch, f, False), tono), (x0 + i * (CELDA + junto), SEP))
    return img


def placa(texto, f):
    """Placa de direccion azul de borde blanco: calle arriba, numero abajo."""
    img = Image.new("RGB", (56, 30), PLACA_AZUL)
    d = ImageDraw.Draw(img)
    d.rectangle([1, 1, 54, 28], outline=BLANCO)
    arriba, abajo = texto.split("|")
    renglon(img, arriba, f, 12, 3, BLANCO)
    renglon(img, abajo, f, 12, 15, BLANCO)
    return img


def flecha(texto, f, estilo):
    """Cartel verde de salida: 出口 y una flecha (a la derecha, a la izquierda o hacia arriba)."""
    img = Image.new("RGB", (56, 30), FLECHA_VERDE)
    d = ImageDraw.Draw(img)
    d.rectangle([1, 1, 54, 28], outline=BLANCO)
    sentido = estilo.split("-")[1]
    letras_x = 5 if sentido != "izq" else 21
    for i, ch in enumerate(texto[:2]):
        img.paste(BLANCO, (letras_x + i * 15, 7), letra_chica(ch, f, 16, 120))
    if sentido == "der":
        d.polygon([(38, 12), (44, 12), (44, 8), (51, 15), (44, 22), (44, 18), (38, 18)], fill=BLANCO)
    elif sentido == "izq":
        d.polygon([(18, 12), (12, 12), (12, 8), (5, 15), (12, 22), (12, 18), (18, 18)], fill=BLANCO)
    else:
        d.polygon([(41, 24), (41, 15), (37, 15), (44, 7), (51, 15), (47, 15), (47, 24)], fill=BLANCO)
    return img


def papelitos(texto, f, semilla):
    """Tres o cuatro papelitos pegados y torcidos, con una palabra grande, renglones y telefono."""
    import random
    rnd = random.Random(semilla)
    img = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    papeles = [(236, 230, 212), (232, 212, 106), (232, 168, 182), (190, 216, 168), (246, 246, 240)]
    tintas = [(170, 30, 24), (30, 22, 18), (24, 54, 120)]
    items = texto.split("|")
    for k, palabra in enumerate(items):
        w, h = rnd.randint(18, 24), rnd.randint(22, 28)
        hoja = Image.new("RGBA", (w, h), papeles[rnd.randrange(len(papeles))] + (255,))
        d = ImageDraw.Draw(hoja)
        tinta = tintas[rnd.randrange(len(tintas))]
        x = 2
        for ch in palabra[:2]:
            hoja.paste(tinta + (255,), (x, 2), letra_chica(ch, f, 9, 100))
            x += 10
        for y in range(14, h - 6, 3):
            d.line([2, y, w - 3 - rnd.randint(0, 6), y], fill=(110, 100, 90, 255))
        if rnd.random() < 0.6:  # tiritas para arrancar con el telefono
            for x in range(1, w - 1, 4):
                d.line([x, h - 5, x, h - 1], fill=(120, 110, 100, 255))
        else:
            d.line([2, h - 3, w - 3, h - 3], fill=tinta + (255,))
        hoja = hoja.rotate(rnd.uniform(-14, 14), resample=Image.NEAREST, expand=True)
        px = int((k % 2) * 22 + rnd.randint(-2, 4))
        py = int((k // 2) * 20 + rnd.randint(-1, 4))
        img.alpha_composite(hoja, (max(0, min(48 - hoja.width, px)), max(0, min(48 - hoja.height, py))))
    return img


def gato(d, foto):
    """Silueta de un gato sentado, clara y con manchas oscuras (para el afiche de 尋貓)."""
    x0, y0, x1, y1 = foto
    cx = (x0 + x1) // 2
    claro, mancha = (200, 188, 168), (40, 36, 32)
    d.ellipse([cx - 7, y0 + 11, cx + 6, y1 - 1], fill=claro)            # cuerpo
    d.ellipse([cx - 5, y0 + 4, cx + 4, y0 + 12], fill=claro)            # cabeza
    d.polygon([(cx - 5, y0 + 6), (cx - 4, y0 + 1), (cx - 1, y0 + 5)], fill=claro)  # oreja
    d.polygon([(cx + 4, y0 + 6), (cx + 3, y0 + 1), (cx, y0 + 5)], fill=claro)      # oreja
    d.line([cx + 6, y1 - 3, cx + 9, y0 + 12], fill=claro, width=2)      # cola
    for mx, my in ((cx - 3, y0 + 15), (cx + 2, y0 + 19), (cx - 1, y0 + 7)):
        d.rectangle([mx, my, mx + 2, my + 2], fill=mancha)              # las manchas


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
    if texto == "尋貓":
        gato(d, foto)
    else:
        d.ellipse([cx - 4, top + 4, cx + 4, top + 12], fill=(150, 140, 125))    # cabeza
        d.rectangle([cx - 6, top + 13, cx + 6, foto[3]], fill=(150, 140, 125)) # hombros
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
        elif formato == "S":
            img = cartel_saliente(texto, f, estilo)
        elif formato == "N":
            img = neon_suelto(texto, f, estilo)
        elif formato == "D":
            img = placa(texto, f)
        elif formato == "F":
            img = flecha(texto, f, estilo)
        elif formato == "A":
            img = papelitos(texto, f, len(filas) * 7919)
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
