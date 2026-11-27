#!/usr/bin/env python3
"""
Genera los sprites de la interfaz en Assets/Resources/UI/ junto con sus .meta de Unity.
Uso:  python3 Tools/GenerarSprites.py     (requiere Pillow)

Los sprites se cargan desde codigo con Resources.Load<Sprite>("UI/<nombre>").
Puedes reemplazar cualquier PNG por tu propio arte conservando el nombre.
"""
import hashlib
import math
import os
from PIL import Image, ImageDraw, ImageFilter

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SALIDA = os.path.join(RAIZ, "Assets", "Resources", "UI")
os.makedirs(SALIDA, exist_ok=True)

SS = 4  # supersampling para bordes suaves

# Paleta
VERDE = (74, 222, 128)
VERDE_OSC = (22, 101, 52)
CIAN = (34, 211, 238)
AZUL = (56, 189, 248)
AMBAR = (251, 191, 36)
NARANJA = (249, 115, 22)
ACERO = (203, 213, 225)
ACERO_OSC = (100, 116, 139)
VIDRIO = (52, 211, 153)
BLANCO = (255, 255, 255)
FONDO = (11, 31, 36)
FONDO2 = (18, 52, 56)


def guid_para(nombre):
    return hashlib.md5(("incremental-dron-v2/" + nombre).encode()).hexdigest()


META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 11
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {b}, y: {b}, z: {b}, w: {b}}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guardar(img, nombre, tam, borde=0):
    img = img.resize((tam, tam), Image.LANCZOS)
    ruta = os.path.join(SALIDA, nombre + ".png")
    img.save(ruta)
    with open(ruta + ".meta", "w") as f:
        f.write(META.format(guid=guid_para(nombre), b=borde))


def lienzo(tam=128):
    n = tam * SS
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img), n


def P(v, n):
    """Coordenada normalizada 0..1 -> pixeles del lienzo."""
    return v * n


# ------------------------------------------------------------------ formas base
def caja_redondeada(nombre, tam, radio, relleno, borde_color=None, borde_px=0, borde9=None,
                    gradiente=None, brillo=False):
    img, d, n = lienzo(tam)
    r = radio * SS
    if gradiente:
        g = Image.new("RGBA", (n, n))
        gd = ImageDraw.Draw(g)
        for y in range(n):
            t = y / (n - 1)
            c = tuple(int(gradiente[0][i] * (1 - t) + gradiente[1][i] * t) for i in range(3)) + (255,)
            gd.line([(0, y), (n, y)], fill=c)
        mascara = Image.new("L", (n, n), 0)
        ImageDraw.Draw(mascara).rounded_rectangle([0, 0, n - 1, n - 1], r, fill=255)
        img.paste(g, (0, 0), mascara)
    else:
        d.rounded_rectangle([0, 0, n - 1, n - 1], r, fill=relleno)
    if brillo:
        capa = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        cd = ImageDraw.Draw(capa)
        cd.rounded_rectangle([SS * 3, SS * 2, n - SS * 3, n * 0.46], r * 0.8, fill=(255, 255, 255, 38))
        mascara = Image.new("L", (n, n), 0)
        ImageDraw.Draw(mascara).rounded_rectangle([0, 0, n - 1, n - 1], r, fill=255)
        capa.putalpha(Image.composite(capa.getchannel("A"), Image.new("L", (n, n), 0), mascara))
        img = Image.alpha_composite(img, capa)
        d = ImageDraw.Draw(img)
    if borde_color and borde_px:
        d.rounded_rectangle([0, 0, n - 1, n - 1], r, outline=borde_color, width=borde_px * SS)
    guardar(img, nombre, tam, borde9 if borde9 is not None else int(radio) + 4)


def sombra(img, desplazamiento=6, difuminado=10, alfa=110):
    n = img.size[0]
    s = Image.new("RGBA", img.size, (0, 0, 0, 0))
    s.putalpha(img.getchannel("A").point(lambda a: alfa if a > 0 else 0))
    s = s.filter(ImageFilter.GaussianBlur(difuminado * SS / 4))
    base = Image.new("RGBA", img.size, (0, 0, 0, 0))
    base.paste(s, (0, desplazamiento * SS // 2))
    return Image.alpha_composite(base, img)


# ------------------------------------------------------------------ iconos
def icono_plastico():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    # tapon
    d.rounded_rectangle([*c(.40, .07), *c(.60, .17)], 8 * SS, fill=(14, 116, 144))
    # cuello
    d.polygon([c(.42, .17), c(.58, .17), c(.60, .28), c(.40, .28)], fill=(125, 211, 252))
    # cuerpo
    d.rounded_rectangle([*c(.28, .26), *c(.72, .93)], 16 * SS, fill=AZUL)
    # etiqueta
    d.rectangle([*c(.28, .46), *c(.72, .68)], fill=(14, 165, 233))
    d.rectangle([*c(.28, .50), *c(.72, .64)], fill=(224, 242, 254))
    # brillo
    d.rounded_rectangle([*c(.34, .30), *c(.40, .88)], 6 * SS, fill=(255, 255, 255, 110))
    guardar(sombra(img), "icon_plastico", 128)


def icono_metal():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    d.ellipse([*c(.24, .08), *c(.76, .26)], fill=(226, 232, 240))
    d.rectangle([*c(.24, .17), *c(.76, .84)], fill=ACERO)
    d.ellipse([*c(.24, .75), *c(.76, .93)], fill=ACERO_OSC)
    d.rectangle([*c(.24, .17), *c(.76, .80)], fill=ACERO)
    # banda naranja
    d.rectangle([*c(.24, .38), *c(.76, .62)], fill=NARANJA)
    d.rectangle([*c(.24, .44), *c(.76, .56)], fill=(254, 215, 170))
    d.ellipse([*c(.24, .08), *c(.76, .26)], fill=(241, 245, 249))
    d.ellipse([*c(.36, .12), *c(.64, .22)], outline=ACERO_OSC, width=3 * SS)
    d.rectangle([*c(.30, .25), *c(.37, .82)], fill=(255, 255, 255, 120))
    guardar(sombra(img), "icon_metal", 128)


def icono_vidrio():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    d.rounded_rectangle([*c(.43, .05), *c(.57, .14)], 6 * SS, fill=(6, 95, 70))
    d.polygon([c(.44, .14), c(.56, .14), c(.57, .42), c(.43, .42)], fill=(16, 185, 129))
    d.polygon([c(.43, .38), c(.57, .38), c(.74, .55), c(.74, .90), c(.26, .90), c(.26, .55)], fill=VIDRIO)
    d.rounded_rectangle([*c(.26, .80), *c(.74, .94)], 12 * SS, fill=(5, 150, 105))
    d.rectangle([*c(.26, .56), *c(.74, .74)], fill=(209, 250, 229))
    d.rounded_rectangle([*c(.32, .45), *c(.38, .88)], 6 * SS, fill=(255, 255, 255, 120))
    guardar(sombra(img), "icon_vidrio", 128)


def icono_dron():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    # brazos
    d.line([c(.2, .30), c(.8, .70)], fill=(148, 163, 184), width=9 * SS)
    d.line([c(.8, .30), c(.2, .70)], fill=(148, 163, 184), width=9 * SS)
    # helices
    for x, y in [(.18, .28), (.82, .28), (.18, .72), (.82, .72)]:
        d.ellipse([*c(x - .13, y - .045), *c(x + .13, y + .045)], fill=(34, 211, 238, 230))
        d.ellipse([*c(x - .03, y - .03), *c(x + .03, y + .03)], fill=(15, 23, 42))
    # cuerpo
    d.rounded_rectangle([*c(.34, .36), *c(.66, .64)], 18 * SS, fill=(241, 245, 249))
    d.rounded_rectangle([*c(.34, .50), *c(.66, .64)], 18 * SS, fill=(203, 213, 225))
    d.ellipse([*c(.43, .40), *c(.57, .54)], fill=(15, 23, 42))
    d.ellipse([*c(.47, .43), *c(.53, .49)], fill=VERDE)
    guardar(sombra(img), "icon_dron", 128)


def icono_bodega():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    d.polygon([c(.50, .10), c(.88, .30), c(.50, .50), c(.12, .30)], fill=(253, 224, 71))
    d.polygon([c(.12, .30), c(.50, .50), c(.50, .92), c(.12, .72)], fill=(217, 119, 6))
    d.polygon([c(.88, .30), c(.50, .50), c(.50, .92), c(.88, .72)], fill=(245, 158, 11))
    d.polygon([c(.41, .15), c(.59, .25), c(.40, .36), c(.22, .26)], fill=(254, 249, 195))
    d.line([c(.50, .50), c(.50, .92)], fill=(146, 64, 14), width=3 * SS)
    guardar(sombra(img), "icon_bodega", 128)


def icono_motor():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    d.polygon([c(.58, .05), c(.22, .55), c(.46, .55), c(.38, .95), c(.78, .40), c(.54, .40)],
              fill=(250, 204, 21))
    d.polygon([c(.58, .05), c(.46, .24), c(.60, .24), c(.54, .40), c(.78, .40)], fill=(254, 240, 138))
    guardar(sombra(img), "icon_motor", 128)


def icono_iman():
    img, d, n = lienzo()
    cx, cy = n * .5, n * .52
    r_ext, r_int = n * .36, n * .17
    # arco en herradura
    d.pieslice([cx - r_ext, cy - r_ext, cx + r_ext, cy + r_ext], 180, 360, fill=(239, 68, 68))
    d.rectangle([cx - r_ext, cy, cx - r_int, n * .78], fill=(239, 68, 68))
    d.rectangle([cx + r_int, cy, cx + r_ext, n * .78], fill=(239, 68, 68))
    d.pieslice([cx - r_int, cy - r_int, cx + r_int, cy + r_int], 180, 360, fill=(0, 0, 0, 0))
    # vaciar centro
    hueco = Image.new("L", img.size, 0)
    hd = ImageDraw.Draw(hueco)
    hd.pieslice([cx - r_int, cy - r_int, cx + r_int, cy + r_int], 180, 360, fill=255)
    hd.rectangle([cx - r_int, cy, cx + r_int, n * .8], fill=255)
    img.putalpha(Image.composite(Image.new("L", img.size, 0), img.getchannel("A"), hueco))
    d = ImageDraw.Draw(img)
    # puntas
    d.rectangle([cx - r_ext, n * .70, cx - r_int, n * .88], fill=(241, 245, 249))
    d.rectangle([cx + r_int, n * .70, cx + r_ext, n * .88], fill=(241, 245, 249))
    guardar(sombra(img), "icon_iman", 128)


def icono_logistica():
    img, d, n = lienzo()
    cx, cy = n * .5, n * .5
    # dientes del engranaje
    for i in range(8):
        a = math.radians(i * 45)
        ux, uy = math.cos(a), math.sin(a)
        px, py = -uy, ux
        r0, r1, w = n * .30, n * .46, n * .075
        d.polygon([(cx + ux * r0 + px * w, cy + uy * r0 + py * w),
                   (cx + ux * r1 + px * w * .8, cy + uy * r1 + py * w * .8),
                   (cx + ux * r1 - px * w * .8, cy + uy * r1 - py * w * .8),
                   (cx + ux * r0 - px * w, cy + uy * r0 - py * w)], fill=VERDE)
    d.ellipse([cx - n * .36, cy - n * .36, cx + n * .36, cy + n * .36], fill=VERDE)
    d.ellipse([cx - n * .30, cy - n * .30, cx + n * .30, cy + n * .30], fill=(34, 197, 94))
    d.ellipse([cx - n * .15, cy - n * .15, cx + n * .15, cy + n * .15], fill=(11, 31, 36))
    d.ellipse([cx - n * .09, cy - n * .09, cx + n * .09, cy + n * .09], fill=(187, 247, 208))
    guardar(sombra(img), "icon_logistica", 128)


def icono_hoja():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    puntos = []
    for i in range(0, 101):
        t = i / 100
        x = .5 + .36 * math.sin(math.pi * t) * (1 - .0 * t)
        y = .92 - .84 * t
        puntos.append((x, y))
    izq = [(1 - x, y) for x, y in reversed(puntos)]
    forma = [c(x, y) for x, y in puntos + izq]
    d.polygon(forma, fill=VERDE)
    d.polygon([c(x, y) for x, y in (puntos[:70] + [(.5, .5)])], fill=(34, 197, 94))
    d.line([c(.5, .95), c(.5, .20)], fill=VERDE_OSC, width=4 * SS)
    for y in (.4, .55, .7):
        d.line([c(.5, y + .08), c(.66, y - .06)], fill=VERDE_OSC, width=3 * SS)
        d.line([c(.5, y + .08), c(.34, y - .06)], fill=VERDE_OSC, width=3 * SS)
    guardar(sombra(img), "icon_hoja", 128)


def icono_check():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    d.line([c(.20, .52), c(.42, .74), c(.80, .28)], fill=VERDE, width=int(n * .12), joint="curve")
    guardar(img, "icon_check", 64)


def icono_candado():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    d.arc([*c(.30, .12), *c(.70, .62)], 180, 360, fill=(148, 163, 184), width=int(n * .09))
    d.line([c(.32, .38), c(.32, .50)], fill=(148, 163, 184), width=int(n * .09))
    d.line([c(.68, .38), c(.68, .50)], fill=(148, 163, 184), width=int(n * .09))
    d.rounded_rectangle([*c(.22, .46), *c(.78, .88)], 12 * SS, fill=(148, 163, 184))
    d.ellipse([*c(.45, .58), *c(.55, .68)], fill=(30, 41, 59))
    d.rectangle([*c(.48, .64), *c(.52, .78)], fill=(30, 41, 59))
    guardar(img, "icon_candado", 64)


def icono_cerrar():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    d.line([c(.25, .25), c(.75, .75)], fill=BLANCO, width=int(n * .11))
    d.line([c(.75, .25), c(.25, .75)], fill=BLANCO, width=int(n * .11))
    guardar(img, "icon_cerrar", 64)


def icono_mira():
    img, d, n = lienzo(64)
    c = lambda x, y: (P(x, n), P(y, n))
    d.ellipse([*c(.30, .30), *c(.70, .70)], outline=BLANCO, width=int(n * .05))
    for a, b in [((.5, .08), (.5, .24)), ((.5, .76), (.5, .92)), ((.08, .5), (.24, .5)), ((.76, .5), (.92, .5))]:
        d.line([c(*a), c(*b)], fill=BLANCO, width=int(n * .05))
    d.ellipse([*c(.46, .46), *c(.54, .54)], fill=BLANCO)
    guardar(img, "icon_mira", 64)


def icono_pip(nombre, color, borde):
    img, d, n = lienzo(64)
    c = lambda x, y: (P(x, n), P(y, n))
    d.rounded_rectangle([*c(.08, .25), *c(.92, .75)], 14 * SS, fill=color, outline=borde, width=2 * SS)
    guardar(img, nombre, 64, 14)


def icono_play():
    img, d, n = lienzo()
    c = lambda x, y: (P(x, n), P(y, n))
    d.polygon([c(.28, .16), c(.28, .84), c(.84, .5)], fill=BLANCO)
    guardar(img, "icon_play", 64)


def icono_estrella():
    img, d, n = lienzo()
    pts = []
    for i in range(10):
        r = n * (.44 if i % 2 == 0 else .19)
        a = -math.pi / 2 + i * math.pi / 5
        pts.append((n * .5 + r * math.cos(a), n * .52 + r * math.sin(a)))
    d.polygon(pts, fill=AMBAR)
    guardar(sombra(img), "icon_estrella", 64)


def logo_titulo():
    img, d, n = lienzo(256)
    c = lambda x, y: (P(x, n), P(y, n))
    # circulo base
    d.ellipse([*c(.04, .04), *c(.96, .96)], fill=(14, 60, 58), outline=VERDE, width=6 * SS)
    d.ellipse([*c(.10, .10), *c(.90, .90)], outline=(34, 197, 94, 110), width=2 * SS)
    # hoja
    pts = []
    for i in range(0, 101):
        t = i / 100
        pts.append((.50 + .26 * math.sin(math.pi * t), .80 - .52 * t))
    izq = [(1 - x, y) for x, y in reversed(pts)]
    d.polygon([c(x, y) for x, y in pts + izq], fill=(34, 197, 94))
    d.line([c(.5, .82), c(.5, .30)], fill=VERDE_OSC, width=4 * SS)
    # dron sobre la hoja
    d.line([c(.22, .30), c(.78, .52)], fill=(226, 232, 240), width=5 * SS)
    d.line([c(.78, .30), c(.22, .52)], fill=(226, 232, 240), width=5 * SS)
    for x, y in [(.22, .28), (.78, .28), (.22, .54), (.78, .54)]:
        d.ellipse([*c(x - .10, y - .03), *c(x + .10, y + .03)], fill=(34, 211, 238, 230))
    d.rounded_rectangle([*c(.40, .31), *c(.60, .49)], 12 * SS, fill=(241, 245, 249))
    d.ellipse([*c(.46, .35), *c(.54, .43)], fill=(15, 23, 42))
    d.ellipse([*c(.48, .37), *c(.52, .41)], fill=VERDE)
    guardar(img, "logo_titulo", 256)


def gradiente_vertical(nombre, tam, arriba, abajo):
    img = Image.new("RGBA", (tam, tam))
    d = ImageDraw.Draw(img)
    for y in range(tam):
        t = y / (tam - 1)
        col = tuple(int(arriba[i] * (1 - t) + abajo[i] * t) for i in range(4))
        d.line([(0, y), (tam, y)], fill=col)
    ruta = os.path.join(SALIDA, nombre + ".png")
    img.save(ruta)
    with open(ruta + ".meta", "w") as f:
        f.write(META.format(guid=guid_para(nombre), b=0))


def main():
    # --- 9-slice
    caja_redondeada("ui_panel", 128, 26, None, borde_color=(74, 222, 128, 200), borde_px=3,
                    gradiente=((20, 58, 62, 255), (9, 28, 33, 255)), borde9=34)
    caja_redondeada("ui_tarjeta", 128, 22, None, borde_color=(94, 234, 212, 90), borde_px=2,
                    gradiente=((28, 76, 80, 255), (17, 48, 52, 255)), borde9=30)
    caja_redondeada("ui_boton", 64, 16, None, borde_color=(255, 255, 255, 90), borde_px=2,
                    gradiente=((255, 255, 255, 255), (210, 220, 224, 255)), brillo=True, borde9=20)
    caja_redondeada("ui_pildora", 64, 30, (8, 24, 28, 190), borde_color=(74, 222, 128, 70), borde_px=2, borde9=26)
    caja_redondeada("ui_barra_fondo", 64, 14, (6, 20, 24, 230), borde_color=(255, 255, 255, 50), borde_px=2, borde9=16)
    caja_redondeada("ui_barra_relleno", 64, 14, None, gradiente=((134, 239, 172, 255), (22, 163, 74, 255)),
                    brillo=True, borde9=16)
    caja_redondeada("ui_barra_carga", 64, 14, None, gradiente=((125, 211, 252, 255), (14, 165, 233, 255)),
                    brillo=True, borde9=16)
    caja_redondeada("ui_circulo", 64, 31, (255, 255, 255, 255), borde9=0)

    icono_pip("ui_pip_on", (74, 222, 128, 255), (220, 252, 231, 255))
    icono_pip("ui_pip_off", (15, 40, 44, 255), (71, 98, 102, 255))

    # --- iconos
    icono_plastico(); icono_metal(); icono_vidrio(); icono_dron()
    icono_bodega(); icono_motor(); icono_iman(); icono_logistica()
    icono_hoja(); icono_check(); icono_candado(); icono_cerrar(); icono_mira()
    icono_play(); icono_estrella(); logo_titulo()

    gradiente_vertical("ui_fondo_menu", 64, (6, 30, 36, 235), (4, 16, 22, 245))
    print("Sprites generados en", SALIDA)


if __name__ == "__main__":
    main()
